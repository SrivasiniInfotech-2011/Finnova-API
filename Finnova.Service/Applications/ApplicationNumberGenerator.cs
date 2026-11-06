using Finnova.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Finnova.Service.Applications;

public sealed class ApplicationNumberGenerator : IApplicationNumberGenerator
{
    private readonly IConfiguration _configuration;
    public ApplicationNumberGenerator(IConfiguration configuration) => _configuration = configuration;
    public string Next()
    {
        var prefix = _configuration["ApplicationProcessing:ApplicationNumberPrefix"] ?? "APP";
        return $"{prefix}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
    }
}
