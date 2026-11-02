using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Service.IdentityVerification.Commands.ManualOverrides;
using Finnova.Service.IdentityVerification.Storage;
using Finnova.Service.IdentityVerification.Validation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Finnova.SystemAdminService.Controllers;
[ApiController, Route("api/identity-verification/manual"), Authorize(Policy = "SystemAdmin")]
public sealed class IdentityVerificationManualController(IMediator mediator, DocumentInputValidator validator, ProtectedDocumentLifecycle lifecycle) : ControllerBase
{
    [HttpPost("comparison")] public Task<ComparisonVerificationResult> Comparison([FromBody] ManualComparisonDecisionRequest request) => mediator.Send(new SubmitManualComparisonCommand(request, User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted);
    [HttpPost("pan"), Consumes("multipart/form-data")] public Task<IdentifierValidationResult> Pan([FromForm] ManualPanForm form) => Upload(form.DocumentFile, (id, size, type) => mediator.Send(new SubmitManualPanCommand(new(form.Pan, form.CardholderName, form.DateOfBirth, id, type, size), User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted));
    [HttpPost("aadhaar"), Consumes("multipart/form-data")] public Task<IdentifierValidationResult> Aadhaar([FromForm] ManualAadhaarForm form) => Upload(form.DocumentFile, (id, size, type) => mediator.Send(new SubmitManualAadhaarCommand(new(form.AadhaarNumber, form.CardholderName, form.Address, id, type, size), User.Identity?.Name ?? "system-admin"), HttpContext.RequestAborted));
    private async Task<T> Upload<T>(IFormFile? file, Func<Guid, long, string, Task<T>> send)
    {
        if (file is null) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationValidationException(["DocumentFile"]); if (file.Length <= 0 || file.Length > 5 * 1024 * 1024) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationDocumentSizeException(); await using (var headerStream = file.OpenReadStream()) { var header = new byte[16]; var read = 0; while (read < header.Length) { var n = await headerStream.ReadAsync(header.AsMemory(read), HttpContext.RequestAborted); if (n == 0) break; read += n; } validator.Validate(file.FileName, file.ContentType, file.Length, header[..read]); } await using var input = file.OpenReadStream(); return await lifecycle.UploadAndExecuteAsync(input, file.ContentType, file.Length, reference => send(reference.Id, reference.SizeBytes, reference.ContentType), HttpContext.RequestAborted);
    }
    public sealed class ManualPanForm { public string Pan { get; set; } = ""; public string CardholderName { get; set; } = ""; public DateOnly DateOfBirth { get; set; } public IFormFile? DocumentFile { get; set; } }
    public sealed class ManualAadhaarForm { public string AadhaarNumber { get; set; } = ""; public string CardholderName { get; set; } = ""; public string Address { get; set; } = ""; public IFormFile? DocumentFile { get; set; } }
}
