namespace Finnova.Service.IdentityVerification.External;
public sealed class IdentityVerificationOptions
{
    public const string SectionName = "IdentityVerification";
    public long MaxDocumentBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AcceptedContentTypes { get; set; } = ["image/jpeg", "image/png", "application/pdf"];
    public RetryOptions Retry { get; set; } = new();
    public ProtectedBlobStorageOptions BlobStorage { get; set; } = new();
    public ProviderOperationOptions PanIdentifierValidation { get; set; } = new();
    public ProviderOperationOptions AadhaarIdentifierValidation { get; set; } = new();
    public ComparisonProviderOptions PanNameVerification { get; set; } = new();
    public ComparisonProviderOptions AadhaarAddressVerification { get; set; } = new();
}
public sealed class ProtectedBlobStorageOptions
{
    public bool Enabled { get; set; }
    public string ConnectionStringName { get; set; } = "IdentityVerificationBlobStorage";
    public string ContainerName { get; set; } = "identity-verification-documents";
    public string Prefix { get; set; } = "identity-verification";
    public long MaxDocumentBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AcceptedContentTypes { get; set; } = ["image/jpeg", "image/png", "application/pdf"];
    public bool CreateContainerIfMissing { get; set; }
    public bool AllowOverwrite { get; set; }
    public int UploadTimeoutSeconds { get; set; } = 60;
    public int DownloadTimeoutSeconds { get; set; } = 60;
    public int DeleteTimeoutSeconds { get; set; } = 30;
}
public sealed class RetryOptions { public int MaxAttempts { get; set; } = 3; public int BaseDelayMilliseconds { get; set; } = 200; public int TimeoutSeconds { get; set; } = 10; }
public class ProviderOperationOptions { public bool Enabled { get; set; } public string BaseAddress { get; set; } = ""; public string Path { get; set; } = ""; public string HttpMethod { get; set; } = "POST"; public string RequestMode { get; set; } = "Json"; public string IdentifierFieldName { get; set; } = ""; }
public sealed class ComparisonProviderOptions : ProviderOperationOptions
{
    public string AdapterName { get; set; } = "";
    public ComparisonInputMapping InputMapping { get; set; } = new();
    public string? SubmittedValueFieldName { get; set; }
    public string? DocumentFieldName { get; set; }
    public Dictionary<string, string> ExtractedValueFieldNames { get; set; } = new();
    public string? ResponseOutcomePath { get; set; }
    public string? ResponseMessagePath { get; set; }
}
public sealed class ComparisonInputMapping
{
    public bool RequiresDocumentFile { get; set; }
    public bool RequiresSubmittedValue { get; set; } = true;
    public List<string> RequiredExtractedValueKeys { get; set; } = [];
    public bool AllowsDocumentAndExtractedValues { get; set; } = true;
    public string MappingProfile { get; set; } = "";
}
