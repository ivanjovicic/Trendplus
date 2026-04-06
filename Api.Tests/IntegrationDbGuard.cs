using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Api.Tests;

internal static class IntegrationDbGuard
{
    public static bool TryResolveConnectionString(string? connectionString, [NotNullWhen(true)] out string? resolved)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            resolved = null;
            return false;
        }

        resolved = connectionString;
        return true;
    }

    public static bool TryEnsureAvailable(params (string Name, string ConnectionString)[] targets)
    {
        foreach (var (_, connectionString) in targets)
        {
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                connection.Open();
            }
            catch
            {
                return false;
            }
        }

        return true;
    }
}
