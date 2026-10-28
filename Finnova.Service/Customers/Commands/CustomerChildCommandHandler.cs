using MediatR;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;

namespace Finnova.Service.Customers.Commands;

public sealed class CustomerChildCommandHandler(
    ICustomerRepository customers,
    ICustomerUnitOfWork uow,
    ICustomerAuditRepository audits,
    ILookupReferenceResolver lookups,
    IDocumentDefinitionResolver documents,
    IIndiaAddressControl addresses,
    IAccountContractBoundary accountContract,
    ICustomerChildMapper mapper)
    : IRequestHandler<AddBankAccountCommand, BankAccountResponse>,
      IRequestHandler<ModifyBankAccountCommand, BankAccountResponse>,
      IRequestHandler<SetDefaultBankAccountCommand, BankAccountResponse>,
      IRequestHandler<ClearDefaultBankAccountCommand>,
      IRequestHandler<AddCustomerDocumentCommand, CustomerDocumentResponse>,
      IRequestHandler<ModifyCustomerDocumentCommand, CustomerDocumentResponse>,
      IRequestHandler<AddCustomerTrackCommand, CustomerTrackResponse>,
      IRequestHandler<ReleaseCustomerTrackCommand, CustomerTrackResponse>,
      IRequestHandler<UpdateCustomerCreditValidUpToCommand, CustomerCreditResponse>,
      IRequestHandler<AddFactoringSubLimitCommand, FactoringSubLimitResponse>,
      IRequestHandler<ModifyFactoringSubLimitCommand, FactoringSubLimitResponse>,
      IRequestHandler<CopyCommunicationAddressCommand, CopyCommunicationAddressResult>
{
    public Task<BankAccountResponse> Handle(AddBankAccountCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.BankChanged, ct, async customer =>
        {
            var accounts = customer.BankAccounts
                .Select(mapper.Request)
                .Append(x.Request)
                .ToArray();
            BankAccountRules.Validate(accounts);
            await mapper.AddBank(customer, x.Request, ct);
            return mapper.BankResponse(customer.BankAccounts[^1]);
        });

    public Task<BankAccountResponse> Handle(ModifyBankAccountCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.BankChanged, ct, async customer =>
        {
            var bank = customer.BankAccounts.FirstOrDefault(b => b.Id == x.BankAccountId)
                ?? throw new CustomerChildRecordNotFoundException("bank account");

            await mapper.ModifyBank(bank, x.Request, ct);
            BankAccountRules.Validate(customer.BankAccounts.Select(mapper.Request).ToArray());
            return mapper.BankResponse(bank);
        });

    public Task<BankAccountResponse> Handle(SetDefaultBankAccountCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.BankChanged, ct, customer =>
        {
            var bank = customer.BankAccounts.FirstOrDefault(b => b.Id == x.BankAccountId)
                ?? throw new CustomerChildRecordNotFoundException("bank account");

            return SetDefaultBankAsync(customer, bank);
        });

    public async Task Handle(ClearDefaultBankAccountCommand x, CancellationToken ct)
    {
        await InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.BankChanged, ct, async customer =>
        {
            await mapper.ClearDefault(customer);
            BankAccountRules.Validate(customer.BankAccounts.Select(mapper.Request).ToArray());
            return Unit.Value;
        });
    }

    public Task<CustomerDocumentResponse> Handle(AddCustomerDocumentCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.DocumentChanged, ct, async customer =>
        {
            var definitions = await documents.GetApplicableAsync(
                x.Request.ConstitutionCode,
                CustomerLookupTypes.EntityRole,
                ct);
            DocumentRules.ValidateApplicable(x.Request.DocumentDefinitionId, definitions);
            DocumentRules.RejectRawBinary(x.Request.Value);

            var row = mapper.AddDocument(customer, x.Request, definitions);
            return mapper.DocumentResponse(row);
        });

    public Task<CustomerDocumentResponse> Handle(ModifyCustomerDocumentCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.DocumentChanged, ct, async customer =>
        {
            var row = customer.Documents.FirstOrDefault(d => d.Id == x.DocumentId)
                ?? throw new CustomerChildRecordNotFoundException("document");
            var definitions = await documents.GetApplicableAsync(
                x.Request.ConstitutionCode,
                CustomerLookupTypes.EntityRole,
                ct);
            DocumentRules.ValidateApplicable(x.Request.DocumentDefinitionId, definitions);
            DocumentRules.RejectRawBinary(x.Request.Value);
            mapper.ModifyDocument(row, x.Request, definitions);
            return mapper.DocumentResponse(row);
        });

    public Task<CustomerTrackResponse> Handle(AddCustomerTrackCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.TrackChanged, ct, async customer =>
        {
            await accountContract.RequireModifyPrerequisiteAsync(customer.Id, x.Request.AccountNumber, ct);
            await lookups.ResolveActiveAsync(
                CustomerLookupTypes.Module,
                CustomerLookupTypes.TrackType,
                x.Request.TrackTypeCode,
                nameof(x.Request.TrackTypeCode),
                ct);

            var row = mapper.AddTrack(customer, x.Request);
            return mapper.TrackResponse(row);
        });

    public Task<CustomerTrackResponse> Handle(ReleaseCustomerTrackCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.TrackChanged, ct, async customer =>
        {
            var row = customer.Tracks.FirstOrDefault(t => t.Id == x.TrackId)
                ?? throw new CustomerChildRecordNotFoundException("track");
            await accountContract.RequireModifyPrerequisiteAsync(customer.Id, row.AccountNumber, ct);
            mapper.ReleaseTrack(row, x.ActorId);
            return mapper.TrackResponse(row);
        });

    public Task<CustomerCreditResponse> Handle(UpdateCustomerCreditValidUpToCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, null, ct, async customer =>
        {
            _ = await accountContract.GetCreditAsync(customer.Id, ct);
            CustomerPrerequisiteRules.RequireCreditValidUpToOnly(false);
            return await accountContract.UpdateValidUpToAsync(
                customer.Id,
                x.ValidUpTo,
                x.ActorId,
                ct);
        });

    public Task<FactoringSubLimitResponse> Handle(AddFactoringSubLimitCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.FactoringChanged, ct, customer =>
        {
            CustomerPrerequisiteRules.RequireFactoring(
                x.Request.FactoringApplicable,
                x.Request.FactoringTypeCode);
            var row = mapper.AddFactoring(customer, x.Request);
            return Task.FromResult(mapper.FactoringResponse(row));
        });

    public Task<FactoringSubLimitResponse> Handle(ModifyFactoringSubLimitCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.FactoringChanged, ct, customer =>
        {
            var row = customer.FactoringSubLimits.FirstOrDefault(r => r.Id == x.SubLimitId)
                ?? throw new CustomerChildRecordNotFoundException("factoring sub-limit");
            CustomerPrerequisiteRules.RequireFactoring(
                x.Request.FactoringApplicable,
                x.Request.FactoringTypeCode);
            mapper.ModifyFactoring(row, x.Request);
            return Task.FromResult(mapper.FactoringResponse(row));
        });

    public Task<CopyCommunicationAddressResult> Handle(CopyCommunicationAddressCommand x, CancellationToken ct)
        => InTransactionAsync(x.CustomerCode, x.ActorId, null, ct, async customer =>
        {
            var communication = customer.Addresses.SingleOrDefault(
                a => a.AddressType == CustomerAddressType.Communication)
                ?? throw new CustomerChildRecordNotFoundException("communication address");
            var communicationResponse = mapper.AddressResponse(communication);
            var permanent = await addresses.CopyCommunicationToPermanentAsync(communicationResponse, ct);
            mapper.ReplacePermanent(customer, permanent);
            return new CopyCommunicationAddressResult(customer.CustomerCode, permanent);
        });

    private async Task<BankAccountResponse> SetDefaultBankAsync(Customer customer, BankAccount bank)
    {
        await mapper.SetDefault(customer, bank);
        BankAccountRules.Validate(customer.BankAccounts.Select(mapper.Request).ToArray());
        return mapper.BankResponse(bank);
    }

    private async Task<T> InTransactionAsync<T>(
        string customerCode,
        string actorId,
        CustomerAuditAction? auditAction,
        CancellationToken ct,
        Func<Customer, Task<T>> action)
    {
        var customer = await Require(customerCode, ct);
        await using var transaction = await uow.BeginTransactionAsync(ct);
        try
        {
            var result = await action(customer);
            if (auditAction.HasValue)
                await audits.AddAsync(mapper.Audit(customer, actorId, auditAction.Value), ct);
            await uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Customer> Require(string code, CancellationToken ct)
        => await customers.GetByCodeAsync(code, ct)
            ?? throw new CustomerNotFoundException(code);
}
