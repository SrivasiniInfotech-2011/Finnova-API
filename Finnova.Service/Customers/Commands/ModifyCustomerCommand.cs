using FluentValidation;
using MediatR;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;

namespace Finnova.Service.Customers.Commands;

public sealed record ModifyCustomerCommand(CustomerModifyRequest Request, string ActorId) : IRequest<CustomerResponse>;

public sealed class ModifyCustomerCommandValidator : AbstractValidator<ModifyCustomerCommand>
{
    public ModifyCustomerCommandValidator()
    {
        RuleFor(x => x.Request.CustomerCode).Length(12);
        RuleFor(x => x.Request.CompanyId).NotEmpty();
    }
}

public sealed class ModifyCustomerCommandHandler(
    ICustomerRepository customers,
    ICustomerAuditRepository audits,
    ICustomerUnitOfWork uow,
    ICustomerMasterReferenceValidator references,
    ICustomerAggregateMapper mapper) : IRequestHandler<ModifyCustomerCommand, CustomerResponse>
{
    public async Task<CustomerResponse> Handle(ModifyCustomerCommand message, CancellationToken ct)
    {
        var current = await customers.GetByCodeAsync(message.Request.CustomerCode, ct)
            ?? throw new CustomerNotFoundException(message.Request.CustomerCode);

        CustomerCodeRules.EnsureImmutable(current.CustomerCode, message.Request.CustomerCode);
        if (current.CompanyId != message.Request.CompanyId)
            throw new CustomerNotFoundException(message.Request.CustomerCode);

        CustomerFieldRules.NumericCode(message.Request.GroupCode, nameof(message.Request.GroupCode), 4);
        CustomerFieldRules.Length(message.Request.IndustryCode, nameof(message.Request.IndustryCode), 3);
        BankAccountRules.Validate(message.Request.BankAccounts);

        var currentProjection = mapper.Response(
            current,
            new MasterReferenceValidationResult(
                new Dictionary<string, CustomerReferenceProjection>(),
                Array.Empty<CustomerDocumentResponse>(),
                null,
                null));
        var refs = await references.ValidateAsync(message.Request, currentProjection, ct);

        await using var transaction = await uow.BeginTransactionAsync(ct);
        try
        {
            mapper.ApplyModify(current, message.Request, refs);
            await audits.AddAsync(mapper.ModifiedAudit(current, message.ActorId), ct);
            await uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return mapper.Response(current, refs);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
