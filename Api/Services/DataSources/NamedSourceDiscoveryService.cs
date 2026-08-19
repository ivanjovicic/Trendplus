using Microsoft.Extensions.Options;

namespace Api.Services.DataSources;

public sealed class NamedSourceDiscoveryService
{
    private readonly DataSourceConnectorOptions _options;
    private readonly ISourceSessionFactory _sessionFactory;
    private readonly ILogger<NamedSourceDiscoveryService> _logger;

    public NamedSourceDiscoveryService(
        IOptions<DataSourceConnectorOptions> options,
        ISourceSessionFactory sessionFactory,
        ILogger<NamedSourceDiscoveryService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<NamedSourceSummaryDto> ListSources()
    {
        var sources = _options.Sources ?? new Dictionary<string, DataSourceProfileOptions>(StringComparer.OrdinalIgnoreCase);
        var items = new List<NamedSourceSummaryDto>(sources.Count);
        foreach (var (name, profile) in sources.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(name) || profile is null)
                continue;

            items.Add(ToSummary(name, profile));
        }

        Audit("list", name: "*", provider: "n/a", identity: $"{items.Count} sources", category: "ok");
        return items;
    }

    public async Task<SourceConnectionTestDto> TestConnectionAsync(string name, CancellationToken ct = default)
    {
        if (!TryResolve(name, out var profile, out var error))
            throw error;

        var identity = SafeIdentity(profile);
        if (string.IsNullOrWhiteSpace(profile.ConnectionString))
        {
            Audit("test-connection", name, profile.Provider, identity, SqlServerConnectionDiagnostics.CategoryUnavailable);
            return new SourceConnectionTestDto(false, SqlServerConnectionDiagnostics.CategoryUnavailable, identity, "Source is not configured.");
        }

        try
        {
            await using var session = _sessionFactory.Create(profile.Provider, profile.ConnectionString);
            await session.TestConnectionAsync(ct);
            Audit("test-connection", name, session.Provider, session.SourceIdentity, "ok");
            return new SourceConnectionTestDto(true, "ok", session.SourceIdentity, null);
        }
        catch (NotSupportedException)
        {
            Audit("test-connection", name, profile.Provider, identity, SqlServerConnectionDiagnostics.CategoryUnknown);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var category = SqlServerConnectionDiagnostics.Categorize(ex.InnerException ?? ex);
            Audit("test-connection", name, profile.Provider, identity, category);
            return new SourceConnectionTestDto(false, category, identity, "Connection test failed.");
        }
    }

    public async Task<SourceTablesDto> GetTablesAsync(string name, CancellationToken ct = default)
    {
        await using var session = OpenConfigured(name);
        var tables = await session.GetTablesAsync(includeTemporaryTables: false, ct);
        var schemas = tables
            .Select(table => SqlServerIdentifier.TryParseTable(table, out var schema, out _, out _) ? schema : "dbo")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(schema => schema, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Audit("list-tables", name, session.Provider, session.SourceIdentity, "ok");
        return new SourceTablesDto(name, schemas, tables);
    }

    public async Task<SourceColumnsDto> GetColumnsAsync(string name, string table, CancellationToken ct = default)
    {
        if (!SqlServerIdentifier.TryQuoteTable(table, out _, out var failureReason))
            throw new ArgumentException(failureReason, nameof(table));

        await using var session = OpenConfigured(name);
        var columns = await session.GetColumnsAsync(table, ct);
        Audit("list-columns", name, session.Provider, session.SourceIdentity, "ok");
        return new SourceColumnsDto(name, table, columns);
    }

    internal static NamedSourceSummaryDto ToSummary(string name, DataSourceProfileOptions profile)
    {
        var provider = string.IsNullOrWhiteSpace(profile.Provider) ? "unknown" : profile.Provider.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? name : profile.DisplayName.Trim();
        var configured = !string.IsNullOrWhiteSpace(profile.ConnectionString);
        var identity = SafeIdentity(profile);
        return new NamedSourceSummaryDto(name, provider, displayName, identity, configured);
    }

    internal ISourceDataSession OpenConfigured(string name)
    {
        if (!TryResolve(name, out var profile, out var error))
            throw error;

        if (string.IsNullOrWhiteSpace(profile.ConnectionString))
            throw new InvalidOperationException("Source is not configured.");

        return _sessionFactory.Create(profile.Provider, profile.ConnectionString);
    }

    private bool TryResolve(string? name, out DataSourceProfileOptions profile, out Exception error)
    {
        profile = null!;
        error = null!;
        if (string.IsNullOrWhiteSpace(name) || !IsSafeSourceName(name))
        {
            error = new KeyNotFoundException("Source was not found.");
            return false;
        }

        var sources = _options.Sources ?? new Dictionary<string, DataSourceProfileOptions>(StringComparer.OrdinalIgnoreCase);
        if (!sources.TryGetValue(name.Trim(), out var found) || found is null)
        {
            error = new KeyNotFoundException("Source was not found.");
            return false;
        }

        profile = found;
        return true;
    }

    internal static bool IsSafeSourceName(string name)
        => name.Length is >= 1 and <= 64
           && char.IsAsciiLetterOrDigit(name[0])
           && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.');

    private static string SafeIdentity(DataSourceProfileOptions profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ConnectionString))
            return "Data Source=(not configured);Initial Catalog=(not configured)";

        return SqlServerConnectionDiagnostics.ToSourceIdentity(profile.ConnectionString);
    }

    private void Audit(string action, string name, string provider, string identity, string category)
    {
        _logger.LogInformation(
            "Data source discovery {Action} name={Name} provider={Provider} identity={Identity} category={Category}",
            action,
            name,
            provider,
            identity,
            category);
    }
}

public sealed record NamedSourceSummaryDto(
    string Name,
    string Provider,
    string DisplayName,
    string Identity,
    bool Configured);

public sealed record SourceConnectionTestDto(
    bool Success,
    string Category,
    string Identity,
    string? Message);

public sealed record SourceTablesDto(
    string Name,
    IReadOnlyList<string> Schemas,
    IReadOnlyList<string> Tables);

public sealed record SourceColumnsDto(
    string Name,
    string Table,
    IReadOnlyList<string> Columns);
