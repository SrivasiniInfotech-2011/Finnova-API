using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;
using Finnova.Service.UserManagement.Commands.CreateUserAccount;
using Finnova.Service.UserManagement.Commands.CreateUserGroup;
using Finnova.Service.UserManagement.Commands.ResetPassword;
using Finnova.Service.UserManagement.Commands.SaveUserAccess;
using Finnova.Service.UserManagement.Commands.UpdateUserAccount;
using Finnova.Service.UserManagement.Commands.UpdateUserGroup;
using Finnova.Service.UserManagement.Queries.GetUserAccess;
using Finnova.Service.UserManagement.Queries.GetUserAuditTrail;
using Finnova.Service.UserManagement.Queries.GetUserRecordByCode;
using Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;
using Finnova.Service.UserManagement.Queries.References;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// User Management endpoints (FS §9). Every action is SystemAdmin-only (R15). All work flows
/// through MediatR so the ValidationBehavior pipeline runs before each handler. The acting admin
/// is read from the JWT (R14.1/14.2), never the request body.
/// </summary>
[ApiController]
[Route("api/user")]
public class UserManagementController : ControllerBase
{
    private readonly IMediator _mediator;
    public UserManagementController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered union list of users / groups / functional groups (R13).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<UserListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<UserListItemResponse>>> GetPaged(
        [FromQuery] string? search,
        [FromQuery] UserConfiguration? kind,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetUserRecordsPagedQuery(search, kind, isActive, page, pageSize)));

    /// <summary>Read a user record by code (Modify/Query modes, R11.1/R12.1). 404 when missing.</summary>
    [HttpGet("by-code/{code}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccountResponse>> GetByCode(string code)
        => Ok(await _mediator.Send(new GetUserRecordByCodeQuery(code)));

    /// <summary>Create an individual user (R3). 409 on code collision.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserAccountResponse>> Create([FromBody] CreateUserAccountRequest r)
    {
        var result = await _mediator.Send(new CreateUserAccountCommand(
            r.Name, r.Password, r.DateOfJoining, r.Designation, r.Department,
            r.MobileNumber, r.Email, r.UserType, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetByCode), new { code = result.UserCode }, result);
    }

    /// <summary>Modify a user (R11). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccountResponse>> Update(Guid id, [FromBody] UpdateUserAccountRequest r)
        => Ok(await _mediator.Send(new UpdateUserAccountCommand(
            id, r.Name, r.DateOfJoining, r.Designation, r.Department,
            r.MobileNumber, r.Email, r.UserType, r.IsActive, GetActingAdmin())));

    /// <summary>Reset a user's password (Modify-mode facility, R11.5/11.6). 204 on success.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest r)
    {
        await _mediator.Send(new ResetPasswordCommand(id, r.NewPassword, GetActingAdmin()));
        return NoContent();
    }

    /// <summary>Create a user group (R5). 409 on collision / inactive member.</summary>
    [HttpPost("group")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserGroupResponse>> CreateGroup([FromBody] CreateUserGroupRequest r)
    {
        var result = await _mediator.Send(new CreateUserGroupCommand(
            r.Name, r.MemberUserCodes, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetByCode), new { code = result.UserGroupCode }, result);
    }

    /// <summary>Modify a user group (R5/R11). 404 when missing.</summary>
    [HttpPut("group/{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserGroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateUserGroupRequest r)
        => Ok(await _mediator.Send(new UpdateUserGroupCommand(
            id, r.Name, r.MemberUserCodes, r.IsActive, GetActingAdmin())));

    /// <summary>Create a functional group (R6). 409 on collision.</summary>
    [HttpPost("functional-group")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(FunctionalGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FunctionalGroupResponse>> CreateFunctionalGroup(
        [FromBody] CreateFunctionalGroupRequest r)
    {
        var result = await _mediator.Send(new CreateFunctionalGroupCommand(
            r.RoleCenterName, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetByCode), new { code = result.FunctionalGroupCode }, result);
    }

    /// <summary>Save access assignment for a user + LOB (R7/R8/R9/R10). 404 when missing.</summary>
    [HttpPut("{id:guid}/access")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccessResponse>> SaveAccess(Guid id, [FromBody] SaveUserAccessRequest r)
        => Ok(await _mediator.Send(new SaveUserAccessCommand(
            id, r.LineOfBusiness, r.Rows, r.BranchCodes, r.CopyProfile, GetActingAdmin())));

    /// <summary>Read access assignment for a user + LOB (R10 read / Access tab populate).</summary>
    [HttpGet("{id:guid}/access")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(UserAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserAccessResponse>> GetAccess(Guid id, [FromQuery] string lob)
        => Ok(await _mediator.Send(new GetUserAccessQuery(id, lob)));

    /// <summary>Audit trail for a record, newest-first (R14).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<UserManagementAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserManagementAuditEntryResponse>>> GetAudit(Guid id)
        => Ok(await _mediator.Send(new GetUserAuditTrailQuery(id)));

    // ---- Reference LOVs (consumed master data) ----

    /// <summary>Active users for group members + copy-profile source (R5.5/R10.1).</summary>
    [HttpGet("ref/active-users")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<UserGroupMemberResponse>>> GetActiveUsers([FromQuery] string? search)
        => Ok(await _mediator.Send(new GetActiveUsersQuery(search)));

    /// <summary>Admin-accessible, active, Role-Code-linked LOBs (R7.1/7.3).</summary>
    [HttpGet("ref/lines-of-business")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<ReferenceItemResponse>>> GetLinesOfBusiness()
        => Ok(await _mediator.Send(new GetAccessibleLinesOfBusinessQuery()));

    /// <summary>Active role centers, includes ALL (R8.1/9.5).</summary>
    [HttpGet("ref/role-centers")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<ReferenceItemResponse>>> GetRoleCenters()
        => Ok(await _mediator.Send(new GetRoleCentersQuery()));

    /// <summary>Programs (as access rows, flags false) for a role center (R8.3/8.4).</summary>
    [HttpGet("ref/role-centers/{name}/programs")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<AccessRightRow>>> GetRoleCenterPrograms(string name)
        => Ok(await _mediator.Send(new GetRoleCenterProgramsQuery(name)));

    /// <summary>Branch Location Tree for a LOB, includes ALL (R9.1-9.3).</summary>
    [HttpGet("ref/branch-tree")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<BranchTreeNodeResponse>>> GetBranchTree([FromQuery] string lob)
        => Ok(await _mediator.Send(new GetBranchLocationTreeQuery(lob)));

    /// <summary>Lookup LOVs: Designation | Department | UserType (R3.9/3.10/3.11).</summary>
    [HttpGet("ref/lookups")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<List<ReferenceItemResponse>>> GetLookups([FromQuery] string type)
        => Ok(await _mediator.Send(new GetUserLookupsQuery(type)));

    /// <summary>
    /// Resolves the acting admin id from the validated JWT principal so the audit actor cannot be
    /// spoofed by the request body. Prefers NameIdentifier (sub), falling back to Name.
    /// </summary>
    private string GetActingAdmin()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
