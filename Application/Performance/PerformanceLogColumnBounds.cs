using System;

namespace Application.Performance;

/// <summary>
/// Enforces PostgreSQL <c>PerformanceLogs</c> VARCHAR limits for MediatR persistence.
/// Uses the same ellipsis truncation semantics as HTTP performance persistence.
/// </summary>
public static class PerformanceLogColumnBounds
{
    public const int RequestTypeMaxLength = 200;
    public const int RequestNameMaxLength = 500;
    public const int ExceptionMessageMaxLength = 2000;
    public const int PayloadMaxLength = 4000;
    public const int SerializedJsonMaxLength = 3900;

    public static string BoundRequired(string? value, int maxLength) =>
        Truncate(value, maxLength) ?? string.Empty;

    public static string? Truncate(string? value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 3
            ? value[..maxLength]
            : value[..(maxLength - 3)] + "...";
    }
}
