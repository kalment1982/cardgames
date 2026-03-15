using Microsoft.Extensions.FileProviders;
using TractorGame.Core.Logging;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var repoRoot = ResolveRepoRoot();
var staticRoot = Path.Combine(repoRoot, "WebUI", "wwwroot");
if (!Directory.Exists(staticRoot))
{
    throw new DirectoryNotFoundException($"WebUI static files not found: {staticRoot}. Build WebUI first.");
}
var frameworkRoot = ResolveFrameworkRoot(repoRoot);
var stylesPath = ResolveStylesPath(repoRoot);

var fileProvider = new PhysicalFileProvider(staticRoot);
var logger = GameLoggerFactory.CreateDefault();

app.MapPost("/api/log-entry", (LogEntry entry) =>
{
    if (entry.TsUtc == default)
        entry.TsUtc = DateTime.UtcNow;

    if (string.IsNullOrWhiteSpace(entry.Category))
        entry.Category = LogCategories.Diag;

    if (string.IsNullOrWhiteSpace(entry.Level))
        entry.Level = LogLevels.Info;

    if (string.IsNullOrWhiteSpace(entry.Event))
        entry.Event = "ui.unknown";

    logger.Log(entry);
    return Results.Ok(new { accepted = true });
});

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = fileProvider
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = fileProvider
});

if (Directory.Exists(frameworkRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frameworkRoot),
        RequestPath = "/_framework",
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream"
    });
}

if (File.Exists(stylesPath))
{
    app.MapGet("/WebUI.styles.css", async context =>
    {
        context.Response.ContentType = "text/css; charset=utf-8";
        await context.Response.SendFileAsync(stylesPath);
    });
}

app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(staticRoot, "index.html"));
});

app.Run();

static string ResolveRepoRoot()
{
    var fromCurrent = TryFindRepoRoot(Directory.GetCurrentDirectory());
    if (!string.IsNullOrWhiteSpace(fromCurrent))
        return fromCurrent;

    var fromBase = TryFindRepoRoot(AppContext.BaseDirectory);
    if (!string.IsNullOrWhiteSpace(fromBase))
        return fromBase;

    return Directory.GetCurrentDirectory();
}

static string? TryFindRepoRoot(string startPath)
{
    var dir = new DirectoryInfo(Path.GetFullPath(startPath));
    while (dir != null)
    {
        var hasProjectFile = File.Exists(Path.Combine(dir.FullName, "TractorGame.csproj"));
        var hasSolutionFile = File.Exists(Path.Combine(dir.FullName, "tractor.sln"));
        if (hasProjectFile || hasSolutionFile)
            return dir.FullName;

        dir = dir.Parent;
    }

    return null;
}

static string ResolveFrameworkRoot(string repoRoot)
{
    var debugPath = Path.Combine(repoRoot, "WebUI", "bin", "Debug", "net6.0", "wwwroot", "_framework");
    if (Directory.Exists(debugPath))
        return debugPath;

    var releasePath = Path.Combine(repoRoot, "WebUI", "bin", "Release", "net6.0", "wwwroot", "_framework");
    if (Directory.Exists(releasePath))
        return releasePath;

    return debugPath;
}

static string ResolveStylesPath(string repoRoot)
{
    var debugPath = Path.Combine(repoRoot, "WebUI", "obj", "Debug", "net6.0", "scopedcss", "bundle", "WebUI.styles.css");
    if (File.Exists(debugPath))
        return debugPath;

    var releasePath = Path.Combine(repoRoot, "WebUI", "obj", "Release", "net6.0", "scopedcss", "bundle", "WebUI.styles.css");
    if (File.Exists(releasePath))
        return releasePath;

    return debugPath;
}
