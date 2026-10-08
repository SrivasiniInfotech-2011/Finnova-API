using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class AssetMapper
{
    // ---- Class Code ----
    public static ClassCodeResponse ToResponse(this ClassCode x) =>
        new(x.Id, x.Code, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public static List<ClassCodeResponse> ToResponseList(this IEnumerable<ClassCode> items)
        => items.Select(i => i.ToResponse()).ToList();

    // ---- Make Code ----
    public static MakeCodeResponse ToResponse(this MakeCode x) =>
        new(x.Id, x.Code, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public static List<MakeCodeResponse> ToResponseList(this IEnumerable<MakeCode> items)
        => items.Select(i => i.ToResponse()).ToList();

    // ---- Type Code ----
    public static TypeCodeResponse ToResponse(this Finnova.Models.Domain.Entities.TypeCode x) =>
        new(x.Id, x.Code, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public static List<TypeCodeResponse> ToResponseList(this IEnumerable<Finnova.Models.Domain.Entities.TypeCode> items)
        => items.Select(i => i.ToResponse()).ToList();

    // ---- Model Code ----
    public static ModelCodeResponse ToResponse(this ModelCode x) =>
        new(x.Id, x.Code, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt);
    public static List<ModelCodeResponse> ToResponseList(this IEnumerable<ModelCode> items)
        => items.Select(i => i.ToResponse()).ToList();

    // ---- Slim list item (shared across the four masters) ----
    public static CodeListItemResponse ToListItem(this ClassCode x) => new(x.Id, x.Code, x.Description);
    public static CodeListItemResponse ToListItem(this MakeCode x) => new(x.Id, x.Code, x.Description);
    public static CodeListItemResponse ToListItem(this Finnova.Models.Domain.Entities.TypeCode x) => new(x.Id, x.Code, x.Description);
    public static CodeListItemResponse ToListItem(this ModelCode x) => new(x.Id, x.Code, x.Description);

    // ---- Asset ----
    /// <summary>Maps an asset, projecting resolved Class/Type code strings for the grid (R10.1).
    /// Pass the resolved code strings explicitly, or rely on loaded navigation properties.</summary>
    public static AssetResponse ToResponse(this Asset x, string? classCodeValue = null, string? typeCodeValue = null) =>
        new(
            x.Id,
            x.AssetCode,
            x.Description,
            x.ClassCodeId,
            classCodeValue ?? x.ClassCode?.Code,
            x.TypeCodeId,
            typeCodeValue ?? x.TypeCode?.Code,
            x.MakeCodeId,
            x.ModelCodeId,
            x.BookDepreciationCategory,
            x.BookDepreciationRate,
            x.StockDepreciationCategory,
            x.StockDepreciationRate,
            x.GuidelineLimit,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt);

    public static List<AssetResponse> ToResponseList(this IEnumerable<Asset> items)
        => items.Select(i => i.ToResponse()).ToList();
}
