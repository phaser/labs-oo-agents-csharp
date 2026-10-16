// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: tests/unifiedllm/test_retry.py

using System.Net;
using Nooa.Llm;

namespace Nooa.Tests.Llm;

public class RetryPolicyTests
{
    private static HttpRequestException Status(HttpStatusCode code, string message = "request failed") =>
        new(message, null, code);

    private static RetryPolicy Policy(RetryOptions? options, List<TimeSpan>? delays = null, Func<double>? random = null)
    {
        Task Delay(TimeSpan span, CancellationToken token)
        {
            delays?.Add(span);
            return Task.CompletedTask;
        }

        return new RetryPolicy(options, Delay, random ?? (() => 0.0));
    }

    private static Func<CancellationToken, Task<string>> Script(List<int> calls, params object[] steps)
    {
        return _ =>
        {
            var step = steps[calls.Count];
            calls.Add(calls.Count);
            return step is Exception e ? Task.FromException<string>(e) : Task.FromResult((string)step);
        };
    }

    [Fact]
    public void CalculateDelay_Attempts_GrowExponentially()
    {
        var policy = Policy(new RetryOptions { JitterFactor = 0.0 });

        Assert.Equal(TimeSpan.FromSeconds(1), policy.CalculateDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(2), policy.CalculateDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.CalculateDelay(2));
    }

    [Fact]
    public void CalculateDelay_LargeAttempt_IsCappedAtMaxDelay()
    {
        var policy = Policy(new RetryOptions { MaxDelay = TimeSpan.FromSeconds(5), JitterFactor = 0.0 });

        Assert.Equal(TimeSpan.FromSeconds(5), policy.CalculateDelay(10));
    }

    [Fact]
    public void CalculateDelay_RateLimit_UsesLongerBase()
    {
        var policy = Policy(new RetryOptions { JitterFactor = 0.0 });

        Assert.Equal(TimeSpan.FromSeconds(1), policy.CalculateDelay(0, isRateLimit: false));
        Assert.Equal(TimeSpan.FromSeconds(3), policy.CalculateDelay(0, isRateLimit: true));
        Assert.Equal(TimeSpan.FromSeconds(9), policy.CalculateDelay(1, isRateLimit: true));
    }

    [Fact]
    public void CalculateDelay_Jitter_AddsUpToTheFactor()
    {
        var options = new RetryOptions { JitterFactor = 0.5 };

        Assert.Equal(TimeSpan.FromSeconds(1), Policy(options, random: () => 0.0).CalculateDelay(0));
        Assert.Equal(TimeSpan.FromSeconds(1.25), Policy(options, random: () => 0.5).CalculateDelay(0));
    }

