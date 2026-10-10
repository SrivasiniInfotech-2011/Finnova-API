using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Contracts.Common;
using Finnova.Service.Assets.ClassCodes.Commands.CreateClassCode;
using Finnova.Service.Assets.ClassCodes.Commands.UpdateClassCode;
using Finnova.Service.Assets.ClassCodes.Commands.DeleteClassCode;
using Finnova.Service.Assets.ClassCodes.Queries.GetClassCodesPaged;
using Finnova.Service.Assets.ClassCodes.Queries.GetActiveClassCodes;
using Finnova.Service.Assets.MakeCodes.Commands.CreateMakeCode;
using Finnova.Service.Assets.MakeCodes.Commands.UpdateMakeCode;
using Finnova.Service.Assets.MakeCodes.Commands.DeleteMakeCode;
using Finnova.Service.Assets.MakeCodes.Queries.GetMakeCodesPaged;
using Finnova.Service.Assets.MakeCodes.Queries.GetActiveMakeCodes;
using Finnova.Service.Assets.TypeCodes.Commands.CreateTypeCode;
using Finnova.Service.Assets.TypeCodes.Commands.UpdateTypeCode;
using Finnova.Service.Assets.TypeCodes.Commands.DeleteTypeCode;
using Finnova.Service.Assets.TypeCodes.Queries.GetTypeCodesPaged;
using Finnova.Service.Assets.TypeCodes.Queries.GetActiveTypeCodes;
using Finnova.Service.Assets.ModelCodes.Commands.CreateModelCode;
using Finnova.Service.Assets.ModelCodes.Commands.UpdateModelCode;
using Finnova.Service.Assets.ModelCodes.Commands.DeleteModelCode;
using Finnova.Service.Assets.ModelCodes.Queries.GetModelCodesPaged;
using Finnova.Service.Assets.ModelCodes.Queries.GetActiveModelCodes;
using Finnova.Service.Assets.Assets.Commands.CreateAsset;
using Finnova.Service.Assets.Assets.Commands.UpdateAsset;
using Finnova.Service.Assets.Assets.Queries.GetAssetsPaged;
using Finnova.Service.Assets.Assets.Queries.GetAssetById;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Asset Master endpoints (FINNOVA-14). A single controller exposes both areas
/// (Asset Definition code masters + Asset Mapping) under /api/asset. Every action is
/// SystemAdmin-only (R7.1-R7.3); all work flows through MediatR so ValidationBehavior runs.
/// </summary>
[ApiController]
[Route("api/[controller]")]            // -> /api/asset
[Authorize(Policy = "SystemAdmin")]
public class AssetController : ControllerBase
{
    private readonly IMediator _mediator;
    public AssetController(IMediator mediator) => _mediator = mediator;

