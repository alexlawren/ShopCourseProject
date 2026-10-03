var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5287", "https://localhost:7177"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .WithHeaders("Authorization", "Content-Type", "x-requested-with", "x-signalr-user-agent");
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var hasStaticWebRoot = Directory.Exists(wwwrootPath);
if (hasStaticWebRoot)
{
    var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    provider.Mappings[".wasm"] = "application/wasm";
    provider.Mappings[".br"] = "application/octet-stream";
    provider.Mappings[".gz"] = "application/octet-stream";
    provider.Mappings[".pdb"] = "application/octet-stream";

    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = provider
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Gateway" }));

app.UseWebSockets();

app.MapReverseProxy();

if (hasStaticWebRoot && File.Exists(Path.Combine(wwwrootPath, "index.html")))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program;
