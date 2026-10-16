// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Storage;

/// <summary>Identity and creation time of a stored snapshot. <see cref="CreatedAt"/> is an ISO 8601 string.</summary>
public sealed record SnapshotInfo(string SnapshotId, string CreatedAt);
