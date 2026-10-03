namespace Shop.CatalogService.Application.Catalog.Queries;

public static class AdminProductQueryValidator
{
    public static IDictionary<string, string[]> Validate(AdminProductQueryParameters parameters)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (parameters.Page < 1)
        {
            AddError(errors, nameof(parameters.Page), "Page must be greater than or equal to 1.");
        }

        if (parameters.PageSize < 1)
        {
            AddError(errors, nameof(parameters.PageSize), "PageSize must be greater than or equal to 1.");
        }
        else if (parameters.PageSize > AdminProductQueryParameters.MaxPageSize)
        {
            AddError(errors, nameof(parameters.PageSize), $"PageSize must not exceed {AdminProductQueryParameters.MaxPageSize}.");
        }

        var sort = string.IsNullOrWhiteSpace(parameters.Sort)
            ? AdminProductQueryParameters.DefaultSort
            : parameters.Sort.Trim();

        if (!AdminProductQueryParameters.SupportedSorts.Contains(sort, StringComparer.OrdinalIgnoreCase))
        {
            AddError(errors, nameof(parameters.Sort),
                $"Sort '{parameters.Sort}' is not supported. Supported values: {string.Join(", ", AdminProductQueryParameters.SupportedSorts)}.");
        }

        return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());

        static void AddError(Dictionary<string, List<string>> dict, string key, string message)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = [];
                dict[key] = list;
            }
            list.Add(message);
        }
    }
}
