using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.Services.Startup
{
    internal static class DbConnectionHelper
    {
        /// <summary>
        /// Executes a lightweight SQL probe (SELECT 1) against the provided <see cref="DbContext"/> with retries.
        /// Returns true when the probe succeeds, false when all attempts fail or cancellation occurs.
        /// </summary>
        public static async Task<bool> TryExecuteSqlProbeAsync(DbContext dbContext, ILogger logger, CancellationToken ct, int maxAttempts = 3, TimeSpan? initialDelay = null)
        {
            initialDelay ??= TimeSpan.FromSeconds(2);
            var delay = initialDelay.Value;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                try
                {
                    var sw = Stopwatch.StartNew();
                    // Execute a minimal probe. EF will open and close the connection for this call.
                    await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", ct);
                    sw.Stop();
                    logger?.LogInformation("DB probe succeeded for {DbContext} in {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts})", dbContext.GetType().Name, sw.ElapsedMilliseconds, attempt, maxAttempts);
                    return true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    logger?.LogInformation("DB probe cancelled for {DbContext}", dbContext.GetType().Name);
                    return false;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "DB probe attempt {Attempt}/{MaxAttempts} failed for {DbContext}", attempt, maxAttempts, dbContext.GetType().Name);
                    if (attempt == maxAttempts)
                    {
                        logger?.LogError(ex, "DB probe ultimately failed for {DbContext} after {Attempts} attempts", dbContext.GetType().Name, maxAttempts);
                        return false;
                    }

                    try
                    {
                        await Task.Delay(delay, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return false;
                    }

                    // Exponential backoff with a sensible cap.
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }

            return false;
        }

        public static async Task<(bool Ok, long? ElapsedMs, string? Error)> TryProbeConnectionStringAsync(
            string name,
            string? connectionString,
            CancellationToken requestToken,
            ILogger? logger = null,
            string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (logger is not null && correlationId is not null)
                {
                    logger.LogWarning(
                        "Dependency probe {ProbeName} failed with {ErrorCode}. CorrelationId={CorrelationId}",
                        name,
                        DependencyHealthPublicErrors.MissingConnectionString,
                        correlationId);
                }

                return (false, null, DependencyHealthPublicErrors.ForMissingConnectionString());
            }

            var sw = Stopwatch.StartNew();

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                var csb = new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = 5,
                    CommandTimeout = 5
                };

                await using var connection = new NpgsqlConnection(csb.ConnectionString);
                await connection.OpenAsync(timeoutCts.Token);

                await using var command = new NpgsqlCommand("SELECT 1;", connection)
                {
                    CommandTimeout = 5
                };

                await command.ExecuteScalarAsync(timeoutCts.Token);
                sw.Stop();
                return (true, sw.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
            {
                sw.Stop();
                var code = DependencyHealthPublicErrors.ForCanceled(requestAborted: true);
                logger?.LogWarning(
                    "Dependency probe {ProbeName} canceled by request. ErrorCode={ErrorCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                    name,
                    code,
                    sw.ElapsedMilliseconds,
                    correlationId);
                return (false, sw.ElapsedMilliseconds, code);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                var code = DependencyHealthPublicErrors.ForCanceled(requestAborted: false);
                logger?.LogWarning(
                    "Dependency probe {ProbeName} timed out. ErrorCode={ErrorCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                    name,
                    code,
                    sw.ElapsedMilliseconds,
                    correlationId);
                return (false, sw.ElapsedMilliseconds, code);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var code = DependencyHealthPublicErrors.ForUnexpectedFailure();
                logger?.LogError(
                    ex,
                    "Dependency probe {ProbeName} failed. ErrorCode={ErrorCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}",
                    name,
                    code,
                    sw.ElapsedMilliseconds,
                    correlationId);
                return (false, sw.ElapsedMilliseconds, code);
            }
        }
    }
}
