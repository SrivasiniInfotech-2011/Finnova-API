using Finnova.Models.Domain.Exceptions;
using Microsoft.Extensions.Options;
using Finnova.Service.IdentityVerification.External;
namespace Finnova.Service.IdentityVerification.Validation;
public sealed class DocumentInputValidator
{
    private readonly IdentityVerificationOptions _options;
    public DocumentInputValidator(IOptions<IdentityVerificationOptions> options) => _options = options.Value;
    public void Validate(string fileName, string contentType, long sizeBytes, ReadOnlySpan<byte> header)
    {
        if (sizeBytes > _options.MaxDocumentBytes) throw new IdentityVerificationDocumentSizeException();
        if (!_options.AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase) || !IdentityVerificationRules.HasAllowedExtension(fileName, new[] { contentType }) || !HasSignature(contentType, header)) throw new IdentityVerificationDocumentFormatException();
    }
    private static bool HasSignature(string type, ReadOnlySpan<byte> h) => type switch
    {
        "image/jpeg" => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        "image/png" => h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }),
        "application/pdf" => h.Length >= 5 && h[..5].SequenceEqual("%PDF-"u8), _ => false
    };
}
