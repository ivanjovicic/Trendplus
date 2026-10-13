using System;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Decides whether PostgreSQL-backed proof is mandatory in the current environment.
/// </summary>
internal static class PostgresTestRequirement
{
    public const string RequireVariable = "TRENDPLUS_REQUIRE_POSTGRES_TESTS";

    public static bool IsRequired =>
        IsTrue(Environment.GetEnvironmentVariable("CI"))
        || IsTrue(Environment.GetEnvironmentVariable(RequireVariable));

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A PostgreSQL test that reports Skipped (not Passed) where PostgreSQL is not required,
/// and runs (failing if the fixture is unavailable) in CI or with TRENDPLUS_REQUIRE_POSTGRES_TESTS=true.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class PostgresRequiredFactAttribute : FactAttribute
{
    public PostgresRequiredFactAttribute()
    {
        if (!PostgresTestRequirement.IsRequired)
        {
            Skip = "PostgreSQL proof not run: set " + PostgresTestRequirement.RequireVariable + "=true with a working Docker/Testcontainers runtime (always required in CI).";
        }
    }
}
