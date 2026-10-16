// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Nooa.Llm;

/// <summary>Builds the Microsoft.Extensions.AI client for an OpenAI-compatible endpoint.</summary>
internal static class OpenAiChatClientFactory
{
    private const string _nvidiaNimBase = "https://integrate.api.nvidia.com/v1";

    // Upstream model names are litellm routing strings. These prefixes only select a route and are not part of the wire model name.
    private static readonly string[] _routePrefixes = ["openai/", "hosted_vllm/", "nvidia_nim/"];

    /// <summary>
    /// Splits a litellm-style model string into the wire model name and the default base URL of its route.
    /// Any other prefix stays in the name, because an open model can have a slash in its real name.
    /// </summary>
    public static (string Model, string? DefaultApiBase) ParseModel(string model)
    {
        foreach (var prefix in _routePrefixes)
        {
            if (model.StartsWith(prefix, StringComparison.Ordinal))
            {
                return (model[prefix.Length..], prefix == "nvidia_nim/" ? _nvidiaNimBase : null);
            }
        }

        return (model, null);
    }

    /// <summary>Creates the chat client. <paramref name="apiBase"/> null uses the default OpenAI endpoint.</summary>
    public static IChatClient Create(string model, string? apiBase, string apiKey, OpenAIClientOptions? options = null)
    {
        options ??= new OpenAIClientOptions();
        if (apiBase is not null)
        {
            options.Endpoint = new Uri(apiBase);
        }

        return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model).AsIChatClient();
    }
}
