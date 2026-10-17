// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/out_accessor.py

using System.Globalization;
using System.Text;
using Nooa.Agentdoc;

namespace Nooa.Runtime;

/// <summary>
/// Jupyter-style access to the values of earlier cells: <c>Out[n]</c> is the value of cell n and <c>Out[-1]</c> is the last value.
/// </summary>
/// <remarks>
/// Only a cell with a non-null value is stored. The values are live objects in memory.
/// They are not part of a snapshot, because persisted events hold JSON only (D11).
/// </remarks>
public sealed class OutAccessor
{
    private const int _reprChars = 500;

    private readonly List<(int Count, object Value)> _entries = [];

    /// <summary>The number of stored values.</summary>
    public int Count => _entries.Count;

    /// <summary>The last stored value.</summary>
    /// <exception cref="InvalidOperationException">No value is stored yet.</exception>
    public object Last => _entries.Count > 0
        ? _entries[^1].Value
        : throw new InvalidOperationException("No outputs recorded yet");

    /// <summary>Returns the value of cell n (n above 0), or the nth value from the end (n below 0).</summary>
    /// <exception cref="IndexOutOfRangeException">No value is stored yet, or n below 0 is out of range.</exception>
    /// <exception cref="KeyNotFoundException">Cell n has no stored value.</exception>
    public object this[int index]
    {
        get
        {
            if (_entries.Count == 0)
            {
                throw new IndexOutOfRangeException("No outputs recorded yet");
            }

            if (index < 0)
            {
                return -index <= _entries.Count
                    ? _entries[^(-index)].Value
                    : throw new IndexOutOfRangeException($"Out index {index} out of range (have {_entries.Count} outputs)");
            }

            // The cell numbers are sparse because a cell without a value stores nothing.
            foreach (var (count, value) in _entries)
            {
                if (count == index)
                {
                    return value;
                }
            }

            throw new KeyNotFoundException($"No output for execution {index}");
        }
    }

    /// <summary>True when cell <paramref name="executionCount"/> has a stored value.</summary>
    public bool Contains(int executionCount) => _entries.Any(e => e.Count == executionCount);

    /// <summary>Stores the value of a cell. A null value is not stored.</summary>
    internal void Record(int executionCount, object? value)
    {
        if (value is not null)
        {
            _entries.Add((executionCount, value));
        }
    }

    /// <summary>Lists each stored value as <c>Out[n]: value</c>, with a cut for long values.</summary>
    public override string ToString()
    {
        if (_entries.Count == 0)
        {
            return "Out (no outputs yet)";
        }

        var sb = new StringBuilder();
        foreach (var (count, value) in _entries)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(CultureInfo.InvariantCulture, $"Out[{count}]: {PFormat.TruncatingFormat(value, _reprChars)}");
        }

        return sb.ToString();
    }
}