    [Fact]
    public void CalculateDelay_DefaultRandom_StaysInJitterRange()
    {
        var policy = new RetryPolicy(new RetryOptions { JitterFactor = 0.5 });

        var delays = Enumerable.Range(0, 100).Select(_ => policy.CalculateDelay(0).TotalSeconds).ToList();

        Assert.All(delays, d => Assert.InRange(d, 1.0, 1.5));
        Assert.True(delays.Distinct().Count() > 1);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, true, false)]
    [InlineData(HttpStatusCode.TooManyRequests, true, true)]
    [InlineData(HttpStatusCode.InternalServerError, true, false)]
    [InlineData(HttpStatusCode.BadGateway, true, false)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true, false)]
    [InlineData(HttpStatusCode.GatewayTimeout, true, false)]
    [InlineData(HttpStatusCode.Unauthorized, false, false)]
    [InlineData(HttpStatusCode.BadRequest, false, false)]
    public void Classify_StructuredStatus_FollowsTheRetryableSet(HttpStatusCode code, bool retryable, bool rateLimit)
    {
        Assert.Equal((retryable, rateLimit), Policy(new RetryOptions()).Classify(Status(code)));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(418)]
    public void Classify_NonRetryableStatus_StaysTerminalDespiteTimeoutText(int code)
    {
        var error = Status((HttpStatusCode)code, "endpoint not reachable; request timed out");

        Assert.Equal((false, false), Policy(new RetryOptions()).Classify(error));
    }

    [Fact]
    public void Classify_ExcludedStatus_IsNotRetried()
    {
        var options = new RetryOptions { RetryableStatusCodes = new HashSet<int> { 429, 500, 502, 503, 504 } };

        Assert.Equal((false, false), Policy(options).Classify(Status(HttpStatusCode.RequestTimeout)));
    }

    [Fact]
    public void Classify_429Excluded_IsNotRetriedByStatusOrText()
    {
        var policy = Policy(new RetryOptions { RetryableStatusCodes = new HashSet<int> { 500, 502, 503, 504 } });

        Assert.Equal((false, false), policy.Classify(Status(HttpStatusCode.TooManyRequests)));
        Assert.Equal((false, false), policy.Classify(new Exception("API call failed with status 429: rate limit exceeded")));
    }

    [Fact]
    public void Classify_Status429InMessage_IsRateLimit()
    {
        Assert.Equal((true, true), Policy(new RetryOptions()).Classify(new Exception("API call failed with status 429: rate limit exceeded")));
    }

    [Fact]
    public void Classify_Status500InMessage_IsRetryableNotRateLimit()
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new Exception("API call failed with status 500: internal server error")));
    }

    [Theory]
    [InlineData("litellm.BadGatewayError: OpenAIException - 502 Bad Gateway")]
    [InlineData("Server returned 503 Service Unavailable")]
    [InlineData("Upstream 504 Gateway Timeout")]
    public void Classify_StatusPhraseWithoutStatusToken_IsRetryable(string message)
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new Exception(message)));
    }

    [Fact]
    public void Classify_PhraseForExcludedCode_IsNotRetryable()
    {
        var options = new RetryOptions { RetryableStatusCodes = new HashSet<int> { 429, 500 } };

        Assert.Equal((false, false), Policy(options).Classify(new Exception("OpenAIException - 502 Bad Gateway")));
    }

    [Fact]
    public void Classify_TimeoutException_IsRetryable()
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new TimeoutException()));
    }

    [Fact]
    public void Classify_TimeoutInMessage_IsRetryable()
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new Exception("Request timed out after 30s")));
    }

    [Fact]
    public void Classify_TimeoutInInnerException_IsRetryable()
    {
        var error = new TaskCanceledException("canceled", new TimeoutException());

        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(error));
    }

    [Theory]
    [InlineData("failed to connect")]
    [InlineData("server disconnected without sending a response")]
    [InlineData("network is unreachable")]
    [InlineData("RemoteDisconnected: remote end closed connection without response")]
    [InlineData("endpoint not reachable")]
    public void Classify_EndpointFailureText_IsRetryable(string message)
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new Exception(message)));
    }

    [Fact]
    public void Classify_HttpRequestExceptionWithoutStatus_IsRetryable()
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new HttpRequestException("failed")));
    }

    private sealed class APIConnectionError(string message) : Exception(message);

    [Fact]
    public void Classify_ConnectionErrorClassName_IsRetryable()
    {
        Assert.Equal((true, false), Policy(new RetryOptions()).Classify(new APIConnectionError("boom")));
    }

    [Theory]
    [InlineData("Invalid API key")]
    [InlineData("API call failed with status 400: bad request")]
    public void Classify_PermanentError_IsNotRetryable(string message)
    {
        Assert.Equal((false, false), Policy(new RetryOptions()).Classify(new Exception(message)));
    }

    [Fact]
    public void Classify_EmptyContent_RetriesOnlyWhenEnabled()
    {
        var error = new EmptyContentException("thinking");

        Assert.Equal((false, false), Policy(new RetryOptions()).Classify(error));
        Assert.Equal((true, false), Policy(new RetryOptions { RetryOnEmptyContent = true }).Classify(error));
    }

    [Fact]
    public async Task ExecuteAsync_Timeout408ThenSuccess_UsesNormalBackoff()
    {
        var delays = new List<TimeSpan>();
        var calls = new List<int>();
        var error = Status(HttpStatusCode.RequestTimeout);

        var result = await Policy(new RetryOptions { JitterFactor = 0.0 }, delays)
            .ExecuteAsync(Script(calls, error, error, "success"), TestContext.Current.CancellationToken);

        Assert.Equal("success", result);
        Assert.Equal(3, calls.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], delays);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout408Forever_ExhaustsNormalBudgetAndRethrows()
    {
        var delays = new List<TimeSpan>();
        var calls = new List<int>();
        var error = Status(HttpStatusCode.RequestTimeout);

        var caught = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Policy(new RetryOptions { JitterFactor = 0.0 }, delays)
                .ExecuteAsync(Script(calls, error, error, error, error), TestContext.Current.CancellationToken));

        Assert.Same(error, caught);
        Assert.Equal(4, calls.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], delays);
    }

    [Fact]
    public async Task ExecuteAsync_Success_DoesNotRetry()
    {
        var calls = new List<int>();

        var result = await Policy(null).ExecuteAsync(Script(calls, "ok"), TestContext.Current.CancellationToken);

        Assert.Equal("ok", result);
        Assert.Single(calls);
    }

    [Fact]
    public async Task ExecuteAsync_EndpointDisconnectThenSuccess_Retries()
    {
        var calls = new List<int>();
        var error = new Exception("RemoteDisconnected: remote end closed connection without response");

        var result = await Policy(null).ExecuteAsync(Script(calls, error, "success"), TestContext.Current.CancellationToken);

        Assert.Equal("success", result);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task ExecuteAsync_MaxRetriesExhausted_Rethrows()
    {
        var calls = new List<int>();
        var error = new Exception("API call failed with status 500");

        await Assert.ThrowsAsync<Exception>(() =>
            Policy(new RetryOptions { MaxRetries = 2 }).ExecuteAsync(Script(calls, error, error, error), TestContext.Current.CancellationToken));

        Assert.Equal(3, calls.Count);
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryableError_IsNotRetried()
    {
        var calls = new List<int>();

        await Assert.ThrowsAsync<Exception>(() =>
            Policy(new RetryOptions { MaxRetries = 3 }).ExecuteAsync(Script(calls, new Exception("Invalid API key")), TestContext.Current.CancellationToken));

        Assert.Single(calls);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellation_PropagatesWithoutRetry()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Policy(new RetryOptions { MaxRetries = 2 }).ExecuteAsync<string>(
                token =>
                {
                    calls++;
                    cts.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult("never");
                },
                cts.Token));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_RateLimit_GetsExtraRetries()
    {
        var delays = new List<TimeSpan>();
        var calls = new List<int>();
        var error = new Exception("API call failed with status 429");
        var options = new RetryOptions { MaxRetries = 1, RateLimitExtraRetries = 3, JitterFactor = 0.0 };

        var result = await Policy(options, delays).ExecuteAsync(Script(calls, error, error, error, error, "success"), TestContext.Current.CancellationToken);

        Assert.Equal("success", result);
        Assert.Equal(5, calls.Count);
        Assert.Equal(
            [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(27), TimeSpan.FromSeconds(60)],
            delays);
    }

    [Fact]
    public async Task ExecuteAsync_RetryCallback_ReceivesAttemptErrorAndDelay()
    {
        var seen = new List<(int Attempt, Exception Error, TimeSpan Delay)>();
        var error = new Exception("API call failed with status 500");
        var options = new RetryOptions { OnRetry = (a, e, d) => seen.Add((a, e, d)) };

        await Policy(options).ExecuteAsync(Script([], error, "success"), TestContext.Current.CancellationToken);

        var call = Assert.Single(seen);
        Assert.Equal(1, call.Attempt);
        Assert.Same(error, call.Error);
        Assert.True(call.Delay > TimeSpan.Zero);
    }

    [Fact]
    public void Constructor_NegativeRetries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(new RetryOptions { MaxRetries = -1 }));
    }
}
