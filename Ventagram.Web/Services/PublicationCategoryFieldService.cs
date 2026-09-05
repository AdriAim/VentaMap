using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationCategoryFieldService(VentagramDbContext db)
{
    private static readonly string[] OperationOptionOrder = Enum.GetValues<PublicationOperationType>()
        .OrderBy(x => (byte)x)
        .Select(x => x.ToDisplayName())
        .ToArray();

    public async Task<List<PublicationCategoryField>> GetRequiredActiveByGroupAsync(PublicationGroup group)
    {
        var groupId = (byte)group;

        return await db.PublicationCategoryFields
            .AsNoTracking()
            .Where(x => x.IsActive
                && x.Required
                && (x.GroupId == groupId || x.GroupId == null)
                && (x.CategoryId == null || (x.Category != null && x.Category.Group == group)))
            .OrderByDescending(x => x.Required)
            .ThenByDescending(x => x.ShowInBasicData)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    public async Task<PublicationCategoryField?> GetOperationFilterForAllGroupsAsync()
    {
        var operationFields = await db.PublicationCategoryFields
            .AsNoTracking()
            .Where(x => x.IsActive
                && x.Required
                && x.InternalName.ToLower() == "operacion")
            .ToListAsync();

        if (operationFields.Count == 0)
        {
            return new PublicationCategoryField
            {
                InternalName = "operacion",
                Label = "Tipo de operacion",
                DataType = PublicationCategoryFieldDataType.Lista,
                Required = true,
                SortOrder = 1,
                OptionsCsv = string.Join(",", OperationOptionOrder)
            };
        }

        var configuredOptions = operationFields
            .SelectMany(x => SplitOptions(x.OptionsCsv))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderedOptions = OperationOptionOrder
            .Where(configuredOptions.Contains)
            .Concat(configuredOptions
                .Where(x => !OperationOptionOrder.Contains(x, StringComparer.OrdinalIgnoreCase))
                .OrderBy(x => x))
            .ToArray();

        return new PublicationCategoryField
        {
            Id = operationFields.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).First().Id,
            InternalName = "operacion",
            Label = operationFields.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Label))?.Label ?? "Tipo de operacion",
            DataType = PublicationCategoryFieldDataType.Lista,
            Required = true,
            SortOrder = 1,
            OptionsCsv = string.Join(",", orderedOptions)
        };
    }

    public async Task<List<PublicationCategoryField>> GetActiveByCategoryIdAsync(int categoryId)
    {
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == categoryId);

        return await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == categoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    public Task<List<PublicationCategoryField>> GetActiveDefinitionsForCategoryAsync(int categoryId)
    {
        return GetActiveByCategoryIdAsync(categoryId);
    }

    private static string[] SplitOptions(string? optionsCsv) => string.IsNullOrWhiteSpace(optionsCsv)
        ? []
        : optionsCsv.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
