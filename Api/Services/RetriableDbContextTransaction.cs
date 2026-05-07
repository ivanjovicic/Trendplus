using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

internal static class RetriableDbContextTransaction
{
    public static async Task ExecuteAsync(DbContext dbContext, Func<CancellationToken, Task> operation, CancellationToken ct = default)
        => await ExecuteAsync(dbContext, operation, logger: null, operationName: null, ct);

    public static async Task ExecuteAsync(
        DbContext dbContext,
        Func<CancellationToken, Task> operation,
        ILogger? logger,
        string? operationName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(operation);

        var effectiveOperationName = string.IsNullOrWhiteSpace(operationName)
            ? dbContext.GetType().Name
            : operationName;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            logger?.LogInformation(
                "DB transaction started. Operation: {Operation}. DbContext: {DbContext}.",
                effectiveOperationName,
                dbContext.GetType().Name);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await operation(ct);
                await transaction.CommitAsync(ct);
                stopwatch.Stop();
                logger?.LogInformation(
                    "DB transaction committed. Operation: {Operation}. DbContext: {DbContext}. ElapsedMs: {ElapsedMs}.",
                    effectiveOperationName,
                    dbContext.GetType().Name,
                    stopwatch.ElapsedMilliseconds);
            }
            catch
            {
                stopwatch.Stop();
                await transaction.RollbackAsync(ct);
                logger?.LogWarning(
                    "DB transaction rolled back. Operation: {Operation}. DbContext: {DbContext}. ElapsedMs: {ElapsedMs}.",
                    effectiveOperationName,
                    dbContext.GetType().Name,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        });
    }
}