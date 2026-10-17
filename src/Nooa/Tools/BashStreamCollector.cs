// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Nooa.Agentdoc;

namespace Nooa.Tools;

/// <summary>
/// Collects the text of one bash output stream between two framing markers. The text before the marker is command output.
/// The text after the marker (stdout only) is the status payload. A marker holds a random sentinel that only the host and
/// the shell protocol know, so command output cannot forge it.
/// </summary>
internal sealed class BashStreamCollector
{
    private const int _maxPayloadChars = 1 << 20;

    private readonly object _gate = new();
    private readonly int _limit;
    private readonly bool _expectPayload;
    private readonly StringBuilder _pending = new();
    private readonly StringBuilder _payload = new();
    private TruncatingTextWriter _writer;
    private Phase _phase = Phase.Idle;
    private string _marker = string.Empty;
    private string _endMarker = string.Empty;
    private Action<string>? _sink;
    private TaskCompletionSource _done = NewCompletion();

    public BashStreamCollector(int limit, bool expectPayload)
    {
        _limit = limit;
        _expectPayload = expectPayload;
        _writer = new TruncatingTextWriter(limit);
    }

    private enum Phase
    {
        Idle,
        Output,
        Payload,
        Finished,
        Eof,
    }

    /// <summary>Completes when the end marker arrives, or when the stream ends.</summary>
    public Task Done
    {
        get
        {
            lock (_gate)
            {
                return _done.Task;
            }
        }
    }

    /// <summary>True when the end marker arrived. False means the stream ended or the command is still running.</summary>
    public bool Framed
    {
        get
        {
            lock (_gate)
            {
                return _phase == Phase.Finished;
            }
        }
    }

    /// <summary>The bounded text collected so far.</summary>
    public string Text
    {
        get
        {
            lock (_gate)
            {
                return _writer.ToString();
            }
        }
    }

    /// <summary>The status payload: exit code, directory and job PIDs, one per line. Empty until the end marker arrives.</summary>
    public string Payload
    {
        get
        {
            lock (_gate)
            {
                return _phase == Phase.Finished ? _payload.ToString() : string.Empty;
            }
        }
    }

    /// <summary>Starts a new command. The collector drops text that arrived since the last command.</summary>
    /// <param name="sentinel">The random sentinel of this command.</param>
    /// <param name="sink">A callback for each piece of command output as it arrives. Null for none.</param>
    public void Begin(string sentinel, Action<string>? sink = null)
    {
        lock (_gate)
        {
            if (_phase == Phase.Eof)
            {
                return;
            }

            _writer = new TruncatingTextWriter(_limit);
            _pending.Clear();
            _payload.Clear();
            _marker = "\n" + sentinel + "\n";
            _endMarker = _marker;
            _sink = sink;
            _phase = Phase.Output;
            _done = NewCompletion();
        }
    }

    /// <summary>Takes a decoded piece of the stream.</summary>
    public void Append(ReadOnlySpan<char> chunk)
    {
        lock (_gate)
        {
            switch (_phase)
            {
                case Phase.Output:
                    AppendOutput(chunk);
                    break;
                case Phase.Payload:
                    AppendPayload(chunk);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Tells the collector that the stream ended.</summary>
    public void MarkEof()
    {
        lock (_gate)
        {
            if (_phase == Phase.Output && _pending.Length > 0)
            {
                Emit(_pending.ToString());
                _pending.Clear();
            }

            if (_phase != Phase.Finished)
            {
                _phase = Phase.Eof;
            }

            _done.TrySetResult();
        }
    }

    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void AppendOutput(ReadOnlySpan<char> chunk)
    {
        _pending.Append(chunk);
        var text = _pending.ToString();
        var index = text.IndexOf(_marker, StringComparison.Ordinal);
        if (index >= 0)
        {
            Emit(text[..index]);
            _pending.Clear();
            var rest = text[(index + _marker.Length)..];
            if (_expectPayload)
            {
                _phase = Phase.Payload;
                _payload.Append('\n'); // The begin marker used the line break that opens the payload.
                AppendPayload(rest);
            }
            else
            {
                Finish();
            }

            return;
        }

        // Keep only a tail that can still grow into the marker. The rest is command output.
        var keepFrom = text.Length;
        for (var i = Math.Max(0, text.Length - (_marker.Length - 1)); i < text.Length; i++)
        {
            if (text[i] == '\n' && _marker.AsSpan().StartsWith(text.AsSpan(i)))
            {
                keepFrom = i;
                break;
            }
        }

        Emit(text[..keepFrom]);
        _pending.Clear().Append(text, keepFrom, text.Length - keepFrom);
    }

    private void AppendPayload(ReadOnlySpan<char> chunk)
    {
        if (_payload.Length < _maxPayloadChars)
        {
            _payload.Append(chunk);
        }

        var text = _payload.ToString();
        var index = text.IndexOf(_endMarker, StringComparison.Ordinal);
        if (index >= 0)
        {
            _payload.Clear().Append(text, 1, index - 1); // Drop the opening line break and the end marker.
            Finish();
        }
    }

    private void Finish()
    {
        _phase = Phase.Finished;
        _done.TrySetResult();
    }

    private void Emit(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        _writer.Write(text);
        _sink?.Invoke(text);
    }
}
