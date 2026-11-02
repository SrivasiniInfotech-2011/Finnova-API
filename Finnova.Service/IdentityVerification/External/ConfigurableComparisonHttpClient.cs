using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Finnova.Models.Domain.Enums;
using Finnova.Service.IdentityVerification.Adapters;
using Microsoft.Extensions.Logging;

namespace Finnova.Service.IdentityVerification.External;

/// <summary>
/// Sends comparison requests to a configured provider and maps only configured,
/// provider-neutral response values to comparison outcomes.
/// </summary>
public sealed class ConfigurableComparisonHttpClient : IComparisonAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _baseAddress;
    private readonly string _path;
    private readonly string _httpMethod;
    private readonly string _requestMode;
    private readonly int _timeoutSeconds;
    private readonly int _maxRetryAttempts;
    private readonly int _baseDelayMilliseconds;
    private readonly ComparisonProviderOptions _providerOptions;
    private readonly string? _responseOutcomePath;
    private readonly string? _responseMessagePath;
    private readonly ILogger<ConfigurableComparisonHttpClient> _logger;

    public VerificationOperationType SupportedOperation { get; }

    /// <summary>
    /// Creates a client using the supplied HttpClient. The HttpClient lifetime is owned by
    /// the caller or HttpClientFactory and is not disposed by this adapter.
    /// </summary>
    public ConfigurableComparisonHttpClient(
        HttpClient httpClient,
        VerificationOperationType operationType,
        ComparisonProviderOptions providerOptions,
        ILogger<ConfigurableComparisonHttpClient>? logger = null)
        : this(httpClient, operationType, providerOptions, new RetryOptions(), logger)
    {
    }

    /// <summary>
    /// Creates the adapter from the operation-specific deployment configuration.
    /// </summary>
    public ConfigurableComparisonHttpClient(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<IdentityVerificationOptions> options,
        VerificationOperationType operationType,
        ILogger<ConfigurableComparisonHttpClient>? logger = null)
        : this(
            httpClient,
            operationType,
            SelectProviderOptions(options?.Value ?? throw new ArgumentNullException(nameof(options)), operationType),
            options.Value.Retry ?? new RetryOptions(),
            logger)
    {
    }

    public ConfigurableComparisonHttpClient(
        HttpClient httpClient,
        VerificationOperationType operationType,
        ComparisonProviderOptions providerOptions,
        RetryOptions retryOptions,
        ILogger<ConfigurableComparisonHttpClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _providerOptions = providerOptions ?? throw new ArgumentNullException(nameof(providerOptions));
        SupportedOperation = operationType;
        _baseAddress = providerOptions.BaseAddress?.TrimEnd('/') ?? string.Empty;
        _path = providerOptions.Path?.TrimStart('/') ?? string.Empty;
        _httpMethod = string.IsNullOrWhiteSpace(providerOptions.HttpMethod) ? "POST" : providerOptions.HttpMethod;
        _requestMode = providerOptions.RequestMode;
        _timeoutSeconds = retryOptions.TimeoutSeconds;
        _maxRetryAttempts = retryOptions.MaxAttempts;
        _baseDelayMilliseconds = retryOptions.BaseDelayMilliseconds;
        _responseOutcomePath = providerOptions.ResponseOutcomePath;
        _responseMessagePath = providerOptions.ResponseMessagePath;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigurableComparisonHttpClient>.Instance;
    }

    private static ComparisonProviderOptions SelectProviderOptions(
        IdentityVerificationOptions options,
        VerificationOperationType operationType)
    {
        return operationType switch
        {
            VerificationOperationType.PanNameVerification => options.PanNameVerification,
            VerificationOperationType.AadhaarAddressVerification => options.AadhaarAddressVerification,
            _ => throw new ArgumentOutOfRangeException(nameof(operationType), operationType, "Only comparison operations are supported.")
        };
    }

    public async Task<ComparisonAdapterResult> VerifyAsync(
        ProviderNeutralComparisonInput input,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.SubmittedValue))
        {
            return new ComparisonAdapterResult.NoUsableMatch(
                "SUBMITTED_VALUE_EMPTY",
                "The submitted value to compare is empty.");
        }

        if (input.OperationType != SupportedOperation)
        {
            return new ComparisonAdapterResult.NoUsableMatch(
                "OPERATION_MISMATCH",
                "The configured comparison operation does not match the request.");
        }

        if (!TryCreateProviderUri(out var providerUri))
        {
            return new ComparisonAdapterResult.Unavailable(
                "CONFIGURATION_ERROR",
                "The comparison provider is not configured.");
        }

        if (!TryValidateInput(input, out var validationResult))
        {
            return validationResult!;
        }

        byte[]? documentBytes;
        try
        {
            documentBytes = await ReadDocumentBytesAsync(input.ProtectedDocumentStream, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to prepare the configured document for {Operation} (CorrelationId: {CorrelationId})",
                SupportedOperation,
                input.CorrelationId);
            return new ComparisonAdapterResult.NoUsableMatch(
                "DOCUMENT_READ_ERROR",
                "The comparison document could not be read.");
        }

        try
        {
            using var response = await SendWithRetryAsync(
                providerUri,
                input,
                documentBytes,
                token).ConfigureAwait(false);

            return await MapResponseAsync(response, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            return new ComparisonAdapterResult.Unavailable(
                "TIMEOUT",
                "The provider call timed out.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Provider transport failure for {Operation} (CorrelationId: {CorrelationId})",
                SupportedOperation,
                input.CorrelationId);
            return new ComparisonAdapterResult.Unavailable(
                "PROVIDER_UNAVAILABLE",
                "The comparison provider is temporarily unavailable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected comparison provider failure for {Operation} (CorrelationId: {CorrelationId})",
                SupportedOperation,
                input.CorrelationId);
            return new ComparisonAdapterResult.Unavailable(
                "PROVIDER_ERROR",
                "The comparison provider could not complete the request.");
        }
    }

    private bool TryCreateProviderUri(out Uri providerUri)
    {
        providerUri = default!;
        if (!_providerOptions.Enabled
            || string.IsNullOrWhiteSpace(_baseAddress)
            || string.IsNullOrWhiteSpace(_path)
            || string.IsNullOrWhiteSpace(_providerOptions.AdapterName))
        {
            return false;
        }

        return Uri.TryCreate($"{_baseAddress}/{_path}", UriKind.Absolute, out providerUri)
            && !string.IsNullOrWhiteSpace(_providerOptions.ResponseOutcomePath)
            && (providerUri.Scheme == Uri.UriSchemeHttp || providerUri.Scheme == Uri.UriSchemeHttps);
    }

    private bool TryValidateInput(
        ProviderNeutralComparisonInput input,
        out ComparisonAdapterResult? result)
    {
        result = null;
        var mapping = GetProviderOptions().InputMapping;
        var hasDocument = input.ProtectedDocumentReferenceId.HasValue || input.ProtectedDocumentStream is not null;
        var hasExtractedValues = input.ExtractedValues is { Count: > 0 };

        if (mapping.RequiresSubmittedValue)
        {
            if (string.IsNullOrWhiteSpace(input.SubmittedValue))
            {
                result = new ComparisonAdapterResult.NoUsableMatch(
                    "SUBMITTED_VALUE_EMPTY",
                    "The submitted value to compare is empty.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_providerOptions.SubmittedValueFieldName))
            {
                result = new ComparisonAdapterResult.Unavailable(
                    "CONFIGURATION_ERROR",
                    "The submitted-value field is not configured.");
                return false;
            }
        }

        if (mapping.RequiresDocumentFile)
        {
            if (!hasDocument)
            {
                result = new ComparisonAdapterResult.NoUsableMatch(
                    "MISSING_DOCUMENT",
                    "The configured comparison operation requires a document.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_providerOptions.DocumentFieldName))
            {
                result = new ComparisonAdapterResult.Unavailable(
                    "CONFIGURATION_ERROR",
                    "The document field is not configured.");
                return false;
            }
        }

        if (!mapping.AllowsDocumentAndExtractedValues && hasDocument && hasExtractedValues)
        {
            result = new ComparisonAdapterResult.NoUsableMatch(
                "CONFLICTING_INPUT_SOURCES",
                "The configured comparison operation does not accept both input sources.");
            return false;
        }

        foreach (var key in mapping.RequiredExtractedValueKeys ?? [])
        {
            if (string.IsNullOrWhiteSpace(key)
                || !input.ExtractedValues.TryGetValue(key, out var value)
                || string.IsNullOrWhiteSpace(value)
                || !GetProviderOptions().ExtractedValueFieldNames.TryGetValue(key, out var fieldName)
                || string.IsNullOrWhiteSpace(fieldName))
            {
                result = new ComparisonAdapterResult.NoUsableMatch(
                    "MISSING_EXTRACTED_VALUE",
                    "A configured extracted value is missing.");
                return false;
            }
        }

        if (_requestMode.Equals("Json", StringComparison.OrdinalIgnoreCase)
            && input.ProtectedDocumentStream is not null
            && !input.ProtectedDocumentReferenceId.HasValue
            && !string.IsNullOrWhiteSpace(GetProviderOptions().DocumentFieldName))
        {
            result = new ComparisonAdapterResult.NoUsableMatch(
                "DOCUMENT_REFERENCE_REQUIRED",
                "The configured JSON provider requires a protected document reference.");
            return false;
        }

        if (!_requestMode.Equals("Json", StringComparison.OrdinalIgnoreCase)
            && !_requestMode.Equals("Multipart", StringComparison.OrdinalIgnoreCase))
        {
            result = new ComparisonAdapterResult.Unavailable(
                "CONFIGURATION_ERROR",
                "The configured provider request mode is not supported.");
            return false;
        }

        return true;
    }

    private ComparisonProviderOptions GetProviderOptions() => _providerOptions;

    /// <summary>
    /// Sends a freshly constructed request for each attempt. A request body cannot be
    /// safely reused after HttpClient has sent it, especially for multipart streams.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Uri providerUri,
        ProviderNeutralComparisonInput input,
        byte[]? documentBytes,
        CancellationToken token)
    {
        var maxAttempts = Math.Max(1, _maxRetryAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = BuildRequest(providerUri, input, documentBytes);
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Max(1, _timeoutSeconds)));
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                token,
                timeoutCts.Token);

            try
            {
                _logger.LogInformation(
                    "Provider call attempt {Attempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
                    attempt,
                    maxAttempts,
                    SupportedOperation,
                    input.CorrelationId);

                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedTokenSource.Token).ConfigureAwait(false);

                if (!IsTransientStatusCode(response.StatusCode) || attempt == maxAttempts)
                {
                    return response;
                }

                response.Dispose();
                await DelayBeforeRetryAsync(attempt, maxAttempts, input, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < maxAttempts)
            {
                await DelayBeforeRetryAsync(attempt, maxAttempts, input, token).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (
                !token.IsCancellationRequested
                && attempt < maxAttempts
                && IsTransientException(ex))
            {
                await DelayBeforeRetryAsync(attempt, maxAttempts, input, token).ConfigureAwait(false);
            }
        }

        throw new HttpRequestException("The comparison provider did not complete after the configured attempts.");
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        int maxAttempts,
        ProviderNeutralComparisonInput input,
        CancellationToken token)
    {
        var delayMs = CalculateBackoffDelay(attempt, _baseDelayMilliseconds);
        _logger.LogInformation(
            "Waiting {DelayMs}ms before retry {RetryAttempt}/{MaxAttempts} for {Operation} (CorrelationId: {CorrelationId})",
            delayMs,
            attempt + 1,
            maxAttempts,
            SupportedOperation,
            input.CorrelationId);
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token).ConfigureAwait(false);
    }

    private HttpRequestMessage BuildRequest(
        Uri providerUri,
        ProviderNeutralComparisonInput input,
        byte[]? documentBytes)
    {
        var request = new HttpRequestMessage(new HttpMethod(_httpMethod), providerUri)
        {
            Content = BuildRequestContent(input, documentBytes)
        };
        request.Headers.Add("X-Correlation-Id", input.CorrelationId);
        if (!string.IsNullOrWhiteSpace(input.ActorReference))
        {
            request.Headers.Add("X-Actor-Reference", input.ActorReference);
        }

        return request;
    }

    private HttpContent BuildRequestContent(
        ProviderNeutralComparisonInput input,
        byte[]? documentBytes)
    {
        var providerOptions = GetProviderOptions();
        return _requestMode.Equals("Multipart", StringComparison.OrdinalIgnoreCase)
            ? BuildMultipartContent(input, documentBytes, providerOptions)
            : BuildJsonContent(input, providerOptions);
    }

    private static HttpContent BuildJsonContent(
        ProviderNeutralComparisonInput input,
        ComparisonProviderOptions providerOptions)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(providerOptions.SubmittedValueFieldName))
        {
            fields[providerOptions.SubmittedValueFieldName] = input.SubmittedValue;
        }

        foreach (var pair in input.ExtractedValues)
        {
            if (providerOptions.ExtractedValueFieldNames.TryGetValue(pair.Key, out var fieldName)
                && !string.IsNullOrWhiteSpace(fieldName))
            {
                fields[fieldName] = pair.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(providerOptions.DocumentFieldName)
            && input.ProtectedDocumentReferenceId.HasValue)
        {
            fields[providerOptions.DocumentFieldName] = input.ProtectedDocumentReferenceId.Value.ToString("D");
        }

        return new StringContent(
            JsonSerializer.Serialize(fields),
            Encoding.UTF8,
            "application/json");
    }

    private static HttpContent BuildMultipartContent(
        ProviderNeutralComparisonInput input,
        byte[]? documentBytes,
        ComparisonProviderOptions providerOptions)
    {
        var multipart = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(providerOptions.SubmittedValueFieldName))
        {
            multipart.Add(
                new StringContent(input.SubmittedValue, Encoding.UTF8),
                providerOptions.SubmittedValueFieldName);
        }

        foreach (var pair in input.ExtractedValues)
        {
            if (providerOptions.ExtractedValueFieldNames.TryGetValue(pair.Key, out var fieldName)
                && !string.IsNullOrWhiteSpace(fieldName))
            {
                multipart.Add(new StringContent(pair.Value, Encoding.UTF8), fieldName);
            }
        }

        if (documentBytes is not null && !string.IsNullOrWhiteSpace(providerOptions.DocumentFieldName))
        {
            var documentContent = new ByteArrayContent(documentBytes);
            documentContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(documentContent, providerOptions.DocumentFieldName, "identity-document");
        }
        else if (input.ProtectedDocumentReferenceId.HasValue
                 && !string.IsNullOrWhiteSpace(providerOptions.DocumentFieldName))
        {
            multipart.Add(
                new StringContent(input.ProtectedDocumentReferenceId.Value.ToString("D")),
                providerOptions.DocumentFieldName);
        }

        return multipart;
    }

    private async Task<ComparisonAdapterResult> MapResponseAsync(
        HttpResponseMessage response,
        CancellationToken token)
    {
        if (IsTransientStatusCode(response.StatusCode))
        {
            return new ComparisonAdapterResult.Unavailable(
                "PROVIDER_UNAVAILABLE",
                "The comparison provider is temporarily unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new ComparisonAdapterResult.NoUsableMatch(
                "PROVIDER_REJECTED",
                "The comparison provider rejected the request.");
        }

        if (string.IsNullOrWhiteSpace(_responseOutcomePath))
        {
            return new ComparisonAdapterResult.NoUsableMatch(
                "OUTCOME_NOT_CONFIGURED",
                "The provider comparison outcome is not configured.");
        }

        try
        {
            await using var responseStream = await response.Content
                .ReadAsStreamAsync(token)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: token)
                .ConfigureAwait(false);

            if (!TryGetJsonValue(document.RootElement, _responseOutcomePath, out var outcomeElement)
                || outcomeElement.ValueKind != JsonValueKind.String)
            {
                return new ComparisonAdapterResult.NoUsableMatch(
                    "OUTCOME_NOT_USABLE",
                    "The provider response did not contain a usable comparison outcome.");
            }

            var outcome = outcomeElement.GetString()?.Trim();
            if (string.Equals(outcome, nameof(ComparisonOutcome.Match), StringComparison.OrdinalIgnoreCase))
            {
                return new ComparisonAdapterResult.Match("The comparison matched.");
            }

            if (string.Equals(outcome, nameof(ComparisonOutcome.NonMatch), StringComparison.OrdinalIgnoreCase))
            {
                return new ComparisonAdapterResult.NonMatch("The comparison did not match.");
            }

            if (string.Equals(outcome, nameof(ComparisonOutcome.NoUsableMatch), StringComparison.OrdinalIgnoreCase))
            {
                return new ComparisonAdapterResult.NoUsableMatch(
                    "NO_USABLE_MATCH",
                    "The comparison did not produce a usable match.");
            }

            return new ComparisonAdapterResult.NoUsableMatch(
                "UNKNOWN_OUTCOME",
                "The provider response contained an unknown comparison outcome.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "The comparison provider returned an unusable response for {Operation}",
                SupportedOperation);
            return new ComparisonAdapterResult.NoUsableMatch(
                "RESPONSE_NOT_USABLE",
                "The provider response could not be mapped.");
        }
    }

    private static bool TryGetJsonValue(
        JsonElement root,
        string path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static async Task<byte[]?> ReadDocumentBytesAsync(
        Stream? documentStream,
        CancellationToken token)
    {
        if (documentStream is null)
        {
            return null;
        }

        var originalPosition = documentStream.CanSeek ? documentStream.Position : (long?)null;
        try
        {
            if (documentStream.CanSeek)
            {
                documentStream.Position = 0;
            }

            using var buffer = new MemoryStream();
            await documentStream.CopyToAsync(buffer, token).ConfigureAwait(false);
            return buffer.ToArray();
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                documentStream.Position = originalPosition.Value;
            }
        }
    }

    private static bool IsTransientException(HttpRequestException exception)
    {
        return exception.StatusCode is null || IsTransientStatusCode(exception.StatusCode.Value);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode >= 500;
    }

    private static int CalculateBackoffDelay(int attempt, int baseDelayMilliseconds)
    {
        var safeBaseDelay = Math.Max(0, baseDelayMilliseconds);
        var exponent = Math.Min(attempt - 1, 10);
        var exponentialDelay = safeBaseDelay * (1 << exponent);
        var jitter = Random.Shared.Next(0, Math.Max(1, exponentialDelay / 3 + 1));
        return exponentialDelay + jitter;
    }
}
