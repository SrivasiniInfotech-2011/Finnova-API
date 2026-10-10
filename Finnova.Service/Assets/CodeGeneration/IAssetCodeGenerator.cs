namespace Finnova.Service.Assets.CodeGeneration;

public interface IAssetCodeGenerator
{
    /// <summary>Deterministically builds the next Asset Code for a Class Code and the current
    /// highest sequence already used for that class. Format: "{CLASS}-{seq:D6}" (R3.2, R3.3).</summary>
    string NextAssetCode(string classCode, int currentMaxSequence);
}
