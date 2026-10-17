// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py

namespace Nooa.Examples.Quickstart.Example05;

/// <summary>A piece of art with an appraisal.</summary>
public sealed class Artwork(string title, string artist, double appraisedValue)
{
    /// <summary>The title of the work.</summary>
    public string Title { get; } = title;

    /// <summary>The artist.</summary>
    public string Artist { get; } = artist;

    /// <summary>Get the appraisal details including current market value.</summary>
    public Appraisal GetAppraisal() => new(Title, Artist, appraisedValue, "USD");
}
