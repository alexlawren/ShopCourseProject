using System.ComponentModel.DataAnnotations;
using Shop.OrderService.Application.Cart.Dtos;

namespace Shop.OrderService.Tests;

public class CartDtoValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        Validator.TryValidateObject(model, context, validationResults, validateAllProperties: true);
        return validationResults;
    }

    [Fact]
    public void AddCartItemRequest_Valid_PassesValidation()
    {
        var request = new AddCartItemRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 5
        };

        var errors = ValidateModel(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void AddCartItemRequest_EmptyProductId_FailsValidation()
    {
        var request = new AddCartItemRequest
        {
            ProductId = Guid.Empty,
            Quantity = 1
        };

        var errors = ValidateModel(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AddCartItemRequest.ProductId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void AddCartItemRequest_QuantityZeroOrNegative_FailsValidation(int quantity)
    {
        var request = new AddCartItemRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = quantity
        };

        var errors = ValidateModel(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AddCartItemRequest.Quantity)));
    }

    [Fact]
    public void AddCartItemRequest_QuantityGreaterThan1000_FailsValidation()
    {
        var request = new AddCartItemRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 1001
        };

        var errors = ValidateModel(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(AddCartItemRequest.Quantity)));
    }

    [Fact]
    public void UpdateCartItemRequest_Valid_PassesValidation()
    {
        var request = new UpdateCartItemRequest
        {
            Quantity = 10
        };

        var errors = ValidateModel(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void UpdateCartItemRequest_QuantityZeroOrNegative_FailsValidation(int quantity)
    {
        var request = new UpdateCartItemRequest
        {
            Quantity = quantity
        };

        var errors = ValidateModel(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UpdateCartItemRequest.Quantity)));
    }

    [Fact]
    public void UpdateCartItemRequest_QuantityGreaterThan1000_FailsValidation()
    {
        var request = new UpdateCartItemRequest
        {
            Quantity = 1001
        };

        var errors = ValidateModel(request);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UpdateCartItemRequest.Quantity)));
    }

    [Fact]
    public void CartDto_TotalQuantity_CalculatesSumOfAllItemQuantities()
    {
        var cart = new CartDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = new[]
            {
                new CartItemDto(Guid.NewGuid(), 3),
                new CartItemDto(Guid.NewGuid(), 7),
                new CartItemDto(Guid.NewGuid(), 1)
            }
        };

        Assert.Equal(11, cart.TotalQuantity);
    }

    [Fact]
    public void CartDto_TotalQuantity_EmptyItems_ReturnsZero()
    {
        var cart = new CartDto
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items = Array.Empty<CartItemDto>()
        };

        Assert.Equal(0, cart.TotalQuantity);
    }
}
