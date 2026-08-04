using Application.Logging;
using Infrastructure.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests;

public sealed class SqlCommandLoggingHelperTests
{
    [Fact]
    public void LogSqlExecution_WhenSqlCaptureEnabled_StoresTruncatedSqlAndMaskedParameters()
    {
        var previous = RequestLogContext.Current;
        RequestLogContext.Current = new RequestLogContext
        {
            ShouldCaptureSql = true,
            MaxQueryLength = 12,
            RequestId = "req-1",
            TraceId = "trace-1"
        };

        try
        {
            using var command = new NpgsqlCommand();
            command.Parameters.AddWithValue("@password", "super-secret");
            command.Parameters.AddWithValue("@sku", "ABC123");

            SqlCommandLoggingHelper.LogSqlExecution(
                dbSource: "analytics",
                commandKind: "ExecuteReader",
                sql: "SELECT * FROM ProductsDim WHERE ProductId = 12345",
                parameters: command.Parameters,
                durationMs: 42,
                succeeded: true,
                rowsAffected: 7,
                exception: null,
                requestId: "req-1",
                traceId: "trace-1");

            var captured = RequestLogContext.Current.CapturedSqlExecutions;

            Assert.Single(captured);
            var execution = captured[0];
            Assert.Equal("analytics", execution.DbSource);
            Assert.Equal("ExecuteReader", execution.CommandKind);
            Assert.Equal(
                SqlCommandLoggingHelper.TruncateSql("SELECT * FROM ProductsDim WHERE ProductId = 12345", 12),
                execution.CommandText);
            Assert.Equal(
                SqlCommandLoggingHelper.ComputeCommandHash("SELECT * FROM ProductsDim WHERE ProductId = 12345"),
                execution.CommandHash);
            Assert.Equal(42, execution.DurationMs);
            Assert.True(execution.Succeeded);
            Assert.Equal(7, execution.RowsAffected);
            Assert.Equal("***", execution.Parameters["password"]);
            Assert.Equal("ABC123", execution.Parameters["sku"]);
        }
        finally
        {
            RequestLogContext.Current = previous;
        }
    }

    [Fact]
    public void LogSqlExecution_WhenSqlCaptureDisabled_DoesNotStoreSnapshot()
    {
        var previous = RequestLogContext.Current;
        RequestLogContext.Current = new RequestLogContext
        {
            ShouldCaptureSql = false,
            RequestId = "req-2",
            TraceId = "trace-2"
        };

        try
        {
            using var command = new NpgsqlCommand();
            command.Parameters.AddWithValue("@sku", "ABC123");

            SqlCommandLoggingHelper.LogSqlExecution(
                dbSource: "analytics",
                commandKind: "ExecuteReader",
                sql: "SELECT 1",
                parameters: command.Parameters,
                durationMs: 5,
                succeeded: true,
                rowsAffected: null,
                exception: null,
                requestId: "req-2",
                traceId: "trace-2");

            Assert.Empty(RequestLogContext.Current.CapturedSqlExecutions);
        }
        finally
        {
            RequestLogContext.Current = previous;
        }
    }
}
