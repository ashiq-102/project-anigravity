using ChartCreationMCPServer.Execution;
using ChartCreationMCPServer.Storage;
using ChartCreationMCPServer.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Configuration ──────────────────────────────────────────────────────────
var azureConfig = builder.Configuration.GetSection("AzureStorage");

// ── 2. Prepare the Python environment ─────────────────────────────────────────
// Detects Python, installs anything missing when configured to, and returns a verdict.
var python = await PythonEnvironmentSetup.EnsureReadyAsync(builder.Configuration.GetSection("Python"));
PythonEnvironmentSetup.Report(python);
var pythonInfo = python.Info;

// ── 3. Register storage dependencies and MCP tools's dependencies and MCP tools as singletons ────────────────────────
builder.Services.AddSingleton<IStorageStore>(sp =>
{
    var connectionString = azureConfig["ConnectionString"] ?? throw new InvalidOperationException("AzureStorage:ConnectionString is required.");
    var inputContainer = azureConfig["InputContainer"] ?? "input-files";
    var outputContainer = azureConfig["OutputContainer"] ?? "output-charts";

    return new AzureBlobStorageStore(connectionString, inputContainer, outputContainer);
});

builder.Services.AddSingleton(builder.Configuration.GetSection("Python"));

builder.Services.AddSingleton<PythonCodeValidator>(_ =>
    new PythonCodeValidator(pythonInfo.ExecutablePath));

builder.Services.AddSingleton<PythonCodeExecutor>(_ =>
    new PythonCodeExecutor(pythonInfo.ExecutablePath));

// Lets ChartPlugin read the Team-Name header from the current request to scope storage.
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<ChartPlugin>();

// ── 4. Register 24-hour temp cache cleanup background service ─────────────────
// This cleans stale files from the local /tmp/chart_input_cache/ folder every 24 hours.
// Azure Blob Storage is NEVER touched — blobs survive indefinitely.
// If a user requests a cleaned file later, GetAbsolutePath() re-downloads it from blob.
builder.Services.AddHostedService<TempCacheCleanupService>();

// ── 5. Register MCP server with HTTP/SSE transport ────────────────────────────
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<ChartPlugin>();

// ── 6. Logging ────────────────────────────────────────────────────────────────
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

var app = builder.Build();

// ── 7. Map MCP endpoint ───────────────────────────────────────────────────────
app.MapMcp();

// ── 8. Health check endpoint ──────────────────────────────────────────────────
// Includes the Python verdict so the environment inside a deployed container can be
// inspected without shell access.
app.MapGet("/health", () => Results.Ok(new
{
    status = python.IsReady ? "healthy" : "degraded",
    service = "ChartCreationMCPServer",
    python = new
    {
        ready = python.IsReady,
        executable = python.Info.ExecutablePath,
        version = python.Info.Version,
        setup = python.Status.ToString(),
        detail = python.Detail
    }
}));

app.Run();

// ── Background service: 24-hour local temp cache cleanup ──────────────────────

/// <summary>
/// A hosted background service that runs every 24 hours and deletes stale files
/// from the local temp cache folder (/tmp/chart_input_cache/).
///
/// Why this is needed:
///   When Python needs to read an input file, the blob is downloaded to a local temp folder.
///   Without cleanup, this folder grows indefinitely on a long-running container.
///   This service trims files that haven't been used in the last 24 hours.
///
/// What it does NOT do:
///   - It never touches Azure Blob Storage. Blobs are permanent.
///   - If a user requests a cleaned file, GetAbsolutePath() re-downloads it from blob automatically.
/// </summary>
public sealed class TempCacheCleanupService : BackgroundService
{
    private readonly IStorageStore _store;
    private readonly ILogger<TempCacheCleanupService> _logger;

    // How often the cleanup timer fires
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    // Delete temp files older than this
    private const int CacheExpiryHours = 24;

    public TempCacheCleanupService(IStorageStore store, ILogger<TempCacheCleanupService> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Runs the cleanup loop for the lifetime of the application.
    /// Waits 24 hours between each cleanup run.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[TempCacheCleanup] Background cleanup service started. " +
                               "Will clean local temp cache every {Hours} hours.", CleanupInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait first — no need to clean immediately on startup
            await Task.Delay(CleanupInterval, stoppingToken);

            try
            {
                _logger.LogInformation("[TempCacheCleanup] Running scheduled cleanup of local temp cache...");

                var deleted = await _store.CleanupTempCacheAsync(olderThanHours: CacheExpiryHours);

                _logger.LogInformation("[TempCacheCleanup] Cleanup complete. Deleted {Count} stale temp file(s).", deleted);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — don't log as error
                break;
            }
            catch (Exception ex)
            {
                // Log but don't crash the service — cleanup failure is non-critical
                _logger.LogError(ex, "[TempCacheCleanup] Cleanup failed unexpectedly.");
            }
        }

        _logger.LogInformation("[TempCacheCleanup] Background cleanup service stopped.");
    }
}
