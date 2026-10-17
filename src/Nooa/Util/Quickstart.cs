// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/util/quickstart.py

using Microsoft.Extensions.AI;
using Nooa.Llm;

namespace Nooa.Util;

/// <summary>
/// Picks a reasonable default LLM from the credentials in the environment, for the quickstart examples.
/// </summary>
/// <remarks>
/// <para>Order of choice, first credential found wins:</para>
/// <list type="number">
/// <item><c>NVIDIA_API_KEY</c>: NVIDIA NIM at <c>integrate.api.nvidia.com</c>.</item>
/// <item><c>OPENAI_API_KEY</c>: OpenAI.</item>
/// <item><c>NVIDIA_INFERENCE_API_KEY</c> or <c>NVIDIA_INTERNAL_API_KEY</c>: the NVIDIA inference gateway.</item>
/// <item>None: OpenAI, so the example fails with a clear missing-<c>OPENAI_API_KEY</c> error.</item>
/// </list>
/// <para>
/// Upstream also loads a <c>.env</c> file with override. That is not ported: a <c>.env</c> file in the working directory
/// is untrusted and would override the shell. Call <see cref="Secrets.LoadSecretsIntoEnv"/> before this helper to load
/// <c>secrets.yaml</c> under the trust rules.
/// </para>
/// </remarks>
public static class Quickstart
{
    /// <summary>The model that is used when no credential is set.</summary>
    public const string DefaultModel = "gpt-5-mini";

    private const string _nimModel = "nvidia_nim/nvidia/nemotron-3-super-120b-a12b";
    private const string _gatewayModel = "openai/azure/openai/gpt-5-mini";
    private const string _gatewayBase = "https://inference-api.nvidia.com/v1";

    /// <summary>
    /// Creates the default client and returns it with the model string it uses.
    /// </summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable. Null reads the process environment. Tests pass a stub.</param>
    /// <param name="chatClientFactory">Builds the chat client from the wire model name, the base URL (or null) and the key. Null builds the OpenAI client.</param>
    /// <exception cref="Errors.ConfigValidationException">No credential is set, so the default OpenAI route has no key.</exception>
    public static (string Model, ILlmClient Client) CreateDefaultLlm(
        Func<string, string?>? getEnvironmentVariable = null,
        Func<string, string?, string, IChatClient>? chatClientFactory = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        string? Read(string name) => getEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

        string model;
        string? apiBase = null;
        string? key;
        if ((key = Read("NVIDIA_API_KEY")) is not null)
        {
            model = _nimModel;
        }
        else if ((key = Read("OPENAI_API_KEY")) is not null)
        {
            model = DefaultModel;
        }
        else if ((key = Read("NVIDIA_INFERENCE_API_KEY") ?? Read("NVIDIA_INTERNAL_API_KEY")) is not null)
        {
            model = _gatewayModel;
            apiBase = _gatewayBase;
        }
        else
        {
            throw new Errors.ConfigValidationException(
                "No LLM credential found. Set NVIDIA_API_KEY, OPENAI_API_KEY or NVIDIA_INFERENCE_API_KEY.");
        }

        var (wireModel, defaultBase) = OpenAiChatClientFactory.ParseModel(model);
        apiBase ??= defaultBase;
        var chat = (chatClientFactory ?? ((m, b, k) => OpenAiChatClientFactory.Create(m, b, k)))(wireModel, apiBase, key);
        return (model, new ChatClientLlmClient(chat, new ChatClientLlmOptions { Model = wireModel }));
    }
}
