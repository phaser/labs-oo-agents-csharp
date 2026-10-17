// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;

namespace Nooa.Tests.Tracing;

/// <summary>Test HTTP handler: records each request (body and authorization header) and answers with a fixed status.</summary>
public sealed class RecordingHttpHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public ConcurrentQueue<(Uri? Uri, string Body, string? Authorization, string? ContentType)> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Enqueue((request.RequestUri, body, request.Headers.Authorization?.ToString(), request.Content?.Headers.ContentType?.MediaType));
        return new HttpResponseMessage(status);
    }
}
