// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/event_backend.py

using System.Globalization;

namespace Nooa.Runtime;

internal static class EventTags
{
    /// <summary>
    /// The highest number in a tag. <c>5</c> gives 5, the range <c>2..40</c> gives 40, and a tag that is not a number gives 0.
    /// </summary>
    public static long MaxNum(string tag)
    {
        var text = tag.Contains("..", StringComparison.Ordinal) ? tag.Split("..")[1] : tag;
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
