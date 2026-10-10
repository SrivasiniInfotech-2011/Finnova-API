namespace Finnova.Service.Assets.CodeGeneration;

public sealed class AssetCodeGenerator : IAssetCodeGenerator
{
    public string NextAssetCode(string classCode, int currentMaxSequence)
    {
        var prefix = (classCode ?? string.Empty).Trim().ToUpperInvariant();
        var next = currentMaxSequence + 1;
        return $"{prefix}-{next:D6}";   // e.g. "LAP-000123"
    }
}
