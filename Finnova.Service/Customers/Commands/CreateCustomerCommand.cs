using FluentValidation;
using MediatR;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;

namespace Finnova.Service.Customers.Commands;

public sealed record CreateCustomerCommand(CustomerCreateRequest Request, string ActorId) : IRequest<CustomerResponse>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Request.CompanyId).NotEmpty();
        RuleFor(x => x.Request.CustomerTypeCode).NotEmpty();
        RuleFor(x => x.Request.GroupCode)
            .Must(x => x is null || System.Text.RegularExpressions.Regex.IsMatch(x, "^\\d{1,4}$"));
        RuleFor(x => x.Request.IndustryCode).MaximumLength(3);
        RuleFor(x => x.Request.Addresses).NotNull();
    }
}

public sealed class CreateCustomerCommandHandler(
    ICustomerRepository customers,
    ICustomerAuditRepository audits,
    ICustomerUnitOfWork uow,
    ICustomerMasterReferenceValidator references,
    IDocumentNumberGenerator customerCodeGenerator,
    ICustomerAggregateMapper mapper) : IRequestHandler<CreateCustomerCommand, CustomerResponse>
{
    public async Task<CustomerResponse> Handle(CreateCustomerCommand message, CancellationToken ct)
    {
        CustomerFieldRules.NumericCode(message.Request.GroupCode, nameof(message.Request.GroupCode), 4);
        CustomerFieldRules.Length(message.Request.IndustryCode, nameof(message.Request.IndustryCode), 3);
        BankAccountRules.Validate(message.Request.BankAccounts);

        var refs = await references.ValidateAsync(message.Request, ct);
        var code = await customerCodeGenerator.GenerateAsync("CUSTOMER", 12, ct);
        CustomerCodeRules.ValidateGenerated(code);
        if (await customers.ExistsByCodeAsync(code, ct))
            throw new CustomerCodeConflictException();

        var identity = mapper.NormalizeIdentity(message.Request);
        DuplicateIdentityRules.RejectIfExisting(await customers.ExistsByIdentityAsync(identity, ct));

        await using var transaction = await uow.BeginTransactionAsync(ct);
        try
        {
            var customer = mapper.Create(message.Request, code, refs);
            await customers.AddAsync(customer, ct);
            await audits.AddAsync(mapper.CreatedAudit(customer, message.ActorId), ct);
            await uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return mapper.Response(customer, refs);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
