using Microsoft.AspNetCore.Http;

namespace Finnova.Service.IdentityVerification.Correlation;

public sealed record RequestContext(string CorrelationId, string ActorReference, DateTime UtcNow);

public interface IRequestContextAccessor
{
    RequestContext Current { get; }
}

public sealed class RequestContextAccessor(IHttpContextAccessor http) : IRequestContextAccessor
{
    public RequestContext Current => new(http.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), http.HttpContext?.User.Identity?.Name ?? "system-admin", DateTime.UtcNow);
}
