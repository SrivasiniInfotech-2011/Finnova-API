using System.Net;
using System.Net.Http.Json;
using Finnova.Models.Domain.Enums;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.IdentityVerification.External;

/// <summary>
/// Configured HTTP adapter for PAN and Aadhaar identifier validation.
/// Provider-specific request fields are supplied through <see cref="ProviderOperationOptions"/>;
/// provider response bodies are intentionally not exposed or logged.
/// </summary>
public sealed class ConfiguredIdentifierAdapter : IIdentifierValidationAdapter
{
    private static readonly HttpClient LegacyHttpClient = new();

    private readonly HttpClient _httpClient;
    private readonly IProviderCallExecutor? _providerCallExecutor;
    private readonly ProviderOperationOptions _providerOptions;
    private readonly int _timeoutSeconds;
    private readonly int _maxRetryAttempts;
    private readonly int _baseDelayMilliseconds;
    private readonly bool _useProviderCallExecutor;

    public VerificationOperationType SupportedOperation { get; }

    /// <summary>
    /// Compatibility constructor used by the current SystemAdminService registration.
    /// New registrations should prefer the HttpClientFactory/executor constructor below.
    /// </summary>
    public ConfiguredIdentifierAdapter(
        VerificationOperationType operationType,
        string baseAddress,
        string path,
        string httpMethod = "POST",
        string requestMode = "Json",
        string identifierFieldName = "",
        int timeoutSeconds = 10,
        int maxRetryAttempts = 3,
        int baseDelayMilliseconds = 200)
        : this(
            operationType,
            new ProviderOperationOptions
            {
                Enabled = true,
                BaseAddress = baseAddress,
                Path = path,
                HttpMethod = httpMethod,
                RequestMode = requestMode,
                IdentifierFieldName = identifierFieldName
            },
            LegacyHttpClient,
            providerCallExecutor: null,
            timeoutSeconds,
            maxRetryAttempts,
            baseDelayMilliseconds)
    {
    }

