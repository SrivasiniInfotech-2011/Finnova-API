namespace Finnova.Models.Domain.Entities;

public sealed class BankAccount
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string? AccountTypeCode { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string? BankCode { get; private set; }
    public string? MICRCode { get; private set; }
    public DateOnly? ClearingDate { get; private set; }
    public string? Address { get; private set; }
    public string? ContractNumber { get; private set; }
    public string? SubContractNumber { get; private set; }
    public string NormalizedDuplicateKey { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        string? accountTypeCode, string accountNumber, string? bankCode, string? micrCode,
        DateOnly? clearingDate, string? address, string? contractNumber, string? subContractNumber,
        bool isDefault)
    {
        AccountTypeCode = accountTypeCode;
        AccountNumber = accountNumber;
        BankCode = bankCode;
        MICRCode = micrCode;
        ClearingDate = clearingDate;
        Address = address;
        ContractNumber = contractNumber;
        SubContractNumber = subContractNumber;
        NormalizedDuplicateKey = $"{bankCode?.Trim().ToUpperInvariant()}:{accountNumber.Trim().ToUpperInvariant()}";
        IsDefault = isDefault;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
