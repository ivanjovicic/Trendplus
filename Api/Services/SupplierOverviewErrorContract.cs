using Microsoft.AspNetCore.Http;
using Npgsql;

namespace Api.Services;

public sealed record SupplierOverviewErrorClassification(
    string ErrorCode,
    string Title,
    string Detail,
    int StatusCode);

public static class SupplierOverviewErrorContract
{
    public static SupplierOverviewErrorClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
        {
            return new(
                "ANALYTICS_TIMEOUT",
                "Podaci dobavljača trenutno nisu dostupni",
                "Upit za podatke dobavljača je istekao ili je prekinut. Pokušajte ponovo.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (exception is PostgresException postgres)
        {
            return postgres.SqlState switch
            {
                "42P01" => new(
                    "SUPPLIER_OVERVIEW_SCHEMA_MISSING",
                    "Podaci dobavljača trenutno nisu dostupni",
                    "Analitička tabela ili pogled za pregled dobavljača nije dostupan. Proverite spremnost analitičke šeme i pokušajte ponovo.",
                    StatusCodes.Status503ServiceUnavailable),
                "42703" => new(
                    "SUPPLIER_OVERVIEW_SCHEMA_INVALID",
                    "Podaci dobavljača trenutno nisu dostupni",
                    "Analitička šema za pregled dobavljača nije kompatibilna sa aktivnim ugovorom. Proverite migracije i pokušajte ponovo.",
                    StatusCodes.Status503ServiceUnavailable),
                _ when postgres.SqlState.StartsWith("08", StringComparison.Ordinal) => DatabaseUnavailable(),
                _ when postgres.SqlState == "57014" => Timeout(),
                _ => DatabaseUnavailable()
            };
        }

        if (exception is NpgsqlException)
        {
            return DatabaseUnavailable();
        }

        return new(
            "SUPPLIER_OVERVIEW_UNEXPECTED_ERROR",
            "Podaci dobavljača trenutno nisu dostupni",
            "Podaci dobavljača trenutno nisu dostupni zbog neočekivane greške. Pokušajte ponovo.",
            StatusCodes.Status500InternalServerError);
    }

    private static SupplierOverviewErrorClassification DatabaseUnavailable() =>
        new(
            "ANALYTICS_DB_UNAVAILABLE",
            "Podaci dobavljača trenutno nisu dostupni",
            "Analitička baza trenutno nije dostupna. Pokušajte ponovo kasnije.",
            StatusCodes.Status503ServiceUnavailable);

    private static SupplierOverviewErrorClassification Timeout() =>
        new(
            "ANALYTICS_TIMEOUT",
            "Podaci dobavljača trenutno nisu dostupni",
            "Upit za podatke dobavljača je istekao. Pokušajte ponovo.",
            StatusCodes.Status503ServiceUnavailable);
}
