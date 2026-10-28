using Finnova.Models.Contracts.Customers;

namespace Finnova.Service.Customers.References;

public sealed record LookupReferenceProjection(string Module, string LookupType, string Code, string Value);
public sealed record NationalityReferenceProjection(string Code, string Name);
public sealed record GLPostingGroupProjection(Guid Id, Guid CompanyId, string Code, string Name);
public sealed record DocumentDefinitionProjection(Guid Id, string Code, string? Category, string? Description, bool IsMandatory, bool? IsImageRequired);
public sealed record AddressControlResult(bool IsValid, IReadOnlyCollection<string> Errors, CustomerAddressResponse? ApprovedAddress);
