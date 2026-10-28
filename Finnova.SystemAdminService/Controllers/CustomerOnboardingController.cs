using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Finnova.Models.Contracts.Customers;
using Finnova.Service.Customers.Commands;
using Finnova.Service.Customers.Queries;
namespace Finnova.SystemAdminService.Controllers;
[ApiController, Authorize(Policy = "SystemAdmin"), Route("api/customer-onboarding")]
public sealed class CustomerOnboardingController(ISender sender) : ControllerBase
{
    [HttpPost] public Task<CustomerResponse> Create(CustomerCreateRequest request, CancellationToken ct) => sender.Send(new CreateCustomerCommand(request, User.Identity?.Name ?? "system"), ct);
    [HttpPut("{customerCode}")] public Task<CustomerResponse> Modify(string customerCode, CustomerModifyRequest request, CancellationToken ct) => sender.Send(new ModifyCustomerCommand(request with { CustomerCode = customerCode }, User.Identity?.Name ?? "system"), ct);
    [HttpGet] public Task<IReadOnlyCollection<CustomerResponse>> Query([FromQuery] CustomerQueryRequest request, CancellationToken ct) => sender.Send(new QueryCustomer(request), ct);
    [HttpPost("{customerCode}/banks")] public Task<BankAccountResponse> AddBank(string customerCode, BankAccountRequest request, CancellationToken ct) => sender.Send(new AddBankAccountCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
    [HttpPost("{customerCode}/banks/{bankId}/default")] public Task<BankAccountResponse> DefaultBank(string customerCode, Guid bankId, CancellationToken ct) => sender.Send(new SetDefaultBankAccountCommand(customerCode, bankId, User.Identity?.Name ?? "system"), ct);
    [HttpPost("{customerCode}/documents")] public Task<CustomerDocumentResponse> AddDocument(string customerCode, CustomerDocumentReferenceRequest request, CancellationToken ct) => sender.Send(new AddCustomerDocumentCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
    [HttpPost("{customerCode}/tracks")] public Task<CustomerTrackResponse> AddTrack(string customerCode, CustomerTrackRequest request, CancellationToken ct) => sender.Send(new AddCustomerTrackCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
    [HttpPost("{customerCode}/addresses/copy-communication")] public Task<CopyCommunicationAddressResult> CopyAddress(string customerCode, CancellationToken ct) => sender.Send(new CopyCommunicationAddressCommand(customerCode, User.Identity?.Name ?? "system"), ct);
    [HttpPatch("{customerCode}/credit/valid-up-to")] public Task<CustomerCreditResponse> UpdateCredit(string customerCode, DateOnly? validUpTo, CancellationToken ct) => sender.Send(new UpdateCustomerCreditValidUpToCommand(customerCode, validUpTo, User.Identity?.Name ?? "system"), ct);
    [HttpPost("{customerCode}/factoring-sub-limits")] public Task<FactoringSubLimitResponse> AddFactoring(string customerCode, FactoringSubLimitRequest request, CancellationToken ct) => sender.Send(new AddFactoringSubLimitCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
}
