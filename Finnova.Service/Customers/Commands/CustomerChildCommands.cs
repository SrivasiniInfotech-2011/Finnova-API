using MediatR;
using Finnova.Models.Contracts.Customers;

namespace Finnova.Service.Customers.Commands;

public sealed record AddBankAccountCommand(string CustomerCode, BankAccountRequest Request, string ActorId) : IRequest<BankAccountResponse>;
public sealed record ModifyBankAccountCommand(string CustomerCode, Guid BankAccountId, BankAccountRequest Request, string ActorId) : IRequest<BankAccountResponse>;
public sealed record SetDefaultBankAccountCommand(string CustomerCode, Guid BankAccountId, string ActorId) : IRequest<BankAccountResponse>;
public sealed record ClearDefaultBankAccountCommand(string CustomerCode, string ActorId) : IRequest;
public sealed record AddCustomerDocumentCommand(string CustomerCode, CustomerDocumentReferenceRequest Request, string ActorId) : IRequest<CustomerDocumentResponse>;
public sealed record ModifyCustomerDocumentCommand(string CustomerCode, Guid DocumentId, CustomerDocumentReferenceRequest Request, string ActorId) : IRequest<CustomerDocumentResponse>;
public sealed record AddCustomerTrackCommand(string CustomerCode, CustomerTrackRequest Request, string ActorId) : IRequest<CustomerTrackResponse>;
public sealed record ReleaseCustomerTrackCommand(string CustomerCode, Guid TrackId, string ActorId) : IRequest<CustomerTrackResponse>;
public sealed record UpdateCustomerCreditValidUpToCommand(string CustomerCode, DateOnly? ValidUpTo, string ActorId) : IRequest<CustomerCreditResponse>;
public sealed record AddFactoringSubLimitCommand(string CustomerCode, FactoringSubLimitRequest Request, string ActorId) : IRequest<FactoringSubLimitResponse>;
public sealed record ModifyFactoringSubLimitCommand(string CustomerCode, Guid SubLimitId, FactoringSubLimitRequest Request, string ActorId) : IRequest<FactoringSubLimitResponse>;
public sealed record CopyCommunicationAddressCommand(string CustomerCode, string ActorId) : IRequest<CopyCommunicationAddressResult>;
