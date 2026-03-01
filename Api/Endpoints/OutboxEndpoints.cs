using Application.Artikli.Common.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

public static class OutboxEndpoints
{
    public static void MapOutboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/outbox")
            .WithTags("System", "Admin")
            .RequireRateLimiting("analytics");

        group.MapGet("/stats", async (ITrendplusDbContext db, CancellationToken ct) =>
        {
            var total = await db.OutboxMessages.CountAsync(ct);
            var processed = await db.OutboxMessages.CountAsync(m => m.IsProcessed, ct);
            var pending = await db.OutboxMessages.CountAsync(m => !m.IsProcessed && m.RetryCount < 5, ct);
            var failed = await db.OutboxMessages.CountAsync(m => !m.IsProcessed && m.RetryCount >= 5, ct);

            var recentMessages = await db.OutboxMessages
                .AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .Take(10)
                .Select(m => new
                {
                    m.Id,
                    m.EventType,
                    m.Payload,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.IsProcessed,
                    m.RetryCount,
                    m.ErrorMessage,
                    m.CorrelationId
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                stats = new
                {
                    total,
                    processed,
                    pending,
                    failed,
                    successRate = total > 0 ? (double)processed / total * 100 : 0
                },
                recentMessages
            });
        })
        .WithName("GetOutboxStats")
        .RequireRateLimiting("analytics");

        group.MapGet("/messages", async (
            ITrendplusDbContext db,
            int pageNumber = 1,
            int pageSize = 50,
            bool? isProcessed = null,
            string? eventType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);
            fromDate = NormalizeToUtc(fromDate);
            toDate = NormalizeToUtc(toDate);

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                return Results.BadRequest(new { message = "fromDate ne moze biti posle toDate." });
            }

            var query = db.OutboxMessages.AsNoTracking().AsQueryable();

            if (isProcessed.HasValue)
            {
                query = query.Where(m => m.IsProcessed == isProcessed.Value);
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                var pattern = $"%{eventType.Trim()}%";
                query = query.Where(m => EF.Functions.ILike(m.EventType, pattern));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(m => m.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(m => m.CreatedAt <= toDate.Value);
            }

            var total = await query.CountAsync(ct);

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.EventType,
                    m.Payload,
                    m.CreatedAt,
                    m.ProcessedAt,
                    m.IsProcessed,
                    m.RetryCount,
                    m.ErrorMessage,
                    m.CorrelationId
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                messages,
                totalCount = total,
                pageNumber,
                pageSize
            });
        })
        .WithName("GetOutboxMessages")
        .RequireRateLimiting("db-heavy");

        group.MapPost("/retry/{id:long}", async (
            long id,
            ITrendplusDbContext db,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
            if (message is null)
            {
                return Results.NotFound(new { message = $"Outbox poruka sa id={id} nije pronadjena." });
            }

            message.RetryCount = 0;
            message.ErrorMessage = null;
            message.IsProcessed = false;
            message.ProcessedAt = null;

            await db.SaveChangesAsync(ct);
            logger.OutboxRetry(id);

            return Results.Ok(new { success = true, id });
        })
        .WithName("RetryOutboxMessage")
        .RequireRateLimiting("writes");

        group.MapPost("/retry-all-failed", async (
            ITrendplusDbContext db,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var failedMessages = await db.OutboxMessages
                .Where(m => !m.IsProcessed && m.RetryCount >= 5)
                .ToListAsync(ct);

            foreach (var message in failedMessages)
            {
                message.RetryCount = 0;
                message.ErrorMessage = null;
            }

            await db.SaveChangesAsync(ct);
            logger.BulkRetry(failedMessages.Count);

            return Results.Ok(new { success = true, count = failedMessages.Count });
        })
        .WithName("RetryAllFailedOutboxMessages")
        .RequireRateLimiting("strict");

        group.MapPost("/purge-processed", async (
            ITrendplusDbContext db,
            ILogger<Program> logger,
            int olderThanDays = 7,
            CancellationToken ct = default) =>
        {
            olderThanDays = Math.Clamp(olderThanDays, 1, 3650);
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

            var messagesToDelete = await db.OutboxMessages
                .Where(m => m.IsProcessed && m.ProcessedAt.HasValue && m.ProcessedAt.Value < cutoffDate)
                .ToListAsync(ct);

            if (messagesToDelete.Count > 0)
            {
                db.OutboxMessages.RemoveRange(messagesToDelete);
                await db.SaveChangesAsync(ct);
            }

            logger.PurgeProcessed(messagesToDelete.Count, olderThanDays);
            return Results.Ok(new { success = true, count = messagesToDelete.Count, olderThanDays });
        })
        .WithName("PurgeProcessedOutboxMessages")
        .RequireRateLimiting("strict");

        group.MapGet("/stats-by-type", async (ITrendplusDbContext db, CancellationToken ct) =>
        {
            var stats = await db.OutboxMessages
                .AsNoTracking()
                .GroupBy(m => m.EventType)
                .Select(g => new
                {
                    eventType = g.Key,
                    total = g.Count(),
                    processed = g.Count(m => m.IsProcessed),
                    pending = g.Count(m => !m.IsProcessed && m.RetryCount < 5),
                    failed = g.Count(m => !m.IsProcessed && m.RetryCount >= 5)
                })
                .OrderByDescending(s => s.total)
                .ToListAsync(ct);

            return Results.Ok(stats);
        })
        .WithName("GetOutboxStatsByType")
        .RequireRateLimiting("analytics");
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value.Value.ToUniversalTime()
        };
    }
}
