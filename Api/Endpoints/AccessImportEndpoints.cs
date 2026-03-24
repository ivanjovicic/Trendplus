using Api.Services;
using Npgsql;
using System.Data.Odbc;
using System.Runtime.InteropServices;

namespace Trendplus2.Endpoints;

public static class AccessImportEndpoints
{
    private const int BatchListFallbackTimeoutSeconds = 8;
    private static readonly string[] SchemaAnalysisUnavailableWarnings =
    [
        "Access database schema could not be fully analyzed. The ODBC provider may have returned unexpected results. Try again or contact support if issues persist."
    ];
    private static readonly string[] UnexpectedSchemaStructureWarnings =
    [
        "The Access ODBC provider returned an unexpected schema structure. Unable to enumerate tables. This may be a provider compatibility issue."
    ];

    private sealed record AccessImportRuntimeStatus(
        bool Available,
        string Platform,
        string[] MissingDependencies,
        string? Detail);

    public static void MapAccessImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/access-import")
            .WithTags("Access Import");

        group.MapGet("/runtime-status", () =>
        {
            var status = GetAccessImportRuntimeStatus();
            return Results.Ok(new
            {
                available = status.Available,
                platform = status.Platform,
                missingDependencies = status.MissingDependencies,
                detail = status.Detail
            });
        })
        .WithName("GetAccessImportRuntimeStatus");

        group.MapGet("/batches", async (
            IAccessImportService service,
            ILogger<Program> logger,
            int take = 20,
            CancellationToken ct = default) =>
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(BatchListFallbackTimeoutSeconds));

