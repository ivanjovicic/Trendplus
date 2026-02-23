using Api.Services;

namespace Trendplus2.Endpoints;

public static class AccessImportEndpoints
{
    public static void MapAccessImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/access-import")
            .WithTags("Access Import");

        group.MapGet("/batches", async (
            IAccessImportService service,
            int take = 20,
            CancellationToken ct = default) =>
        {
            var rows = await service.GetRecentBatchesAsync(take, ct);
            return Results.Ok(rows);
        })
        .RequireRateLimiting("db-heavy")
        .WithName("GetAccessImportBatches");

        group.MapDelete("/batches/{batchId:long}", async (
            long batchId,
            IAccessImportService service,
            CancellationToken ct = default) =>
        {
            var result = await service.DeleteBatchAsync(batchId, ct);
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
            CancellationToken ct = default) =>
        {
            var resolved = await ResolveSourceFileAsync(request, ct);
            if (!resolved.Success)
                return Results.BadRequest(new { error = resolved.Error });

            try
            {
                var preview = await service.PreviewAsync(resolved.Path!, ct);
                return Results.Ok(preview);
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
            CancellationToken ct = default) =>
        {
            var resolved = await ResolveSourceFileAsync(request, ct);
            if (!resolved.Success)
                return Results.BadRequest(new { error = resolved.Error });

            var includeAnalytics = true;
            var overwriteExisting = true;

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(ct);
                includeAnalytics = ParseBoolOrDefault(form["includeAnalytics"], true);
                overwriteExisting = ParseBoolOrDefault(form["overwriteExisting"], true);
            }

            try
            {
                var run = await service.ImportAsync(resolved.Path!, includeAnalytics, overwriteExisting, ct);
                return Results.Ok(run);
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
