using System.Net;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Finnova.Service.IdentityVerification.External;

public sealed class ProviderCallExecutor : IProviderCallExecutor
{
    private readonly ILogger<ProviderCallExecutor> _logger;
    private readonly IdentityVerificationOptions _options;

    public ProviderCallExecutor(
        ILogger<ProviderCallExecutor> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<IdentityVerificationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Executes a provider call with a per-attempt timeout and retry handling.
    /// Retries only transient transport failures, HTTP 408/429, and HTTP 5xx responses
    /// represented by the provider call as exceptions.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> execFunc,
        VerificationOperationType operationType,
        string correlationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(execFunc);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        }

        var operationName = GetOperationName(operationType);
        var retry = _options.Retry ?? new RetryOptions();
        var maxAttempts = Math.Max(1, retry.MaxAttempts);
        var timeoutSeconds = Math.Max(1, retry.TimeoutSeconds);
        var baseDelayMilliseconds = Math.Max(0, retry.BaseDelayMilliseconds);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ct,
                timeoutCts.Token);

            _logger.LogInformation(
                "Provider call attempt {Attempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                attempt,
                maxAttempts,
                operationName,
                correlationId);

            try
            {
                return await execFunc(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation(
                    ex,
                    "Provider call canceled by the caller on attempt {Attempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                    attempt,
                    maxAttempts,
                    operationName,
                    correlationId);
                throw;
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Provider call timed out after {TimeoutSeconds}s on attempt {Attempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                    timeoutSeconds,
                    attempt,
                    maxAttempts,
                    operationName,
                    correlationId);

                if (attempt == maxAttempts)
                {
                    throw new IdentityVerificationProviderUnavailableException();
                }
            }
            catch (HttpRequestException ex) when (IsTransientHttpFailure(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Transient provider transport failure on attempt {Attempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                    attempt,
                    maxAttempts,
                    operationName,
                    correlationId);

                if (attempt == maxAttempts)
                {
                    throw new IdentityVerificationProviderUnavailableException();
                }
            }
            catch (WebException ex) when (IsTransientWebFailure(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Transient provider transport failure on attempt {Attempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                    attempt,
                    maxAttempts,
                    operationName,
                    correlationId);

                if (attempt == maxAttempts)
                {
                    throw new IdentityVerificationProviderUnavailableException();
                }
            }

            var delayMilliseconds = CalculateBackoffDelay(attempt, baseDelayMilliseconds);
            _logger.LogInformation(
                "Waiting {DelayMs}ms before retry {RetryAttempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                delayMilliseconds,
                attempt + 1,
                maxAttempts,
                operationName,
                correlationId);
            await Task.Delay(delayMilliseconds, ct).ConfigureAwait(false);
        }

        throw new IdentityVerificationProviderUnavailableException();
    }

    private static string GetOperationName(VerificationOperationType operationType) => operationType switch
    {
        VerificationOperationType.PanIdentifierValidation => "PAN identifier validation",
        VerificationOperationType.AadhaarIdentifierValidation => "Aadhaar identifier validation",
        VerificationOperationType.PanNameVerification => "PAN name comparison",
        VerificationOperationType.AadhaarAddressVerification => "Aadhaar address comparison",
        _ => throw new ArgumentOutOfRangeException(nameof(operationType), operationType, null)
    };

    private static bool IsTransientHttpFailure(HttpRequestException exception)
    {
        return exception.StatusCode is null
            || IsTransientHttpStatusCode(exception.StatusCode.Value);
    }

    private static bool IsTransientWebFailure(WebException exception)
    {
        if (exception.Response is HttpWebResponse response)
        {
            return IsTransientHttpStatusCode(response.StatusCode);
        }

        return exception.Status
            is WebExceptionStatus.ConnectFailure
            or WebExceptionStatus.ConnectionClosed
            or WebExceptionStatus.KeepAliveFailure
            or WebExceptionStatus.NameResolutionFailure
            or WebExceptionStatus.ReceiveFailure
            or WebExceptionStatus.SendFailure
            or WebExceptionStatus.Timeout;
    }

    private static bool IsTransientHttpStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode is >= 500 and <= 599;
    }

    private static int CalculateBackoffDelay(int attempt, int baseDelayMilliseconds)
    {
        var exponent = Math.Min(Math.Max(attempt - 1, 0), 10);
        var exponentialDelay = Math.Min(
            30_000L,
            (long)baseDelayMilliseconds * (1L << exponent));
        var jitter = Random.Shared.NextInt64(
            0,
            Math.Max(1L, exponentialDelay / 3 + 1));

        return (int)Math.Min(30_000L, exponentialDelay + jitter);
    }
}
