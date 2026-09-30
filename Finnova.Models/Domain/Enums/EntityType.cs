namespace Finnova.Models.Domain.Enums;

/// <summary>The category of party an entity record represents (FINNOVA-11 R1.5).</summary>
public enum EntityType
{
    Dealer = 0,
    DebtCollector = 1,
    Insurer = 2,
    Supplier = 3,
    Employer = 4
}