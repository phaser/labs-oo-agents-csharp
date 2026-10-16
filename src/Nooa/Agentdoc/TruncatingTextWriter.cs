// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_truncating_stream.py

using System.Globalization;
using System.Text;

namespace Nooa.Agentdoc;

/// <summary>
/// A text writer with a hard character limit. It keeps the first characters and a rolling window of the last
/// characters. <see cref="ToString"/> returns a notice with the head, the tail and the count of dropped characters.
/// </summary>
/// <remarks>
/// The writer is truncated only when the total written text is longer than the limit. Until then
/// <see cref="ToString"/> returns all text as it was written.
/// </remarks>
public class TruncatingTextWriter : TextWriter
{
    /// <summary>The default limit in characters.</summary>
    public const int DefaultLimit = 50_000;

    private readonly int _limit;
    private readonly int _headLimit;
    private readonly int _tailLimit;
    private readonly StringBuilder _head = new();
    private readonly StringBuilder _tail = new();

    /// <summary>Creates a writer.</summary>
    /// <param name="limit">The maximum characters to keep. Must be above 0.</param>
    /// <param name="tailChars">The characters kept for the tail. Null means half of <paramref name="limit"/>.</param>
    public TruncatingTextWriter(int limit = DefaultLimit, int? tailChars = null)
        : base(CultureInfo.InvariantCulture)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        _limit = limit;
        _tailLimit = tailChars ?? limit / 2;
        ArgumentOutOfRangeException.ThrowIfNegative(_tailLimit, nameof(tailChars));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_tailLimit, limit, nameof(tailChars));
        _headLimit = limit - _tailLimit;
    }

    /// <inheritdoc />
    public override Encoding Encoding => Encoding.Unicode;

    /// <summary>True when the total written text is longer than the limit.</summary>
    public bool WasTruncated => CharsWritten > _limit;

    /// <summary>The total characters written, including the dropped ones.</summary>
    public long CharsWritten { get; private set; }

    /// <inheritdoc />
    public override void Write(char value) => Write([value]);

    /// <inheritdoc />
    public override void Write(string? value) => Write(value.AsSpan());

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count) => Write(buffer.AsSpan(index, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<char> buffer)
    {
        CharsWritten += buffer.Length;
        var room = Math.Max(0, _headLimit - _head.Length);
        var toHead = Math.Min(room, buffer.Length);
        _head.Append(buffer[..toHead]);
        var overflow = buffer[toHead..];
        if (overflow.IsEmpty)
        {
            return;
        }

        // The tail may grow to twice its limit before it is cut back. This keeps each write cheap.
        _tail.Append(overflow);
        if (_tail.Length > 2 * _tailLimit)
        {
            _tail.Remove(0, _tail.Length - _tailLimit);
        }
    }

    /// <summary>Returns the written text, or the truncation notice when <see cref="WasTruncated"/> is true.</summary>
    public override string ToString()
    {
        var tailLength = Math.Min(_tail.Length, _tailLimit);
        var tail = _tail.ToString(_tail.Length - tailLength, tailLength);
        if (!WasTruncated)
        {
            return _head + tail;
        }

        var dropped = CharsWritten - _head.Length - tailLength;
        return FormatNotice(_head.ToString(), tail, CharsWritten, dropped);
    }

    /// <summary>Tells the reader whether the dropped text can be found. A derived writer adds a file path. The text ends with a line break.</summary>
    protected virtual string RecoveryNotice(long total, long dropped)
        => $"The {Count(dropped)} chars in the middle are not recoverable.\n";

    /// <summary>Formats a count with a thousands separator.</summary>
    protected static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private string FormatNotice(string head, string tail, long total, long dropped)
        => $"<truncated-output>\nOutput too large ({Count(total)} chars). Showing first {Count(head.Length)} and last {Count(tail.Length)} chars.\n"
        + $"{RecoveryNotice(total, dropped)}\n{head}\n\n... {Count(dropped)} chars not shown ...\n\n{tail}\n</truncated-output>";
}
