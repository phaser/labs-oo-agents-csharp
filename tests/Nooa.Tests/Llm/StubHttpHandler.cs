// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;

namespace Nooa.Tests.Llm;

/// <summary>Answers every request with one canned JSON body and keeps the request bodies. No socket is opened.</summary>
internal sealed class StubHttpHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        };
    }
}
