// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Errors;

namespace Nooa.Llm;

/// <summary>
/// The model output does not match the requested type. The message names the problem in words that
/// the caller can send back to the model so that it corrects the output.
/// </summary>
public sealed class StructuredOutputException : ValidationException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong with the output.</param>
    /// <param name="outputType">The type that was requested.</param>
    /// <param name="content">The text that failed to parse. Do not log it above Debug level.</param>
    /// <param name="innerException">The parse or conversion error.</param>
    public StructuredOutputException(string message, Type outputType, string content, Exception? innerException = null)
        : base(message, innerException!)
    {
        OutputType = outputType;
        Content = content;
    }

    /// <summary>The type that was requested.</summary>
    public Type OutputType { get; }

    /// <summary>The text that failed to parse.</summary>
    public string Content { get; }
}
