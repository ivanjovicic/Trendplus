namespace Api.Services.DataSources;

public interface ISourceSessionFactory
{
    ISourceDataSession Create(string provider, string connectionString);
}

public sealed class SourceSessionFactory : ISourceSessionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public SourceSessionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public ISourceDataSession Create(string provider, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var normalized = provider.Trim().ToLowerInvariant();
        return normalized switch
        {
            "sqlserver" or "mssql" => new SqlServerSourceDataSession(
                connectionString,
                _loggerFactory.CreateLogger<SqlServerSourceDataSession>()),
            _ => throw new NotSupportedException($"Provider '{normalized}' is not supported for discovery.")
        };
    }
}
