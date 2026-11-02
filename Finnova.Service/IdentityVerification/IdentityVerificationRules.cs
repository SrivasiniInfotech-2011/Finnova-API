using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.IdentityVerification;

/// <summary>
/// Pure validation, normalization, masking, and document-reference helpers for identity verification.
/// </summary>
public static partial class IdentityVerificationRules
{
    [GeneratedRegex("^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.CultureInvariant)]
    private static partial Regex PanPattern();

    [GeneratedRegex("^[0-9]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex AadhaarPattern();

    /// <summary>
    /// Trims and uppercases a PAN, then validates the Indian PAN format.
    /// </summary>
    public static string NormalizePan(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || !PanPattern().IsMatch(normalized))
        {
            throw new IdentityVerificationValidationException();
        }

        return normalized;
    }

    /// <summary>
    /// Removes user-entered separators from an Aadhaar number and validates its 12-digit format.
    /// </summary>
    public static string NormalizeAadhaar(string value)
    {
        var normalized = value is null
            ? string.Empty
            : string.Concat(value.Where(character => !char.IsWhiteSpace(character) && character != '-'));

        if (!AadhaarPattern().IsMatch(normalized))
        {
            throw new IdentityVerificationValidationException();
        }

        return normalized;
    }

    /// <summary>
    /// Trims required text without placing the supplied value in an exception message or log.
    /// </summary>
    public static string RequireText(string? value, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new IdentityVerificationValidationException();
        }

        return normalized;
    }

    /// <summary>
    /// Returns a masked identifier reference that never returns the complete identifier.
    /// </summary>
    public static string MaskIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= 4)
        {
            return "****";
        }

        return new string('*', value.Length - 4) + value[^4..];
    }

    /// <summary>
    /// Creates a keyed, irreversible reference for exact authorized searches.
    /// </summary>
    public static string SearchReference(string value, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    public static bool IsSupportedContentType(string value) =>
        value is "image/jpeg" or "image/png" or "application/pdf";

    /// <summary>
    /// Checks the supported identity-document filename extensions.
    /// </summary>
    public static bool HasAllowedExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".pdf";
    }

    /// <summary>
    /// Checks a filename against the MIME types accepted by a document validator.
    /// </summary>
    public static bool HasAllowedExtension(
        string? fileName,
        IReadOnlyCollection<string> acceptedContentTypes)
    {
        if (!HasAllowedExtension(fileName) || acceptedContentTypes is null)
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return acceptedContentTypes.Any(contentType => contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            "application/pdf" => extension == ".pdf",
            _ => false
        });
    }
}
