// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace Nooa.Agentdoc;

/// <summary>
/// A string writer for a one-line attempt. <see cref="Overflowed"/> turns true when the text reaches the width,
/// so the renderer can stop and use the multi-line form.
/// </summary>
internal sealed class WidthLimitedWriter(int width) : StringWriter(CultureInfo.InvariantCulture)
{
    /// <summary>True when the written text is at least as long as the width.</summary>
    public bool Overflowed => GetStringBuilder().Length >= width;
}
