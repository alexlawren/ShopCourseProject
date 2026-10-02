using System.ComponentModel.DataAnnotations;
using Shop.CatalogService.Application.Catalog.Dtos;

namespace Shop.CatalogService.Tests;

public class AdminDtoValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData("laptops")]
    [InlineData("gaming-laptops")]
    [InlineData("lenovo-thinkpad-2026")]
    [InlineData("a")]
    [InlineData("123-abc")]
    public void CreateCategoryRequest_ValidSlug_PassesValidation(string slug)
    {
        var request = new CreateCategoryRequest
        {
            Name = "Laptops",
            Slug = slug
        };

        var errors = ValidateModel(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void CreateCategoryRequest_EmptyName_FailsValidation(string? name)
    {
        var request = new CreateCategoryRequest
        {
            Name = name!,
            Slug = "laptops"
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCategoryRequest.Name)));
    }

    [Fact]
    public void CreateCategoryRequest_NameExceedsMaxLength_FailsValidation()
    {
        var request = new CreateCategoryRequest
        {
            Name = new string('a', 121),
            Slug = "laptops"
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCategoryRequest.Name)));
    }

    [Theory]
    [InlineData("Laptops")] // uppercase
    [InlineData("gaming laptops")] // spaces
    [InlineData("laptops!")] // special characters
    [InlineData("-laptops")] // leading hyphen
    [InlineData("laptops-")] // trailing hyphen
    [InlineData("lap--tops")] // double hyphen
    [InlineData("")]
    public void CreateCategoryRequest_InvalidSlug_FailsValidation(string slug)
    {
        var request = new CreateCategoryRequest
        {
            Name = "Laptops",
            Slug = slug
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCategoryRequest.Slug)));
    }

    [Fact]
    public void CreateCategoryRequest_SlugExceedsMaxLength_FailsValidation()
    {
        var request = new CreateCategoryRequest
        {
            Name = "Laptops",
            Slug = new string('a', 141)
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCategoryRequest.Slug)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.50)]
    [InlineData(9999.99)]
    public void CreateProductRequest_ValidModel_PassesValidation(double price)
    {
        var request = new CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = "Valid Product",
            Description = "A valid product description",
            Price = (decimal)price,
            StockQuantity = 10
        };

        var errors = ValidateModel(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void CreateProductRequest_EmptyName_FailsValidation()
    {
        var request = new CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = "",
            Price = 100m,
            StockQuantity = 5
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProductRequest.Name)));
    }

    [Fact]
    public void CreateProductRequest_NameExceedsMaxLength_FailsValidation()
    {
        var request = new CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = new string('x', 201),
            Price = 100m,
            StockQuantity = 5
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProductRequest.Name)));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-50)]
    public void CreateProductRequest_NegativePrice_FailsValidation(double price)
    {
        var request = new CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = "Product",
            Price = (decimal)price,
            StockQuantity = 5
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProductRequest.Price)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateProductRequest_NegativeStockQuantity_FailsValidation(int stockQuantity)
    {
        var request = new CreateProductRequest
        {
            CategoryId = Guid.NewGuid(),
            Name = "Product",
            Price = 100m,
            StockQuantity = stockQuantity
        };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateProductRequest.StockQuantity)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void UpdateStockRequest_ValidQuantity_PassesValidation(int quantity)
    {
        var request = new UpdateStockRequest { Quantity = quantity };

        var errors = ValidateModel(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void UpdateStockRequest_NegativeQuantity_FailsValidation(int quantity)
    {
        var request = new UpdateStockRequest { Quantity = quantity };

        var errors = ValidateModel(request);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UpdateStockRequest.Quantity)));
    }
}