    // ---------------- Class Codes ----------------
    [HttpGet("class-codes")]
    public async Task<ActionResult<PaginatedResponse<ClassCodeResponse>>> GetClassCodes(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetClassCodesPagedQuery(search, page, pageSize)));

    [HttpGet("class-codes/active")]
    public async Task<ActionResult<List<CodeListItemResponse>>> GetActiveClassCodes()
        => Ok(await _mediator.Send(new GetActiveClassCodesQuery()));

    [HttpPost("class-codes")]
    public async Task<ActionResult<ClassCodeResponse>> CreateClassCode([FromBody] CreateClassCodeRequest r)
    {
        var result = await _mediator.Send(new CreateClassCodeCommand(r.Code, r.Description, r.IsActive));
        return CreatedAtAction(nameof(GetClassCodes), new { search = result.Code }, result);
    }

    [HttpPut("class-codes/{id:guid}")]
    public async Task<ActionResult<ClassCodeResponse>> UpdateClassCode(Guid id, [FromBody] UpdateClassCodeRequest r)
        => Ok(await _mediator.Send(new UpdateClassCodeCommand(id, r.Code, r.Description, r.IsActive)));

    [HttpDelete("class-codes/{id:guid}")]
    public async Task<IActionResult> DeleteClassCode(Guid id, [FromQuery] bool hardDelete = false)
    { await _mediator.Send(new DeleteClassCodeCommand(id, hardDelete)); return NoContent(); }

    // ---------------- Make Codes ----------------
    [HttpGet("make-codes")]
    public async Task<ActionResult<PaginatedResponse<MakeCodeResponse>>> GetMakeCodes(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetMakeCodesPagedQuery(search, page, pageSize)));

    [HttpGet("make-codes/active")]
    public async Task<ActionResult<List<CodeListItemResponse>>> GetActiveMakeCodes()
        => Ok(await _mediator.Send(new GetActiveMakeCodesQuery()));

    [HttpPost("make-codes")]
    public async Task<ActionResult<MakeCodeResponse>> CreateMakeCode([FromBody] CreateMakeCodeRequest r)
    {
        var result = await _mediator.Send(new CreateMakeCodeCommand(r.Code, r.Description, r.IsActive));
        return CreatedAtAction(nameof(GetMakeCodes), new { search = result.Code }, result);
    }

    [HttpPut("make-codes/{id:guid}")]
    public async Task<ActionResult<MakeCodeResponse>> UpdateMakeCode(Guid id, [FromBody] UpdateMakeCodeRequest r)
        => Ok(await _mediator.Send(new UpdateMakeCodeCommand(id, r.Code, r.Description, r.IsActive)));

    [HttpDelete("make-codes/{id:guid}")]
    public async Task<IActionResult> DeleteMakeCode(Guid id, [FromQuery] bool hardDelete = false)
    { await _mediator.Send(new DeleteMakeCodeCommand(id, hardDelete)); return NoContent(); }

    // ---------------- Type Codes ----------------
    [HttpGet("type-codes")]
    public async Task<ActionResult<PaginatedResponse<TypeCodeResponse>>> GetTypeCodes(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetTypeCodesPagedQuery(search, page, pageSize)));

    [HttpGet("type-codes/active")]
    public async Task<ActionResult<List<CodeListItemResponse>>> GetActiveTypeCodes()
        => Ok(await _mediator.Send(new GetActiveTypeCodesQuery()));

    [HttpPost("type-codes")]
    public async Task<ActionResult<TypeCodeResponse>> CreateTypeCode([FromBody] CreateTypeCodeRequest r)
    {
        var result = await _mediator.Send(new CreateTypeCodeCommand(r.Code, r.Description, r.IsActive));
        return CreatedAtAction(nameof(GetTypeCodes), new { search = result.Code }, result);
    }

    [HttpPut("type-codes/{id:guid}")]
    public async Task<ActionResult<TypeCodeResponse>> UpdateTypeCode(Guid id, [FromBody] UpdateTypeCodeRequest r)
        => Ok(await _mediator.Send(new UpdateTypeCodeCommand(id, r.Code, r.Description, r.IsActive)));

    [HttpDelete("type-codes/{id:guid}")]
    public async Task<IActionResult> DeleteTypeCode(Guid id, [FromQuery] bool hardDelete = false)
    { await _mediator.Send(new DeleteTypeCodeCommand(id, hardDelete)); return NoContent(); }

    // ---------------- Model Codes ----------------
    [HttpGet("model-codes")]
    public async Task<ActionResult<PaginatedResponse<ModelCodeResponse>>> GetModelCodes(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetModelCodesPagedQuery(search, page, pageSize)));

    [HttpGet("model-codes/active")]
    public async Task<ActionResult<List<CodeListItemResponse>>> GetActiveModelCodes()
        => Ok(await _mediator.Send(new GetActiveModelCodesQuery()));

    [HttpPost("model-codes")]
    public async Task<ActionResult<ModelCodeResponse>> CreateModelCode([FromBody] CreateModelCodeRequest r)
    {
        var result = await _mediator.Send(new CreateModelCodeCommand(r.Code, r.Description, r.IsActive));
        return CreatedAtAction(nameof(GetModelCodes), new { search = result.Code }, result);
    }

    [HttpPut("model-codes/{id:guid}")]
    public async Task<ActionResult<ModelCodeResponse>> UpdateModelCode(Guid id, [FromBody] UpdateModelCodeRequest r)
        => Ok(await _mediator.Send(new UpdateModelCodeCommand(id, r.Code, r.Description, r.IsActive)));

    [HttpDelete("model-codes/{id:guid}")]
    public async Task<IActionResult> DeleteModelCode(Guid id, [FromQuery] bool hardDelete = false)
    { await _mediator.Send(new DeleteModelCodeCommand(id, hardDelete)); return NoContent(); }

    // ---------------- Assets (Asset Mapping) ----------------
    [HttpGet("assets")]
    public async Task<ActionResult<PaginatedResponse<AssetResponse>>> GetAssets(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetAssetsPagedQuery(search, page, pageSize)));

    [HttpGet("assets/{id:guid}")]
    public async Task<ActionResult<AssetResponse>> GetAssetById(Guid id)
        => Ok(await _mediator.Send(new GetAssetByIdQuery(id)));

    [HttpPost("assets")]
    public async Task<ActionResult<AssetResponse>> CreateAsset([FromBody] CreateAssetRequest r)
    {
        var result = await _mediator.Send(new CreateAssetCommand(
            r.ClassCodeId, r.TypeCodeId, r.MakeCodeId, r.ModelCodeId, r.Description,
            r.BookDepreciationCategory, r.BookDepreciationRate, r.StockDepreciationCategory,
            r.StockDepreciationRate, r.GuidelineLimit, r.IsActive));
        return CreatedAtAction(nameof(GetAssetById), new { id = result.Id }, result);
    }

    [HttpPut("assets/{id:guid}")]
    public async Task<ActionResult<AssetResponse>> UpdateAsset(Guid id, [FromBody] UpdateAssetRequest r)
        => Ok(await _mediator.Send(new UpdateAssetCommand(id, r.ClassCodeId, r.TypeCodeId, r.MakeCodeId,
            r.ModelCodeId, r.Description, r.BookDepreciationCategory, r.BookDepreciationRate,
            r.StockDepreciationCategory, r.StockDepreciationRate, r.GuidelineLimit, r.IsActive)));
}
