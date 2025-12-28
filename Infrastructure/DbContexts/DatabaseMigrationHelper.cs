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
                logger.LogInformation("Checking if PerformanceLogs table exists...");

                var sql = @"
                    CREATE TABLE IF NOT EXISTS ""PerformanceLogs"" (
                        ""Id"" BIGSERIAL PRIMARY KEY,
                        ""Timestamp"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""RequestType"" VARCHAR(200) NOT NULL,
                        ""RequestName"" VARCHAR(500) NOT NULL,
                        ""DurationMs"" BIGINT NOT NULL,
                        ""RequestData"" VARCHAR(4000),
                        ""ResponseData"" VARCHAR(4000),
                        ""ExceptionMessage"" VARCHAR(2000),
                        ""IsSuccess"" BOOLEAN NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS ""IX_PerformanceLogs_Timestamp"" 
                        ON ""PerformanceLogs"" (""Timestamp"");
                    
                    CREATE INDEX IF NOT EXISTS ""IX_PerformanceLogs_DurationMs"" 
                        ON ""PerformanceLogs"" (""DurationMs"");
                    
                    CREATE INDEX IF NOT EXISTS ""IX_PerformanceLogs_RequestName"" 
                        ON ""PerformanceLogs"" (""RequestName"");
                ";

                await context.Database.ExecuteSqlRawAsync(sql);
                
                logger.LogInformation("? PerformanceLogs table and indexes created/verified successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "? Failed to create PerformanceLogs table");
                throw;
            }
        }
    }
}
