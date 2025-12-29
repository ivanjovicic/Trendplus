-- Performance Logs Table Migration
-- Database: analytics (Analytics Connection)

CREATE TABLE IF NOT EXISTS "PerformanceLogs" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL,
    "RequestType" VARCHAR(200) NOT NULL,
    "RequestName" VARCHAR(500) NOT NULL,
    "DurationMs" BIGINT NOT NULL,
    "RequestData" VARCHAR(4000),
    "ResponseData" VARCHAR(4000),
    "ExceptionMessage" VARCHAR(2000),
    "IsSuccess" BOOLEAN NOT NULL
);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS "IX_PerformanceLogs_Timestamp" ON "PerformanceLogs" ("Timestamp");
CREATE INDEX IF NOT EXISTS "IX_PerformanceLogs_DurationMs" ON "PerformanceLogs" ("DurationMs");
CREATE INDEX IF NOT EXISTS "IX_PerformanceLogs_RequestName" ON "PerformanceLogs" ("RequestName");

-- Optional: Add comments
COMMENT ON TABLE "PerformanceLogs" IS 'Stores performance metrics for slow and failed MediatR requests';
COMMENT ON COLUMN "PerformanceLogs"."DurationMs" IS 'Request execution time in milliseconds';
COMMENT ON COLUMN "PerformanceLogs"."IsSuccess" IS 'True if request completed successfully, false if exception occurred';
