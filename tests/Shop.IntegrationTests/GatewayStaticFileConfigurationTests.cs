
namespace Shop.IntegrationTests;

/// <summary>
/// Verifies that Shop.Gateway Program.cs registers all MIME type mappings
/// required for Blazor WebAssembly framework assets (.dat, .blat, .wasm, etc.).
/// Without these mappings, ASP.NET Core's static file middleware returns 404
/// for files with unrecognised extensions, breaking the Blazor bootstrap.
/// </summary>
public class GatewayStaticFileConfigurationTests
{
    private static string GetGatewayProgramSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ShopCourseProject.sln")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not locate solution directory containing ShopCourseProject.sln");
        }

        var programPath = Path.Combine(dir.FullName, "src", "Shop.Gateway", "Program.cs");
        if (!File.Exists(programPath))
        {
            throw new FileNotFoundException($"Could not find Shop.Gateway Program.cs at {programPath}");
        }

        return File.ReadAllText(programPath);
    }

    /// <summary>
    /// Blazor WASM runtime requires ICU .dat files and potentially .blat files.
    /// Without explicit MIME type mappings, UseStaticFiles returns 404 for these extensions.
    /// </summary>
    [Theory]
    [InlineData(".dat")]
    [InlineData(".blat")]
    [InlineData(".wasm")]
    public void GatewayProgram_RegistersMimeMapping_ForBlazorCriticalExtension(string extension)
    {
        var source = GetGatewayProgramSource();

        // Look for pattern like: provider.Mappings[".dat"] = "..."
        var pattern = $"""provider.Mappings["{extension}"]""";

        Assert.Contains(pattern, source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GatewayProgram_UsesCustomContentTypeProvider_InStaticFileOptions()
    {
        var source = GetGatewayProgramSource();

        Assert.Contains("FileExtensionContentTypeProvider", source);
        Assert.Contains("ContentTypeProvider = provider", source);
    }

    [Fact]
    public void GatewayProgram_HasFallbackToIndexHtml()
    {
        var source = GetGatewayProgramSource();

        Assert.Contains("MapFallbackToFile", source);
        Assert.Contains("index.html", source);
    }
}
