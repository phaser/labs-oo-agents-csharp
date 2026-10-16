// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/storage/markers.py

namespace Nooa;

/// <summary>
/// Opts a class into state snapshots. A record or a struct is eligible without this marker.
/// An ordinary class is not eligible unless it carries this attribute. The snapshot writes and reads its
/// public properties through <c>System.Text.Json</c>. It never reflects over other classes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SnapshotableAttribute : Attribute
{
}
