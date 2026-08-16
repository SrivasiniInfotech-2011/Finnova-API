using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Users;
using Finnova.Service.Users.Commands.CreateUser;
using Finnova.Service.Users.Commands.UpdateUser;
using Finnova.Service.Users.Queries.GetAllUsers;
using Finnova.Service.Users.Queries.GetUserById;

namespace Finnova.UAService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAll()
    {
        var result = await _mediator.Send(new GetAllUsersQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand(
            request.FirstName, request.MiddleName, request.LastName,
            request.Email, request.Phone, request.Role, request.OrganizationId);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(
            id, request.FirstName, request.MiddleName, request.LastName,
            request.Email, request.Phone, request.Role, request.Status);
        var result = await _mediator.Send(command);
        return result is null ? NotFound() : Ok(result);
    }
}
