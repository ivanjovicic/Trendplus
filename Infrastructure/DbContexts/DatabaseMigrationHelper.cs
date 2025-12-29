using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Infrastructure.DbContexts
{
    public static class DatabaseMigrationHelper
    {
        public static async Task EnsurePerformanceLogsTableExistsAsync(
            AnalyticsDbContext context,
            ILogger logger)
        {
            try
            {
                logger.LogInformation("Skipping PerformanceLogs table creation (managed by EF migrations).");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed during PerformanceLogs initialization step");
                throw;
            }
        }
    }
}
