// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/formatting.py

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Nooa.Agentdoc;
using Nooa.Config;

namespace Nooa.Errors;

/// <summary>
/// Turns compile errors and runtime exceptions of a REPL cell into short text for the model.
/// </summary>
/// <remarks>
/// <para>
/// The text has the form of IPython output. A location reads <c>Cell In[N], line L</c>, relative to the code that the model
/// wrote (the engine runs no prologue in the same source). Framework frames are removed. Frames of the cell and of
/// user code that the cell calls stay. Source lines come from the cell code, never from a file on disk.
/// The text holds no environment value, no file path and no stack of the framework. The final text is cut to the error budget.
/// </para>
/// <para>The formatter never replaces the original failure with a formatter failure. On any internal error it falls back to <c>Type: message</c>.</para>
/// </remarks>
public static partial class ErrorFormatting
{
    private const int _maxCompileErrors = 10;
    private const int _maxChain = 5;
    private const int _maxSourceChars = 300;
    private const string _cellName = "<cell>";
    private const string _causeNotice = "The above exception was the direct cause of the following exception:";

    /// <summary>Formats an error for model feedback and cuts it to the error budget.</summary>
    /// <param name="error">A compile error, a validation error or an exception that the cell threw.</param>
    /// <param name="code">The code of the current cell. It gives the source lines of runtime frames.</param>
    /// <param name="executionCount">The number of the current cell. Source lines show for frames of this cell only.</param>
    /// <param name="maxError">Maximum characters. Null or not above 0 means <see cref="CaptureConfig.MaxError"/> of the default config.</param>
    /// <param name="tailChars">Characters kept for the tail. Null means the default split.</param>
    public static string FormatForLlm(
        Exception error,
        string? code = null,
        int? executionCount = null,
        int? maxError = null,
        int? tailChars = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            var text = error switch
            {
                CompilationErrorException compile => FormatDiagnostics(compile.Diagnostics),
                ValidationException validation => FormatValidation(validation),
                _ => FormatRuntime(error, code, executionCount),
            };
            return Bound(text, maxError, tailChars);
        }
#pragma warning disable CA1031 // The presentation boundary must not hide the original failure behind its own failure.
        catch (Exception)
#pragma warning restore CA1031
        {
            return Bound(Concise(error), maxError, tailChars);
        }
    }

    /// <summary>Formats the error diagnostics of a failed compile. Warnings and hints are left out.</summary>
    public static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToList();
        if (errors.Count == 0)
        {
            return "Compilation failed.";
        }

        var blocks = errors.Take(_maxCompileErrors).Select(FormatDiagnostic).ToList();
        if (errors.Count > _maxCompileErrors)
        {
            blocks.Add($"... and {errors.Count - _maxCompileErrors} more errors");
        }

        return string.Join("\n\n", blocks);
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var sb = new StringBuilder();
        var message = $"error {diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";
        if (!diagnostic.Location.IsInSource)
        {
            return message;
        }

        var span = diagnostic.Location.GetLineSpan();
        var line = span.StartLinePosition.Line;
        sb.Append(CultureInfo.InvariantCulture, $"{LocationName(span.Path)}line {line + 1}\n");
        var lines = diagnostic.Location.SourceTree!.GetText().Lines;
        if (line < lines.Count)
        {
            var source = lines[line].ToString();
            var column = Math.Min(span.StartLinePosition.Character, source.Length);
            var length = span.StartLinePosition.Line == span.EndLinePosition.Line
                ? span.EndLinePosition.Character - span.StartLinePosition.Character
                : source.Length - column;
            var marker = new string('^', Math.Clamp(length, 1, Math.Max(1, source.Length - column)));
            var indent = string.Concat(source.Take(column).Select(c => c == '\t' ? '\t' : ' '));
            if (source.Length > _maxSourceChars)
            {
                source = source[.._maxSourceChars] + "...";
            }

            sb.Append(CultureInfo.InvariantCulture, $"  {source}\n  {indent}{marker}\n");
        }

        sb.Append(message);
        return sb.ToString();
    }

    private static string LocationName(string path) => path.Length == 0 ? "" : path + ", ";

    private static string FormatValidation(ValidationException error) =>
        error is RestrictedCodeException { LineNumber: { } line }
            ? $"line {line}\n{error.GetType().Name}: {error.Message}"
            : $"{error.GetType().Name}: {error.Message}";

    private static string FormatRuntime(Exception error, string? code, int? executionCount)
    {
        var sb = new StringBuilder();
        Render(error, code, executionCount, sb, [], 0);
        return sb.ToString().TrimEnd();
    }

    private static void Render(Exception error, string? code, int? executionCount, StringBuilder sb, HashSet<Exception> seen, int depth)
    {
        var current = Unwrap(error);
        if (!seen.Add(current) || depth >= _maxChain)
        {
            return;
        }

        if (current is AggregateException { InnerExceptions.Count: > 1 } many)
        {
            foreach (var child in many.InnerExceptions.Take(_maxChain))
            {
                Render(child, code, executionCount, sb, seen, depth + 1);
                sb.Append('\n');
            }

            return;
        }

        if (current.InnerException is { } cause)
        {
            var before = sb.Length;
            Render(cause, code, executionCount, sb, seen, depth + 1);
            if (sb.Length > before)
            {
                sb.Append('\n').Append(_causeNotice).Append("\n\n");
            }
        }

        AppendFrames(current, code, executionCount, sb);
        sb.Append(Concise(current)).Append('\n');
    }

    private static Exception Unwrap(Exception error)
    {
        while (true)
        {
            var inner = error switch
            {
                TargetInvocationException t => t.InnerException,
                AggregateException { InnerExceptions.Count: 1 } a => a.InnerExceptions[0],
                _ => null,
            };
            if (inner is null)
            {
                return error;
            }

            error = inner;
        }
    }

    private static void AppendFrames(Exception error, string? code, int? executionCount, StringBuilder sb)
    {
        var frames = new StackTrace(error, true).GetFrames().Reverse().ToList();
        var first = frames.FindIndex(f => CellNumber(f) is not null);
        if (first < 0)
        {
            return;
        }

        string[]? lines = code?.Split('\n');
        foreach (var frame in frames.Skip(first))
        {
            var cell = CellNumber(frame);
            var name = frame.GetMethod() is { } method ? DisplayName(method) : _cellName;
            if (cell is null)
            {
                if (frame.GetMethod() is { DeclaringType: { } type } && IsUserType(type))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"  at {type.FullName}.{name}\n");
                }

                continue;
            }

            var line = frame.GetFileLineNumber();
            sb.Append(CultureInfo.InvariantCulture, $"Cell In[{cell}]");
            if (line > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $", line {line}");
            }

            sb.Append(CultureInfo.InvariantCulture, $", in {name}\n");
            if (line > 0 && cell == executionCount && lines is not null && line <= lines.Length)
            {
                var source = lines[line - 1].Trim();
                if (source.Length > _maxSourceChars)
                {
                    source = source[.._maxSourceChars] + "...";
                }

                sb.Append(CultureInfo.InvariantCulture, $"  {source}\n");
            }
        }
    }

    private static int? CellNumber(StackFrame frame) =>
        frame.GetFileName() is { } file && TryMatch(CellFile(), file) is { } match
            ? int.Parse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture)
            : null;

    private static bool IsUserType(Type type)
    {
        var assembly = type.Assembly;
        if (assembly == typeof(ErrorFormatting).Assembly)
        {
            return false;
        }

        var name = assembly.GetName().Name ?? "";
        return !(name.StartsWith("System", StringComparison.Ordinal)
            || name.StartsWith("Microsoft", StringComparison.Ordinal)
            || name is "netstandard" or "mscorlib");
    }

    private static string DisplayName(MethodBase method)
    {
        var raw = method.Name == "MoveNext" && method.DeclaringType is { } type ? type.Name : method.Name;
        if (TryMatch(LocalFunctionName(), raw) is { } local)
        {
            return local.Groups[1].Value;
        }

        if (TryMatch(GeneratedName(), raw) is { } generated)
        {
            var name = generated.Groups[1].Value;
            return name == "Initialize" ? _cellName : name;
        }

        return raw;
    }

    private static string Concise(Exception error)
    {
        var name = error.GetType().Name;
        string message;
        try
        {
            message = error.Message;
        }
#pragma warning disable CA1031 // A hostile Message property must not break the diagnostic.
        catch (Exception)
#pragma warning restore CA1031
        {
            return name;
        }

        return message.Length > 0 ? $"{name}: {message}" : name;
    }

    private static string Bound(string text, int? maxError, int? tailChars)
    {
        var defaults = TruncationConfig.Default.Capture;
        var limit = maxError is > 0 ? maxError.Value : defaults.MaxError;
        int? tail = tailChars is { } t && t >= 0 && t < limit
            ? t
            : defaults.Tail is { } d ? Math.Min(d, limit - 1) : null;
        using var writer = new TruncatingTextWriter(limit, tail);
        writer.Write(text);
        return writer.ToString();
    }

    // A timeout counts as no match (fail closed).
    private static Match? TryMatch(Regex regex, string input)
    {
        try
        {
            return regex.Match(input) is { Success: true } match ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^Cell In\[(\d+)\]$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CellFile();

    [GeneratedRegex(@">g__([^|>]+)\|", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LocalFunctionName();

    [GeneratedRegex(@"^<+([^<>]+)>", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GeneratedName();
}
