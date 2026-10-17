// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>
/// The model output does not match the requested type. The message holds a short description only and never
/// the model text, because messages reach logs and traces. The caller builds feedback for the model from
/// <see cref="OutputType"/>, <see cref="Path"/> and <see cref="Detail"/>.
/// </summary>
public sealed class StructuredOutputException : ValidationException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="outputType">The type that was requested.</param>
    /// <param name="detail">What is wrong, in words that contain no model text.</param>
    /// <param name="content">The text that failed to parse. Do not log it.</param>
    /// <param name="path">The JSON path of the problem, such as <c>$.score</c>, or null when unknown.</param>
    /// <param name="innerException">The parse or conversion error.</param>
    /// <param name="isMalformedJson">True when the text is not JSON at all, as opposed to JSON of the wrong shape.</param>
    public StructuredOutputException(
        Type outputType, string detail, string content, string? path = null, Exception? innerException = null, bool isMalformedJson = false)
        : base($"The output is not valid {outputType.Name}: {detail}", innerException!)
    {
        OutputType = outputType;
        Detail = detail;
        Content = content;
        Path = path;
        IsMalformedJson = isMalformedJson;
    }

    /// <summary>The type that was requested.</summary>
    public Type OutputType { get; }

    /// <summary>What is wrong, with no model text in it.</summary>
    public string Detail { get; }

    /// <summary>The JSON path of the problem, or null when unknown.</summary>
    public string? Path { get; }

    /// <summary>The text that failed to parse. Do not log it.</summary>
    public string Content { get; }

    /// <summary>True when the text is not JSON at all, as opposed to JSON of the wrong shape.</summary>
    public bool IsMalformedJson { get; }
}
