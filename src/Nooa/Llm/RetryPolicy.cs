// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/unifiedllm/retry.py

using System.ClientModel;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Nooa.Llm;

/// <summary>
/// Runs an async operation with exponential backoff and jitter. Rate limits (HTTP 429) get extra retries
/// and a longer delay. A caller cancellation is never retried.
/// </summary>
public sealed class RetryPolicy
{
    private static readonly IReadOnlyDictionary<int, string[]> _statusPhrases = new Dictionary<int, string[]>
    {
        [500] = ["internal server error", "internalservererror"],
        [502] = ["bad gateway", "badgatewayerror"],
        [503] = ["service unavailable", "serviceunavailableerror", "server unavailable"],
        [504] = ["gateway timeout", "gateway time-out"],
    };

    private static readonly HashSet<string> _endpointClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "APIConnectionError", "APITimeoutError", "ConnectError", "ConnectTimeout", "ConnectionError",
        "ConnectionTimeoutError", "NetworkError", "ReadTimeout", "RemoteProtocolError", "ServerDisconnectedError",
        "Timeout", "TimeoutError", "TransportError", "WriteError", "WriteTimeout",
    };

    private static readonly string[] _endpointPhrases =
    [
        "api connection error", "cannot connect", "can't connect", "connection aborted", "connection closed",
        "connection reset", "connection refused", "connection terminated", "connection timed out",
        "endpoint not reachable", "failed to connect", "max retries exceeded", "name resolution",
        "network is unreachable", "not reachable", "nodename nor servname", "remote disconnected",
        "remote end closed", "server disconnected", "temporary failure in name resolution", "timed out",
        "timeout", "transport error",
    ];

    private static readonly string[] _connectionFailures =
    [
        "abort", "closed", "disconnect", "failed", "lost", "reset", "refused", "terminated", "timed out",
        "unreachable",
    ];

    private readonly RetryOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<double> _random;
    private readonly ILogger? _logger;

    /// <summary>Creates a policy.</summary>
    /// <param name="options">The retry settings. Null uses the defaults.</param>
    /// <param name="delay">Waits between attempts. Tests pass a function that returns at once.</param>
    /// <param name="random">Returns a number in [0, 1) for the jitter. Tests pass a fixed value.</param>
    /// <param name="logger">Receives one warning for each retry. It never receives message text.</param>
    public RetryPolicy(
        RetryOptions? options = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<double>? random = null,
        ILogger? logger = null)
    {
        _options = options ?? new RetryOptions();
        _delay = delay ?? Task.Delay;
        _random = random ?? Random.Shared.NextDouble;
        _logger = logger;
        if (_options.MaxRetries < 0 || _options.RateLimitExtraRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Retry counts cannot be negative");
        }
    }

    /// <summary>True when an empty answer with reasoning counts as a retryable failure.</summary>
    public bool RetryOnEmptyContent => _options.RetryOnEmptyContent;

    /// <summary>
    /// Calculates the wait before a retry: base times factor to the power of the attempt, capped at the
    /// maximum, then increased by up to the jitter fraction.
    /// </summary>
    /// <param name="attempt">The zero-based attempt that failed.</param>
    /// <param name="isRateLimit">True to use the rate-limit base and factor.</param>
    public TimeSpan CalculateDelay(int attempt, bool isRateLimit = false)
    {
        var baseDelay = isRateLimit ? _options.RateLimitBaseDelay : _options.BaseDelay;
        var factor = isRateLimit ? _options.RateLimitBackoffBase : _options.ExponentialBase;
        var seconds = Math.Min(baseDelay.TotalSeconds * Math.Pow(factor, attempt), _options.MaxDelay.TotalSeconds);
        seconds += seconds * _options.JitterFactor * _random();
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Decides whether an error is worth a retry, and whether it is a rate limit.</summary>
    public (bool Retryable, bool RateLimit) Classify(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var codes = _options.RetryableStatusCodes;

        if (error is EmptyContentException && _options.RetryOnEmptyContent)
        {
            return (true, false);
        }

        // A structured status is authoritative: a permanent 4xx must never become a retry
        // because the message text happens to mention a timeout.
        if (StatusOf(error) is { } status)
        {
            if (status == 429)
            {
                return codes.Contains(429) ? (true, true) : (false, false);
            }

            return codes.Contains(status) ? (true, false) : (false, false);
        }

        var message = error.Message.ToLowerInvariant();

        if (message.Contains("status 429", StringComparison.Ordinal) || message.Contains("rate limit", StringComparison.Ordinal))
        {
            return codes.Contains(429) ? (true, true) : (false, false);
        }

        foreach (var code in codes)
        {
            if (message.Contains($"status {code}", StringComparison.Ordinal)
                || (_statusPhrases.TryGetValue(code, out var phrases) && phrases.Any(message.Contains)))
            {
                return (true, false);
            }
        }

        if (Chain(error).Any(e => e is TimeoutException or IOException or SocketException or HttpRequestException))
        {
            return (true, false);
        }

        return IsEndpointError(error, message) ? (true, false) : (false, false);
    }

    /// <summary>
    /// Runs <paramref name="operation"/> and retries it as the options allow. The last error is rethrown
    /// when the retries are used up or the error is not retryable.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var attempt = 0;
        while (true)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                var (retryable, rateLimit) = Classify(e);
                var allowed = _options.MaxRetries + (rateLimit ? _options.RateLimitExtraRetries : 0);
                if (!retryable || attempt >= allowed)
                {
                    throw;
                }

                var delay = CalculateDelay(attempt, rateLimit);
                if (_logger is not null)
                {
                    _logRetry(_logger, attempt + 1, allowed + 1, e.GetType().Name, delay.TotalSeconds, null);
                }

                _options.OnRetry?.Invoke(attempt + 1, e, delay);
                await _delay(delay, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }
    }

    private static readonly Action<ILogger, int, int, string, double, Exception?> _logRetry =
        LoggerMessage.Define<int, int, string, double>(
            LogLevel.Warning,
            new EventId(1, "LlmRetry"),
            "LLM call retry {Attempt}/{Total} after {ErrorType}. Waiting {Seconds:F1}s");

    private static IEnumerable<Exception> Chain(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
        {
            yield return e;
            if (e is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Chain))
                {
                    yield return inner;
                }
            }
        }
    }

    private static int? StatusOf(Exception error)
    {
        foreach (var e in Chain(error))
        {
            switch (e)
            {
                case ClientResultException { Status: > 0 } client:
                    return client.Status;
                case HttpRequestException { StatusCode: { } code }:
                    return (int)code;
            }
        }

        return null;
    }

    private static bool IsEndpointError(Exception error, string message)
    {
        for (var type = error.GetType(); type is not null; type = type.BaseType)
        {
            if (_endpointClassNames.Contains(type.Name))
            {
                return true;
            }
        }

        if (_endpointPhrases.Any(message.Contains))
        {
            return true;
        }

        return message.Contains("connection", StringComparison.Ordinal) && _connectionFailures.Any(message.Contains);
    }
}
