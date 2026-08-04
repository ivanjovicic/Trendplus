using System.Collections.Generic;

namespace Application.Logging;

public sealed record CapturedSqlExecution(
    string DbSource,
    string CommandKind,
    string CommandHash,
    string CommandText,
    IReadOnlyDictionary<string, string> Parameters,
    long DurationMs,
    bool Succeeded,
    int? RowsAffected,
    string? ExceptionMessage);