                var rows = await service.GetRecentBatchesAsync(take, timeoutCts.Token);
                return Results.Ok(rows);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogWarning("Request cancelled while loading access import batches.");
                return Results.StatusCode(499);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(
                    ex,
                    "Access import batches fallback after exceeding {TimeoutSeconds}s.",
                    BatchListFallbackTimeoutSeconds);
                return Results.Ok(Array.Empty<object>());
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Access import batches fallback due to database issue.");
                return Results.Ok(Array.Empty<object>());
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Access import batches fallback due to timeout.");
                return Results.Ok(Array.Empty<object>());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Access import batches fallback due to unexpected error.");
                return Results.Ok(Array.Empty<object>());
            }
        })
        .RequireRateLimiting("db-heavy")
        .WithName("GetAccessImportBatches");

        group.MapGet("/batches/{batchId:long}", async (
            long batchId,
            IBatchLogService logService,
            int logTake = 200,
            string? severity = null,
            CancellationToken ct = default) =>
        {
            var detail = await logService.GetBatchDetailAsync(batchId, logTake, severity, ct);
            return detail is not null
                ? Results.Ok(detail)
                : Results.NotFound(new { error = $"Batch {batchId} nije pronađen." });
        })
        .RequireRateLimiting("db-heavy")
        .WithName("GetAccessImportBatchDetail");

        group.MapGet("/batches/{batchId:long}/logs", async (
            long batchId,
            IBatchLogService logService,
            string? severity = null,
            string? tableName = null,
            int skip = 0,
            int take = 100,
            CancellationToken ct = default) =>
        {
            var logs = await logService.GetLogsAsync(batchId, severity, tableName, skip, take, ct);
            return Results.Ok(logs);
        })
        .RequireRateLimiting("db-heavy")
        .WithName("GetAccessImportBatchLogs");

        group.MapDelete("/batches/{batchId:long}", async (
            long batchId,
            IAccessImportService service,
            bool includeAnalytics = true,
            CancellationToken ct = default) =>
        {
            var result = await service.DeleteBatchAsync(batchId, includeAnalytics, ct);
            return result.Found
                ? Results.Ok(result)
                : Results.NotFound(new { error = $"Batch {batchId} nije pronađen." });
        })
        .RequireRateLimiting("writes")
        .WithName("DeleteAccessImportBatch");

        group.MapGet("/scope-options", () =>
        {
            return Results.Ok(new[]
            {
                new { value = "all", label = "Sve (existing + imported)" },
                new { value = "existing", label = "Samo postojeci" },
                new { value = "imported", label = "Samo importovani" }
            });
        })
        .WithName("GetAccessImportScopeOptions");

        group.MapPost("/preview", async (
            HttpRequest request,
            IAccessImportService service,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var runtimeStatus = GetAccessImportRuntimeStatus();
            if (!runtimeStatus.Available)
            {
                return Results.Problem(
                    title: "Access import runtime missing",
                    detail: runtimeStatus.Detail ?? "Access preview is unavailable on this server because required runtime dependencies are missing.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resolved = await ResolveSourceFileAsync(request, ct);
            if (!resolved.Success)
                return Results.BadRequest(new { error = resolved.Error });

            try
            {
                var preview = await service.PreviewAsync(resolved.Path!, ct: ct);
                return Results.Ok(preview);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (OdbcException ex)
            {
                logger.LogWarning(ex, "Access preview ODBC issue. Returning fail-soft preview response.");
                return Results.Ok(new
                {
                    tables = Array.Empty<object>(),
                    warnings = new[] { $"Preview failed: {ex.GetBaseException().Message}" },
                    canImport = false,
                    mappedAccessTables = 0
                });
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "Access import preview failed due to missing ODBC runtime dependency.");
                return Results.Problem(
                    title: "Access import runtime missing",
                    detail: "Access preview is unavailable on this server because required runtime dependencies are missing.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (PlatformNotSupportedException ex)
            {
                logger.LogWarning(ex, "Access import preview is not supported on this platform.");
                return Results.Problem(
                    title: "Access preview not supported",
                    detail: "Access preview is not supported on this server platform.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
            {
                logger.LogWarning(ex, "Access import preview failed due to missing native dependency.");
                return Results.Problem(
                    title: "Access import runtime missing",
                    detail: "Access preview is unavailable on this server because required native runtime dependencies are missing.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (IndexOutOfRangeException ex)
            {
                // Schema issue: column missing from ODBC provider schema
                logger.LogWarning(ex, "Access import schema handling error - provider returned non-standard schema. Returning best-effort preview.");
                return Results.Ok(new
                {
                    tables = Array.Empty<object>(),
                    warnings = SchemaAnalysisUnavailableWarnings,
                    canImport = false,
                    mappedAccessTables = 0
                });
            }
            catch (Exception ex) when (ex.Message.Contains("does not belong to table", StringComparison.OrdinalIgnoreCase))
            {
                // Schema issue: specific ODBC provider column-not-found error
                logger.LogWarning(ex, "Access import schema error - ODBC provider returned non-standard schema structure. Returning best-effort preview.");
                return Results.Ok(new
                {
                    tables = Array.Empty<object>(),
                    warnings = UnexpectedSchemaStructureWarnings,
                    canImport = false,
                    mappedAccessTables = 0
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Access import preview failed unexpectedly. Exception: {ExceptionType}: {Message}", ex.GetType().Name, ex.GetBaseException().Message);
                return Results.Ok(new
                {
                    tables = Array.Empty<object>(),
                    warnings = new[] { $"Access preview encountered an issue: {ex.GetBaseException().Message}" },
                    canImport = false,
                    mappedAccessTables = 0
                });
            }
            finally
            {
                if (resolved.DeleteAfter && File.Exists(resolved.Path))
                    File.Delete(resolved.Path);
            }
        })
        .RequireRateLimiting("db-heavy")
        .DisableAntiforgery()
        .WithName("PreviewAccessImport");

        group.MapPost("/run", async (
            HttpRequest request,
            IAccessImportService service,
            ILogger<Program> logger,
            CancellationToken ct = default) =>
        {
            var runtimeStatus = GetAccessImportRuntimeStatus();
            if (!runtimeStatus.Available)
            {
                return Results.Problem(
                    title: "Access import runtime missing",
                    detail: runtimeStatus.Detail ?? "Access import is unavailable on this server because required runtime dependencies are missing.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resolved = await ResolveSourceFileAsync(request, ct);
            if (!resolved.Success)
                return Results.BadRequest(new { error = resolved.Error });

            var includeAnalytics = true;
            var overwriteExisting = true;
            var includeTemporaryTables = false;

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(ct);
                includeAnalytics = ParseBoolOrDefault(form["includeAnalytics"], true);
                overwriteExisting = ParseBoolOrDefault(form["overwriteExisting"], true);
                includeTemporaryTables = ParseBoolOrDefault(form["includeTemporaryTables"], false);
            }

            try
            {
                var run = await service.StartImportAsync(resolved.Path!, includeAnalytics, overwriteExisting, includeTemporaryTables, ct);
                return Results.Accepted($"/api/access-import/batches/{run.BatchId}", run);
            }
            catch (FileNotFoundException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (OdbcException ex)
            {
                return Results.Problem(
                    title: "Access connection failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (DllNotFoundException ex)
            {
                logger.LogWarning(ex, "Access import run failed due to missing ODBC runtime dependency.");
                return Results.Problem(
                    title: "Access import runtime missing",
                    detail: "Access import is unavailable on this server because required runtime dependencies are missing.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (PlatformNotSupportedException ex)
            {
                logger.LogWarning(ex, "Access import run is not supported on this platform.");
                return Results.Problem(
                    title: "Access import not supported",
                    detail: "Access import is not supported on this server platform.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
            {
                logger.LogWarning(ex, "Access import run failed due to missing native dependency.");
                return Results.Problem(
                    title: "Access import runtime missing",
                    detail: "Access import is unavailable on this server because required native runtime dependencies are missing.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (IndexOutOfRangeException ex)
            {
                // Schema issue during import: column missing from ODBC provider schema
                logger.LogWarning(ex, "Access import schema handling error during import - provider returned non-standard schema.");
                return Results.BadRequest(new 
                { 
                    error = "Access database schema could not be processed. The ODBC provider may have returned unexpected results. Try again or contact support.",
                    status = "failed"
                });
            }
            catch (Exception ex) when (ex.Message.Contains("does not belong to table", StringComparison.OrdinalIgnoreCase))
            {
                // Schema issue: specific ODBC provider column-not-found error during import
                logger.LogWarning(ex, "Access import schema error - ODBC provider returned non-standard schema structure.");
                return Results.BadRequest(new 
                { 
                    error = "The Access ODBC provider returned an unexpected schema structure. This may be a provider compatibility issue. Please verify your database file and try again.",
                    status = "failed"
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Access import run failed unexpectedly. Exception: {ExceptionType}: {Message}", ex.GetType().Name, ex.GetBaseException().Message);
                
                // For system failures (unavailable runtime), return 503
                if (ex is DllNotFoundException or PlatformNotSupportedException)
                {
                    return Results.Problem(
                        title: "Access import not available",
                        detail: ex.GetBaseException().Message,
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                // For data/validation errors, return 400 with diagnostic info
                return Results.BadRequest(new 
                { 
                    error = ex.GetBaseException().Message,
                    status = "failed"
                });
            }
            finally
            {
                if (resolved.DeleteAfter && File.Exists(resolved.Path))
                    File.Delete(resolved.Path);
            }
        })
        .RequireRateLimiting("writes")
        .DisableAntiforgery()
        .WithName("RunAccessImport");
    }

    private static bool ParseBoolOrDefault(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static AccessImportRuntimeStatus GetAccessImportRuntimeStatus()
    {
        if (OperatingSystem.IsWindows())
        {
            return new AccessImportRuntimeStatus(
                Available: true,
                Platform: "windows",
                MissingDependencies: [],
                Detail: null);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var missing = new List<string>();
            if (!IsCommandAvailable("mdb-tables"))
                missing.Add("mdb-tables");

            if (!IsCommandAvailable("mdb-export"))
                missing.Add("mdb-export");

            if (missing.Count == 0)
            {
                return new AccessImportRuntimeStatus(
                    Available: true,
                    Platform: OperatingSystem.IsLinux() ? "linux" : "macos",
                    MissingDependencies: [],
                    Detail: null);
            }

            var detail = $"Access preview/import is unavailable on this server. Missing runtime tools: {string.Join(", ", missing)}.";
            return new AccessImportRuntimeStatus(
                Available: false,
                Platform: OperatingSystem.IsLinux() ? "linux" : "macos",
                MissingDependencies: missing.ToArray(),
                Detail: detail);
        }

        return new AccessImportRuntimeStatus(
            Available: false,
            Platform: "unknown",
            MissingDependencies: ["Unsupported platform"],
            Detail: "Access preview/import is unavailable on this server platform.");
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
                return false;

            var extensions = OperatingSystem.IsWindows()
                ? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? [".exe", ".cmd", ".bat"])
                : [string.Empty];

            foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(directory, command + extension);
                    if (File.Exists(candidate))
                        return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static async Task<(bool Success, string? Path, string? Error, bool DeleteAfter)> ResolveSourceFileAsync(
        HttpRequest request,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            var root = FindRootAccessFile();
            if (root is not null)
                return (true, root, null, false);

            return (false, null, "Posalji multipart/form-data sa ACCDB fajlom ili postavi TRENDPLUS.accdb u root folder.", false);
        }

        var form = await request.ReadFormAsync(ct);
        var useRoot = ParseBoolOrDefault(form["useRootFile"], false);
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
        {
            if (useRoot)
            {
                var root = FindRootAccessFile();
                if (root is not null)
                    return (true, root, null, false);
                return (false, null, "TRENDPLUS.accdb nije pronadjen u root folderu.", false);
            }

            return (false, null, "ACCDB fajl je obavezan.", false);
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".accdb", StringComparison.OrdinalIgnoreCase))
            return (false, null, "Dozvoljen je samo .accdb fajl.", false);

        var tempPath = Path.Combine(Path.GetTempPath(), "trendplus-access-import");
        Directory.CreateDirectory(tempPath);
        var filePath = Path.Combine(tempPath, $"{Guid.NewGuid():N}.accdb");

        await using var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(fs, ct);
        return (true, filePath, null, true);
    }

    private static string? FindRootAccessFile()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var depth = 0;
        while (dir is not null && depth < 8)
        {
            var candidate = Path.Combine(dir.FullName, "TRENDPLUS.accdb");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
            depth++;
        }

        return null;
    }
}
