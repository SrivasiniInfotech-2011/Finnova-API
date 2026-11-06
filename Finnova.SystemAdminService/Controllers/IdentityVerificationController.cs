using System.Security.Claims;
using System.Text.Json;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
using Finnova.Service.IdentityVerification.Commands.VerifyPanName;
using Finnova.Service.IdentityVerification.Storage;
using Finnova.Service.IdentityVerification.Validation;
using Finnova.Service.IdentityVerification.Workflow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Finnova.SystemAdminService.Controllers;
[ApiController, Route("api/identity-verification"), Authorize(Policy = "SystemAdmin")]
public sealed class IdentityVerificationController(IMediator mediator, DocumentInputValidator validator, ProtectedDocumentLifecycle lifecycle, IVerificationWorkflowCoordinator coordinator) : ControllerBase
{
    [HttpPost("verify-pan-name"), Consumes("multipart/form-data")] public Task<ActionResult<ComparisonVerificationResult>> VerifyPanName([FromForm] ComparisonForm form) => UploadAndSend(form, id => mediator.Send(new VerifyPanNameCommand(new(form.SubmittedValue ?? string.Empty, form.ToSource(id)), Actor()), HttpContext.RequestAborted));
    [HttpPost("verify-aadhar-address"), Consumes("multipart/form-data")] public Task<ActionResult<ComparisonVerificationResult>> VerifyAadharAddress([FromForm] ComparisonForm form) => UploadAndSend(form, id => mediator.Send(new VerifyAadharAddressCommand(new(form.SubmittedValue ?? string.Empty, form.ToSource(id)), Actor()), HttpContext.RequestAborted));
    [HttpPost("workflows/pan-and-name")] public Task<VerificationWorkflowResponse> PanAndName([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.PanAndName }, HttpContext.RequestAborted);
    [HttpPost("workflows/aadhaar-and-address")] public Task<VerificationWorkflowResponse> AadhaarAndAddress([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.AadhaarAndAddress }, HttpContext.RequestAborted);
    [HttpPost("workflows/full")] public Task<VerificationWorkflowResponse> Full([FromBody] VerificationWorkflowRequest r) => coordinator.ExecuteAsync(r with { WorkflowType = Finnova.Models.Domain.Enums.VerificationWorkflowType.Full }, HttpContext.RequestAborted);
    private async Task<ActionResult<ComparisonVerificationResult>> UploadAndSend(ComparisonForm form, Func<Guid?, Task<ComparisonVerificationResult>> send)
    {
        if (form.DocumentFile is null) return Ok(await send(null)); var file = form.DocumentFile; var header = await ReadHeaderAsync(file, HttpContext.RequestAborted); validator.Validate(file.FileName, file.ContentType, file.Length, header); await using var input = file.OpenReadStream(); var result = await lifecycle.UploadAndExecuteAsync(input, file.ContentType, file.Length, reference => send(reference.Id), HttpContext.RequestAborted); return Ok(result);
    }
    private async Task<byte[]> ReadHeaderAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > 5 * 1024 * 1024) throw new Finnova.Models.Domain.Exceptions.IdentityVerificationDocumentSizeException(); await using var input = file.OpenReadStream(); var buffer = new byte[16]; var offset = 0; while (offset < buffer.Length) { var read = await input.ReadAsync(buffer.AsMemory(offset), ct); if (read == 0) break; offset += read; } return buffer[..offset];
    }
    private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system-admin";
    public sealed class ComparisonForm { public string? SubmittedValue { get; set; } public IFormFile? DocumentFile { get; set; } public string? ExtractedValuesJson { get; set; } public string? MappingProfile { get; set; } public Guid? SessionId { get; set; } public ComparisonSourceContract ToSource(Guid? id) { var values = string.IsNullOrWhiteSpace(ExtractedValuesJson) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(ExtractedValuesJson); return new(DocumentFile?.ContentType, DocumentFile?.Length, id, values, MappingProfile, SessionId); } }
}
