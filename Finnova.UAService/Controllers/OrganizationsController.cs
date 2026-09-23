using MediatR;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Organizations;
using Finnova.Service.Organizations.Commands.CreateOrganization;
using Finnova.Service.Organizations.Commands.UpdateOrganization;
using Finnova.Service.Organizations.Queries.GetCurrentOrganization;
using Finnova.Service.Organizations.Queries.GetOrganizationById;

namespace Finnova.UAService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrganizationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the single company for this product instance.
    /// The product supports exactly one organization.
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<OrganizationResponse>> GetCurrent()
    {
        var result = await _mediator.Send(new GetCurrentOrganizationQuery());
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetOrganizationByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<OrganizationResponse>> Create([FromBody] CreateOrganizationRequest request)
    {
        var command = new CreateOrganizationCommand(
            request.Code, request.Name, request.ConstitutionType,
            request.Description, request.CeoName, request.RegistrationDate,
            request.RegistrationNumber, request.PanNumber, request.GstNumber,
            request.CorporateAddress, request.CorporateCity, request.CorporateState,
            request.CorporateCountry, request.CorporatePincode,
            request.CommunicationAddress, request.CommunicationCity, request.CommunicationState,
            request.CommunicationCountry, request.CommunicationPincode,
            request.Telephone, request.Mobile, request.Email, request.Website,
            request.AccountingCurrency);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> Update(Guid id, [FromBody] UpdateOrganizationRequest request)
    {
        var command = new UpdateOrganizationCommand(
            id, request.Code, request.Name, request.ConstitutionType,
            request.Description, request.CeoName, request.RegistrationDate,
            request.RegistrationNumber, request.PanNumber, request.GstNumber,
            request.CorporateAddress, request.CorporateCity, request.CorporateState,
            request.CorporateCountry, request.CorporatePincode,
            request.CommunicationAddress, request.CommunicationCity, request.CommunicationState,
            request.CommunicationCountry, request.CommunicationPincode,
            request.Telephone, request.Mobile, request.Email, request.Website,
            request.AccountingCurrency, request.Status);
        var result = await _mediator.Send(command);
        return result is null ? NotFound() : Ok(result);
    }
}
