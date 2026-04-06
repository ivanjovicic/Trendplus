using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Application.Logging;
using Npgsql;
using Serilog;

namespace Infrastructure.Logging;

/// <summary>
/// Minimal helper to emit structured SQL execution logs.
/// Phase 1: write to application logger (Serilog) only - no DB schema changes.
/// </summary>
public static class SqlCommandLoggingHelper
{
    public static string TruncateSql(string sql, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(sql)) return sql ?? string.Empty;
        return sql.Length <= maxLength ? sql : sql.Substring(0, maxLength) + "...";
    }

    public static string ComputeCommandHash(string sql)
    {
        if (string.IsNullOrEmpty(sql)) return string.Empty;
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sql));
        return BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
    }

    public static Dictionary<string, string> SummarizeParameters(NpgsqlParameterCollection? parameters)
    {
        var summary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters == null) return summary;

        foreach (NpgsqlParameter p in parameters)
        {
            try
            {
                var name = p.ParameterName?.TrimStart('@') ?? "@param";
                var val = p.Value == null || p.Value == DBNull.Value ? "NULL" : p.Value.ToString() ?? "";

                if (IsSensitiveParameter(name))
                {
                    summary[name] = "***";
                }
                else
                {
                    summary[name] = val.Length > 200 ? val.Substring(0, 200) + "..." : val;
                }
            }
            catch
            {
                // Best-effort; never throw from logging helper.
            }
        }

        return summary;
    }

    private static bool IsSensitiveParameter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("pass") || lower.Contains("token") || lower.Contains("secret") || lower.Contains("key");
    }

    public static void LogSqlExecution(
        string dbSource,
        string commandKind,
        string sql,
        NpgsqlParameterCollection? parameters,
        long durationMs,
        bool succeeded,
        int? rowsAffected = null,
        Exception? exception = null,
        string? requestId = null,
        string? traceId = null)
    {
        try
        {
            // Skip successful SQL logs only when request context explicitly disabled capture.
            var req = RequestLogContext.Current;
            if (!req.ShouldCaptureSql
                && succeeded
                && (!string.IsNullOrWhiteSpace(req.RequestId) || !string.IsNullOrWhiteSpace(req.TraceId)))
            {
                return;
            }

            var maxLen = req.MaxQueryLength ?? 2000;
            var truncated = TruncateSql(sql, maxLen);
            var hash = ComputeCommandHash(sql);
            var paramsSummary = SummarizeParameters(parameters);

            if (succeeded)
            {
                Log.ForContext("DbSource", dbSource)
                   .ForContext("CommandKind", commandKind)
                   .ForContext("CommandHash", hash)
                   .ForContext("DurationMs", durationMs)
                   .ForContext("RowsAffected", rowsAffected)
                   .ForContext("RequestId", requestId)
                   .ForContext("TraceId", traceId)
                   .ForContext("CommandText", truncated)
                   .ForContext("Params", paramsSummary, true)
                   .Information("SqlExecuted: {CommandHash} {CommandKind} on {DbSource} ({DurationMs}ms)", hash, commandKind, dbSource, durationMs);
            }
            else
            {
                Log.ForContext("DbSource", dbSource)
                   .ForContext("CommandKind", commandKind)
                   .ForContext("CommandHash", hash)
                   .ForContext("DurationMs", durationMs)
                   .ForContext("RowsAffected", rowsAffected)
                   .ForContext("RequestId", requestId)
                   .ForContext("TraceId", traceId)
                   .ForContext("CommandText", truncated)
                   .ForContext("Params", paramsSummary, true)
                   .Error(exception, "SqlFailed: {CommandHash} {CommandKind} on {DbSource} ({DurationMs}ms) - {Message}", hash, commandKind, dbSource, durationMs, exception?.Message);
            }
        }
        catch (Exception ex)
        {
            try
            {
                Log.Error(ex, "SqlCommandLoggingHelper failed");
            }
            catch
            {
                // Never throw from logging helper.
            }
        }
    }
}
