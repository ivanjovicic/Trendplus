using Microsoft.Data.SqlClient;

namespace Api.Services.DataSources;

internal static class SqlServerConnectionDiagnostics
{
    public const string CategoryAuthentication = "authentication";
    public const string CategoryTimeout = "timeout";
    public const string CategoryNetwork = "network";
    public const string CategoryUnavailable = "unavailable";
    public const string CategoryCanceled = "canceled";
    public const string CategoryUnknown = "unknown";

    public static string ToSourceIdentity(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Password = string.Empty,
                UserID = string.Empty
            };

            var server = string.IsNullOrWhiteSpace(builder.DataSource) ? "(unknown)" : builder.DataSource;
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "(unknown)" : builder.InitialCatalog;
            return $"Data Source={server};Initial Catalog={database}";
        }
        catch (ArgumentException)
        {
            return "Data Source=(invalid);Initial Catalog=(invalid)";
        }
    }

    public static string Categorize(Exception exception)
    {
        if (exception is OperationCanceledException)
            return CategoryCanceled;

        if (exception is SqlException sql)
            return Categorize(sql);

        if (exception is TimeoutException)
            return CategoryTimeout;

        return CategoryUnknown;
    }

    public static string Categorize(SqlException exception)
        => CategorizeNumber(exception.Number, exception.Message);

    internal static string CategorizeNumber(int number, string? message = null)
    {
        return number switch
        {
            -2 => CategoryTimeout,
            2 or 53 or 64 or 233 or 10053 or 10054 or 10060 or 11001 => CategoryNetwork,
            18456 or 18470 or 18487 or 18488 => CategoryAuthentication,
            4060 or 4064 or 18452 => CategoryUnavailable,
            _ when IsTimeoutMessage(message) => CategoryTimeout,
            _ => CategoryUnknown
        };
    }

    public static string RedactSecrets(string? text, string? secret)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(secret) || secret.Length < 4)
            return text ?? string.Empty;

        return text.Replace(secret, "***", StringComparison.Ordinal);
    }

    private static bool IsTimeoutMessage(string? message)
        => !string.IsNullOrEmpty(message)
           && message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
}
