using Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Trendplus2.Endpoints;

public static class AnalyticsTableEndpoints
{
    public static void MapAnalyticsTableEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analitika/{table}/{id}", async (
            string table,
            string id,
            HttpRequest request,
            IAnalyticsDetailReadService detailService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            const int timeoutMs = 10_000;
            logger.LogInformation("Fetching analytics detail for table {Table} and id {Id}", table, id);

            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);

                var detailTask = detailService.GetDetailAsync(table, id, request.Query, cts.Token);
                var completed = await Task.WhenAny(detailTask, Task.Delay(timeoutMs, cts.Token));
                if (completed != detailTask)
                {
                    logger.LogWarning("Analytics detail request timed out for {Table}/{Id} after {Timeout}ms", table, id, timeoutMs);
                    return Results.StatusCode(504);
                }

                var detail = await detailTask; // already completed
                sw.Stop();
                logger.LogInformation("Analytics detail fetched for {Table}/{Id} in {Elapsed}ms", table, id, sw.ElapsedMilliseconds);

                return detail is null
                    ? Results.NotFound(new { message = $"Detalj nije pronađen za tabelu '{table}' i zapis '{id}'." })
                    : Results.Ok(detail);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogWarning("Analytics detail request cancelled by caller for {Table}/{Id}", table, id);
                return Results.StatusCode(499); // client closed request
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogError(ex, "Failed to fetch analytics detail for {Table}/{Id} after {Elapsed}ms", table, id, sw.ElapsedMilliseconds);
                return Results.Problem(detail: "Greska pri ucitavanju detalja analitike.");
            }
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }
}
