namespace Finnova.Tests.Integration;

/// <summary>
/// Compatibility entry point for integration tests that expect a generic test application
/// factory. The existing SystemAdminAppFactory owns all host configuration.
/// </summary>
public static class TestApplicationFactory
{
    public static SystemAdminAppFactory Create() => new();
}
