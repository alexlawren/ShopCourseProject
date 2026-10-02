namespace Shop.CatalogService.Application.Catalog.Queries;

public static class ProductQueryValidator
{
    public static IDictionary<string, string[]> Validate(ProductQueryParameters parameters)
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
        else if (parameters.PageSize > ProductQueryParameters.MaxPageSize)
        {
            AddError(errors, nameof(parameters.PageSize), $"PageSize must not exceed {ProductQueryParameters.MaxPageSize}.");
        }

        if (parameters.MinPrice.HasValue && parameters.MinPrice.Value < 0)
        {
            AddError(errors, nameof(parameters.MinPrice), "MinPrice must be non-negative.");
        }

        if (parameters.MaxPrice.HasValue && parameters.MaxPrice.Value < 0)
        {
            AddError(errors, nameof(parameters.MaxPrice), "MaxPrice must be non-negative.");
        }

        if (parameters.MinPrice.HasValue && parameters.MaxPrice.HasValue && parameters.MinPrice.Value > parameters.MaxPrice.Value)
        {
            AddError(errors, nameof(parameters.MinPrice), "MinPrice cannot be greater than MaxPrice.");
        }

        var sort = string.IsNullOrWhiteSpace(parameters.Sort)
            ? ProductQueryParameters.DefaultSort
            : parameters.Sort.Trim();

        if (!ProductQueryParameters.SupportedSorts.Contains(sort, StringComparer.OrdinalIgnoreCase))
        {
            AddError(errors, nameof(parameters.Sort),
                $"Sort '{parameters.Sort}' is not supported. Supported values: {string.Join(", ", ProductQueryParameters.SupportedSorts)}.");
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