    /// <summary>
    /// Creates an adapter using the shared HttpClientFactory and provider-call executor.
    /// The executor owns retry and timeout policy for this path.
    /// </summary>
    public ConfiguredIdentifierAdapter(
        VerificationOperationType operationType,
        ProviderOperationOptions providerOptions,
        IHttpClientFactory httpClientFactory,
        IProviderCallExecutor providerCallExecutor,
        RetryOptions? retryOptions = null)
        : this(
            operationType,
            providerOptions,
            (httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory))).CreateClient(),
            providerCallExecutor ?? throw new ArgumentNullException(nameof(providerCallExecutor)),
            retryOptions?.TimeoutSeconds ?? 10,
            retryOptions?.MaxAttempts ?? 3,
            retryOptions?.BaseDelayMilliseconds ?? 200)
    {
    }

    /// <summary>
    /// Creates an adapter around an HttpClient, optionally using the shared provider executor.
    /// This overload is useful for named or typed HttpClientFactory registrations.
    /// </summary>
    public ConfiguredIdentifierAdapter(
        VerificationOperationType operationType,
        ProviderOperationOptions providerOptions,
        HttpClient httpClient,
        IProviderCallExecutor? providerCallExecutor = null,
        RetryOptions? retryOptions = null)
        : this(
            operationType,
            providerOptions,
            httpClient,
            providerCallExecutor,
            retryOptions?.TimeoutSeconds ?? 10,
            retryOptions?.MaxAttempts ?? 3,
            retryOptions?.BaseDelayMilliseconds ?? 200)
    {
    }

    private ConfiguredIdentifierAdapter(
        VerificationOperationType operationType,
        ProviderOperationOptions providerOptions,
        HttpClient httpClient,
        IProviderCallExecutor? providerCallExecutor,
        int timeoutSeconds,
        int maxRetryAttempts,
        int baseDelayMilliseconds)
    {
        SupportedOperation = operationType;
        _providerOptions = providerOptions ?? throw new ArgumentNullException(nameof(providerOptions));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _providerCallExecutor = providerCallExecutor;
        _useProviderCallExecutor = providerCallExecutor is not null;
        _timeoutSeconds = Math.Max(1, timeoutSeconds);
        _maxRetryAttempts = Math.Max(1, maxRetryAttempts);
        _baseDelayMilliseconds = Math.Max(0, baseDelayMilliseconds);
    }

    public async Task<IdentifierAdapterResult> ValidateAsync(
        IdentifierValidationInput input,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.OperationType != SupportedOperation)
        {
            return new IdentifierAdapterResult.Unavailable(
                "OPERATION_MISMATCH",
                "The configured identifier operation does not match the request.");
        }

        if (string.IsNullOrWhiteSpace(input.Identifier))
        {
            return new IdentifierAdapterResult.Failure(
                "IDENTIFIER_EMPTY",
                "The identifier to validate is empty.");
        }

        if (!TryCreateProviderUri(out var providerUri))
        {
            return new IdentifierAdapterResult.Unavailable(
                "CONFIGURATION_ERROR",
                "The identifier provider is not configured.");
        }

        if (!IsRequestConfigurationValid())
        {
            return new IdentifierAdapterResult.Unavailable(
                "CONFIGURATION_ERROR",
                "The identifier provider request is not configured.");
        }

        try
        {
            if (_useProviderCallExecutor)
            {
                return await _providerCallExecutor!.ExecuteAsync(
                    ct => SendOnceAsync(providerUri, input, ct),
                    SupportedOperation,
                    input.CorrelationId,
                    token).ConfigureAwait(false);
            }

            return await SendWithRetryAsync(providerUri, input, token)
                .ConfigureAwait(false);
        }
        catch (IdentityVerificationProviderUnavailableException)
        {
            return new IdentifierAdapterResult.Unavailable(
                "PROVIDER_UNAVAILABLE",
                "The identifier provider is temporarily unavailable.");
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            return new IdentifierAdapterResult.Unavailable(
                "TIMEOUT",
                "The identifier provider call timed out.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return new IdentifierAdapterResult.Unavailable(
                "TIMEOUT",
                "The identifier provider call timed out.");
        }
        catch (HttpRequestException)
        {
            return new IdentifierAdapterResult.Unavailable(
                "PROVIDER_UNAVAILABLE",
                "The identifier provider is temporarily unavailable.");
        }
    }

    private bool TryCreateProviderUri(out Uri providerUri)
    {
        providerUri = default!;
        if (!_providerOptions.Enabled
            || string.IsNullOrWhiteSpace(_providerOptions.BaseAddress)
            || string.IsNullOrWhiteSpace(_providerOptions.Path)
            || string.IsNullOrWhiteSpace(_providerOptions.IdentifierFieldName))
        {
            return false;
        }

        var address = $"{_providerOptions.BaseAddress.TrimEnd('/')}/{_providerOptions.Path.TrimStart('/')}";
        return Uri.TryCreate(address, UriKind.Absolute, out providerUri)
            && (providerUri.Scheme == Uri.UriSchemeHttp || providerUri.Scheme == Uri.UriSchemeHttps)
            && TryCreateHttpMethod(_providerOptions.HttpMethod, out _);
    }

    private bool IsRequestConfigurationValid()
    {
        return string.Equals(_providerOptions.RequestMode, "Json", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_providerOptions.IdentifierFieldName);
    }

    private HttpContent CreateRequestContent(string identifier)
    {
        return JsonContent.Create(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [_providerOptions.IdentifierFieldName] = identifier
        });
    }

    private async Task<IdentifierAdapterResult> SendWithRetryAsync(
        Uri providerUri,
        IdentifierValidationInput input,
        CancellationToken token)
    {
        for (var attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
                return await SendOnceAsync(providerUri, input, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException) when (!token.IsCancellationRequested && attempt < _maxRetryAttempts)
            {
                await DelayBeforeRetryAsync(attempt, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < _maxRetryAttempts)
            {
                await DelayBeforeRetryAsync(attempt, token).ConfigureAwait(false);
            }
        }

        return new IdentifierAdapterResult.Unavailable(
            "PROVIDER_UNAVAILABLE",
            "The identifier provider is temporarily unavailable.");
    }

    private async Task<IdentifierAdapterResult> SendOnceAsync(
        Uri providerUri,
        IdentifierValidationInput input,
        CancellationToken token)
    {
        using var request = new HttpRequestMessage(
            CreateHttpMethod(_providerOptions.HttpMethod),
            providerUri)
        {
            Content = CreateRequestContent(input.Identifier)
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", input.CorrelationId);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            token).ConfigureAwait(false);

        if (IsTransientStatusCode(response.StatusCode))
        {
            throw new HttpRequestException(
                "The identifier provider returned a transient status.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return MapStatus(response.StatusCode);
    }

    private static IdentifierAdapterResult MapStatus(HttpStatusCode statusCode)
    {
        if ((int)statusCode is >= 200 and <= 299)
        {
            return new IdentifierAdapterResult.Success(
                safeMessage: "Identifier validation completed successfully.");
        }

        return statusCode switch
        {
            HttpStatusCode.NotFound => new IdentifierAdapterResult.Failure(
                "IDENTIFIER_NOT_FOUND",
                "The identifier was not found by the provider."),
            HttpStatusCode.BadRequest or
            HttpStatusCode.UnprocessableEntity => new IdentifierAdapterResult.Failure(
                "PROVIDER_REJECTED",
                "The provider rejected the identifier validation request."),
            HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden => new IdentifierAdapterResult.Failure(
                "PROVIDER_AUTHENTICATION_FAILED",
                "The identifier provider rejected the configured credentials."),
            _ when (int)statusCode is >= 400 and <= 499 => new IdentifierAdapterResult.Failure(
                "PROVIDER_REJECTED",
                "The provider rejected the identifier validation request."),
            _ => new IdentifierAdapterResult.Unavailable(
                "PROVIDER_UNAVAILABLE",
                "The identifier provider is temporarily unavailable.")
        };
    }

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken token)
    {
        var exponent = Math.Min(attempt - 1, 10);
        var exponentialDelay = _baseDelayMilliseconds * (1 << exponent);
        var jitter = Random.Shared.Next(0, Math.Max(1, exponentialDelay / 3 + 1));
        await Task.Delay(TimeSpan.FromMilliseconds(exponentialDelay + jitter), token)
            .ConfigureAwait(false);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode >= 500;
    }

    private static bool TryCreateHttpMethod(string? value, out HttpMethod method)
    {
        method = default!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            method = new HttpMethod(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HttpMethod CreateHttpMethod(string? value)
    {
        return TryCreateHttpMethod(value, out var method)
            ? method
            : throw new InvalidOperationException("The configured provider HTTP method is invalid.");
    }
}
