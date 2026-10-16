// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/errors/__init__.py

namespace Nooa.Errors;

/// <summary>
/// LLM output contains XML or HTML tags instead of plain code.
/// The message tells the model that tags are not supported, so it can correct its output format.
/// </summary>
public class XmlFormatException : ValidationException
{
    public XmlFormatException(string message)
        : base(message)
    {
    }
}
