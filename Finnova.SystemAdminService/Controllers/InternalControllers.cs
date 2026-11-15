using System.Security.Claims;
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Auth;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Service.LoanAccounts.Kyc;
using Finnova.SystemAdminService.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Finnova.SystemAdminService.Controllers;

[ApiController, Route("internal/service-auth")]
public sealed class ServiceAuthController : ControllerBase
{
    private readonly FinnovaDbContext _db;
    private readonly ServiceTokenIssuer _issuer;
    private readonly ILogger<ServiceAuthController> _log;
    public ServiceAuthController(FinnovaDbContext db, ServiceTokenIssuer issuer, ILogger<ServiceAuthController> log)
        => (_db, _issuer, _log) = (db, issuer, log);

    [HttpPost("token"), AllowAnonymous, EnableRateLimiting("service-token")]
    public async Task<ActionResult<ServiceTokenResponse>> Token([FromBody] ServiceTokenRequest r, CancellationToken ct)
    {
        var credential = await _db.ServiceCredentials.FirstOrDefaultAsync(x => x.ClientId == r.ClientId && x.IsActive, ct);
        if (credential is null || !PasswordHasher.Verify(r.ClientSecret, credential.SecretHash))
        {
            _log.LogWarning("Service token request rejected for client {ClientId}.", r.ClientId);   // no secret in the log
            return Unauthorized();
        }
        credential.LastUsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(_issuer.Issue(credential.ClientId));
    }
}

[ApiController, Route("internal/dcn")]
public sealed class InternalDncController : ControllerBase
{
    private readonly IMediator _mediator;
    public InternalDncController(IMediator mediator) => _mediator = mediator;

    [HttpPost("issue"), Authorize(Policy = "ServiceDncIssue")]
    public async Task<ActionResult<InternalIssueResponse>> Issue([FromBody] InternalIssueRequest r, CancellationToken ct)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "SystemUser";
        var issued = await _mediator.Send(new IssueNumberCommand(
            r.DocumentType, NumberScope.Global, null, actor, r.LineOfBusinessId, r.BranchId, UseFallback: true), ct);
        return Ok(new InternalIssueResponse(issued.Number, issued.SchemeId, issued.SchemeCode ?? string.Empty, issued.ScopeLevel ?? string.Empty));
    }
}

[ApiController, Route("internal/kyc")]
public sealed class InternalKycController : ControllerBase
{
    private readonly IKycVerificationFacade _facade;
    public InternalKycController(IKycVerificationFacade facade) => _facade = facade;

    [HttpPost("verify"), Authorize(Policy = "ServiceKycVerify")]
    public async Task<ActionResult<InternalKycResponse>> Verify([FromBody] InternalKycRequest r, CancellationToken ct)
        => Ok(await _facade.VerifyAsync(r, ct));
}
