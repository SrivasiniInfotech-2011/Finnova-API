namespace Finnova.Models.Domain.Enums;

public enum AppropriationDirection
{
    Vertical = 0,
    Horizontal = 1
}

public enum AppropriationDueType
{
    Installment = 0,
    Principal = 1,
    Interest = 2,
    Insurance = 3,
    Odi = 4,
    BankCharges = 5,
    ChequeReturnCharges = 6,
    LegalCharges = 7
}

public enum AppropriationRuleOperation
{
    Created = 0,
    Updated = 1,
    Activated = 2,
    Deactivated = 3
}