using System.Text.Json;

namespace Shop.IntegrationTests;

public class GatewayConfigurationTests
{
    private static JsonElement GetReverseProxySection()
    {
        // Search upwards for solution directory
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ShopCourseProject.sln")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate solution directory containing ShopCourseProject.sln");
        }

        var configPath = Path.Combine(dir.FullName, "src", "Shop.Gateway", "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Could not find Shop.Gateway appsettings.json at {configPath}");
        }

        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("ReverseProxy").Clone();
    }

    [Fact]
    public void AllRequiredRoutes_Exist_AndMappedToCorrectClusters()
    {
        var reverseProxy = GetReverseProxySection();
        var routes = reverseProxy.GetProperty("Routes");

        var expectedMappings = new Dictionary<string, (string ClusterId, string Path)>
        {
            ["identity-route"] = ("identity-cluster", "/api/auth/{**catch-all}"),
            ["catalog-route"] = ("catalog-cluster", "/api/catalog/{**catch-all}"),
            ["product-images-route"] = ("catalog-cluster", "/product-images/{**catch-all}"),
            ["catalog-hub-base"] = ("catalog-cluster", "/hubs/catalog"),
            ["catalog-hub-route"] = ("catalog-cluster", "/hubs/catalog/{**catch-all}"),
            ["cart-base"] = ("order-cluster", "/api/cart"),
            ["cart-route"] = ("order-cluster", "/api/cart/{**catch-all}"),
            ["orders-base"] = ("order-cluster", "/api/orders"),
            ["orders-route"] = ("order-cluster", "/api/orders/{**catch-all}"),
            ["admin-orders-base"] = ("order-cluster", "/api/admin/orders"),
            ["admin-orders-route"] = ("order-cluster", "/api/admin/orders/{**catch-all}"),
            ["order-hub-base"] = ("order-cluster", "/hubs/orders"),
            ["order-hub-route"] = ("order-cluster", "/hubs/orders/{**catch-all}")
        };

        foreach (var (routeName, (expectedCluster, expectedPath)) in expectedMappings)
        {
            Assert.True(routes.TryGetProperty(routeName, out var routeElement),
                $"Expected route '{routeName}' not found in configuration.");

            var clusterId = routeElement.GetProperty("ClusterId").GetString();
            Assert.Equal(expectedCluster, clusterId);

            var path = routeElement.GetProperty("Match").GetProperty("Path").GetString();
            Assert.Equal(expectedPath, path);
        }
    }

    [Fact]
    public void Clusters_HaveValidDestinationAddresses_EndingWithSlash()
    {
        var reverseProxy = GetReverseProxySection();
        var clusters = reverseProxy.GetProperty("Clusters");

        var expectedClusters = new[] { "identity-cluster", "catalog-cluster", "order-cluster" };

        foreach (var clusterName in expectedClusters)
        {
            Assert.True(clusters.TryGetProperty(clusterName, out var clusterElement),
                $"Expected cluster '{clusterName}' not found in configuration.");

            var destinations = clusterElement.GetProperty("Destinations");
            Assert.True(destinations.EnumerateObject().Any(),
                $"Cluster '{clusterName}' has no destinations configured.");

            foreach (var dest in destinations.EnumerateObject())
            {
                var address = dest.Value.GetProperty("Address").GetString();
                Assert.NotNull(address);
                Assert.True(address.EndsWith("/"),
                    $"Destination '{dest.Name}' in cluster '{clusterName}' must end with '/': {address}");
            }
        }
    }

    [Fact]
    public void InternalCatalogGrpcPort_IsNotExposedInAnyClusterOrRoute()
    {
        var reverseProxy = GetReverseProxySection();
        var rawJson = reverseProxy.GetRawText();

        // 5059 is internal cleartext gRPC port for StockReservationService
        Assert.DoesNotContain("5059", rawJson);
        Assert.DoesNotContain("StockReservation", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grpc", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoGenericCatchAllRoute_Exists()
    {
        var reverseProxy = GetReverseProxySection();
        var routes = reverseProxy.GetProperty("Routes");

        foreach (var route in routes.EnumerateObject())
        {
            var path = route.Value.GetProperty("Match").GetProperty("Path").GetString();
            Assert.NotNull(path);

            Assert.False(path == "/" || path == "/{**catch-all}" || path == "/{*catch-all}",
                $"Route '{route.Name}' is an overly permissive generic catch-all: {path}");
        }
    }
}
