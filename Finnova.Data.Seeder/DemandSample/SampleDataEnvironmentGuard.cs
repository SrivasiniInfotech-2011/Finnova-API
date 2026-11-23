using System;
using System.Collections.Generic;
using System.Text;

namespace Finnova.Data.Seeder.DemandSample;

public sealed class SampleDataEnvironmentException(string message) : InvalidOperationException(message);

/// <summary>
/// The data tool runs only in Development or Testing. There is deliberately no override.
/// An unset, empty, or unrecognized environment is refused. The message never echoes the value.
/// </summary>
public static class SampleDataEnvironmentGuard
{
    public static string Require(string? environmentName)
    {
        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)) return "Development";
        if (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)) return "Testing";
        throw new SampleDataEnvironmentException("The data tool runs only when DOTNET_ENVIRONMENT is Development or Testing.");
    }
}