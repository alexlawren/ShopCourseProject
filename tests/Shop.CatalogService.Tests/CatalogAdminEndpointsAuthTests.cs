using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.CatalogService.Controllers;
using Shop.CatalogService.Domain.Constants;

namespace Shop.CatalogService.Tests;

public class CatalogAdminEndpointsAuthTests
{
    [Fact]
    public void GetAdminCategories_RequiresAdminRole()
    {
        var method = typeof(CatalogController).GetMethod(nameof(CatalogController.GetAdminCategories));
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal(CatalogRoles.Admin, authorizeAttr.Roles);

        var httpGetAttr = method.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGetAttr);
        Assert.Equal("admin/categories", httpGetAttr.Template);
    }

    [Fact]
    public void GetAdminProducts_RequiresAdminRole()
    {
        var method = typeof(CatalogController).GetMethod(nameof(CatalogController.GetAdminProducts));
        Assert.NotNull(method);

        var authorizeAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal(CatalogRoles.Admin, authorizeAttr.Roles);

        var httpGetAttr = method.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGetAttr);
        Assert.Equal("admin/products", httpGetAttr.Template);
    }
}
