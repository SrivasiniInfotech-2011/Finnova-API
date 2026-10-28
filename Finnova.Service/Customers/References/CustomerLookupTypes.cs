// CREATE Finnova.Service/Customers/References/CustomerLookupTypes.cs
using Finnova.Models.Contracts.Customers;

namespace Finnova.Service.Customers.References;

public static class CustomerLookupTypes
{
    public const string Module = "<RESOLVE:LookupModule>";
    public const string Group = "<RESOLVE:GroupLookupType>";
    public const string Industry = "<RESOLVE:IndustryLookupType>";
    public const string Constitution = "<RESOLVE:ConstitutionLookupType>";
    public const string Title = "<RESOLVE:TitleLookupType>";
    public const string MaritalStatus = "MARITAL_STATUS"; // existing Origination/MARITAL_STATUS may be reused only if confirmed for Customer
    public const string CustomerBusinessType = "<RESOLVE:CustomerBusinessTypeLookupType>";
    public const string CustomerBusinessSubType = "<RESOLVE:CustomerBusinessSubTypeLookupType>";
    public const string IndustrySector = "<RESOLVE:IndustrySectorLookupType>";
    public const string SubSector = "<RESOLVE:SubSectorLookupType>";
    public const string TrackType = "<RESOLVE:TrackTypeLookupType>";
    public const string EntityRole = "<RESOLVE:CustomerDocumentEntityRole>";
}