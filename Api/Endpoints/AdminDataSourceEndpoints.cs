using Api.Services.DataSources;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Trendplus2.Endpoints;

namespace Api.Endpoints;

public static class AdminDataSourceEndpoints
{
    private const string ProfileSectionPath = "DataSources:NamedProfiles";

    public static void MapAdminDataSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/data-sources")
            .WithTags("Admin", "Data Sources");

        group.MapGet("/profiles", GetProfiles)
            .WithName("GetDataSourceProfiles")
            .WithSummary("List named source profiles without secrets")
            .Produces<IReadOnlyList<NamedDataSourceProfileDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/{profileName}/test", TestConnection)
            .WithName("TestDataSourceConnection")
            .WithSummary("Test a named source connection with safe error categories")
            .RequireRateLimiting("strict")
            .Produces<DataSourceConnectionTestResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{profileName}/schemas", GetSchemas)
            .WithName("GetDataSourceSchemas")
            .WithSummary("List schemas for a named source")
            .RequireRateLimiting("db-heavy")
            .Produces<DataSourceSchemasResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{profileName}/tables", GetTables)
            .WithName("GetDataSourceTables")
            .WithSummary("List tables for a named source schema")
            .RequireRateLimiting("db-heavy")
            .Produces<DataSourceTablesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{profileName}/columns", GetColumns)
            .WithName("GetDataSourceColumns")
            .WithSummary("List columns for a named source table")
            .RequireRateLimiting("db-heavy")
            .Produces<DataSourceColumnsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static Task<IResult> GetProfiles(
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var denial = Authorize(context, configuration);
        if (denial is not null)
            return Task.FromResult<IResult>(denial);

        var profiles = LoadProfiles(configuration)
            .Select(profile => new NamedDataSourceProfileDto(
                profile.Name,
                profile.Provider,
                profile.DisplayName,
                profile.IsConfigured))
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IResult>(TypedResults.Ok(profiles));
    }

    private static async Task<IResult> TestConnection(
        string profileName,
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var denial = Authorize(context, configuration);
        if (denial is not null)
            return denial;

        if (!TryResolveProfile(configuration, profileName, out var profile, out var notFound))
            return notFound;

        if (!profile.IsConfigured)
        {
            return TypedResults.Ok(new DataSourceConnectionTestResponse(
                ProfileName: profile.Name,
                Provider: profile.Provider,
                Success: false,
                Category: "invalid_configuration",
                Message: "Connection string is missing or blank."));
        }

        if (!string.Equals(profile.Provider, "sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Ok(new DataSourceConnectionTestResponse(
                ProfileName: profile.Name,
                Provider: profile.Provider,
                Success: false,
                Category: "unsupported_provider",
                Message: $"Provider '{profile.Provider}' is not supported by this discovery path."));
        }

        try
        {
            await using var session = new SqlServerSourceDataSession(profile.ConnectionString!);
            await session.TestConnectionAsync(ct);
            return TypedResults.Ok(new DataSourceConnectionTestResponse(
                ProfileName: profile.Name,
                Provider: profile.Provider,
                Success: true,
                Category: "success",
                Message: "Connection test succeeded."));
        }
        catch (Exception ex)
        {
            return TypedResults.Ok(MapConnectionTestFailure(profile, ex));
        }
    }

    private static async Task<IResult> GetSchemas(
        string profileName,
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var denial = Authorize(context, configuration);
        if (denial is not null)
            return denial;

        if (!TryResolveProfile(configuration, profileName, out var profile, out var notFound))
            return notFound;

        var session = CreateSession(profile);
        if (session is null)
            return sessionError(profile, "unsupported_provider", $"Provider '{profile.Provider}' is not supported by this discovery path.");

        await using (session)
        {
            try
            {
                var schemas = await session.GetTablesAsync(ct: ct);
                var distinctSchemas = schemas
                    .Select(ParseSchemaName)
                    .Where(schema => !string.IsNullOrWhiteSpace(schema))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(schema => schema, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return TypedResults.Ok(new DataSourceSchemasResponse(profile.Name, profile.Provider, distinctSchemas));
            }
            catch (Exception ex)
            {
                return sessionError(profile, MapConnectionTestFailure(profile, ex).Category, "Unable to read schemas for this source.");
            }
        }
    }

    private static async Task<IResult> GetTables(
        string profileName,
        HttpContext context,
        IConfiguration configuration,
        [FromQuery] string? schema,
        CancellationToken ct = default)
    {
        var denial = Authorize(context, configuration);
        if (denial is not null)
            return denial;

        if (!TryResolveProfile(configuration, profileName, out var profile, out var notFound))
            return notFound;

        if (string.IsNullOrWhiteSpace(schema))
            return TypedResults.BadRequest(new { error = "schema is required." });

        var session = CreateSession(profile);
        if (session is null)
            return sessionError(profile, "unsupported_provider", $"Provider '{profile.Provider}' is not supported by this discovery path.");

        await using (session)
        {
            try
            {
                var tables = await session.GetTablesAsync(ct: ct);
                var tableNames = new List<string>();
                foreach (var table in tables)
                {
                    var split = SplitQualifiedTableName(table);
                    if (split is null)
                        continue;

                    if (!string.Equals(split.Value.Schema, schema, StringComparison.OrdinalIgnoreCase))
                        continue;

                    tableNames.Add(split.Value.Table);
                }

                tableNames.Sort(StringComparer.OrdinalIgnoreCase);

                return TypedResults.Ok(new DataSourceTablesResponse(profile.Name, profile.Provider, schema, tableNames));
            }
            catch (Exception ex)
            {
                return sessionError(profile, MapConnectionTestFailure(profile, ex).Category, "Unable to read tables for this source.");
            }
        }
    }

    private static async Task<IResult> GetColumns(
        string profileName,
        HttpContext context,
        IConfiguration configuration,
        [FromQuery] string? schema,
        [FromQuery] string? table,
        CancellationToken ct = default)
    {
        var denial = Authorize(context, configuration);
        if (denial is not null)
            return denial;

        if (!TryResolveProfile(configuration, profileName, out var profile, out var notFound))
            return notFound;

        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
            return TypedResults.BadRequest(new { error = "schema and table are required." });

        var session = CreateSession(profile);
        if (session is null)
            return sessionError(profile, "unsupported_provider", $"Provider '{profile.Provider}' is not supported by this discovery path.");

        await using (session)
        {
            try
            {
                var columns = await session.GetColumnsAsync($"{schema}.{table}", ct);
                return TypedResults.Ok(new DataSourceColumnsResponse(profile.Name, profile.Provider, schema, table, columns));
            }
            catch (Exception ex)
            {
                return sessionError(profile, MapConnectionTestFailure(profile, ex).Category, "Unable to read columns for this source.");
            }
        }
    }

    private static ISourceDataSession? CreateSession(NamedDataSourceProfile profile)
    {
        if (string.Equals(profile.Provider, "sqlserver", StringComparison.OrdinalIgnoreCase))
            return new SqlServerSourceDataSession(profile.ConnectionString ?? string.Empty);

        return null;
    }

    private static IResult? Authorize(HttpContext context, IConfiguration configuration)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return null;
    }

    private static bool TryResolveProfile(
        IConfiguration configuration,
        string profileName,
        out NamedDataSourceProfile profile,
        out IResult notFound)
    {
        var profiles = LoadProfiles(configuration);
        NamedDataSourceProfile? resolved = profiles.FirstOrDefault(x => string.Equals(x.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (resolved is null)
        {
            notFound = TypedResults.NotFound(new
            {
                errorCategory = "profile_not_found",
                profile = profileName
            });
            profile = NamedDataSourceProfile.Empty;
            return false;
        }

        profile = resolved;
        notFound = Results.NotFound();
        return true;
    }

    private static IReadOnlyList<NamedDataSourceProfile> LoadProfiles(IConfiguration configuration)
    {
        var section = configuration.GetSection(ProfileSectionPath);
        var profiles = new List<NamedDataSourceProfile>();

        foreach (var child in section.GetChildren())
        {
            var provider = NormalizeProvider(child["Provider"]);
            profiles.Add(new NamedDataSourceProfile(
                Name: child.Key,
                Provider: provider,
                ConnectionString: child["ConnectionString"],
                DisplayName: child["DisplayName"]));
        }

        return profiles;
    }

    private static string NormalizeProvider(string? provider)
        => string.IsNullOrWhiteSpace(provider) ? "sqlserver" : provider.Trim().ToLowerInvariant();

    private static DataSourceConnectionTestResponse MapConnectionTestFailure(NamedDataSourceProfile profile, Exception ex)
    {
        var category = ClassifyFailure(ex);
        return new DataSourceConnectionTestResponse(
            ProfileName: profile.Name,
            Provider: profile.Provider,
            Success: false,
            Category: category,
            Message: category switch
            {
                "invalid_configuration" => "Connection string is missing or invalid.",
                "authentication_failed" => "Authentication failed.",
                "permission_denied" => "Permission denied.",
                "timeout" => "Connection timed out.",
                "network_error" => "Network or server is unavailable.",
                "database_unavailable" => "Database is unavailable.",
                "unsupported_provider" => "Provider is not supported.",
                "cancelled" => "Connection test was cancelled.",
                _ => "Connection test failed."
            });
    }

    private static IResult sessionError(NamedDataSourceProfile profile, string category, string message)
        => TypedResults.Ok(new DataSourceConnectionTestResponse(
            ProfileName: profile.Name,
            Provider: profile.Provider,
            Success: false,
            Category: category,
            Message: message));

    private static string ClassifyFailure(Exception ex)
    {
        if (ex is OperationCanceledException)
            return "cancelled";

        if (ex is ArgumentException or InvalidOperationException)
            return "invalid_configuration";

        if (ex is TimeoutException)
            return "timeout";

        if (ex is SqlException sqlException)
        {
            return sqlException.Number switch
            {
                -2 => "timeout",
                18456 or 18452 or 18461 or 18487 or 18488 => "authentication_failed",
                229 or 230 or 297 => "permission_denied",
                4060 or 40615 or 50000 => "database_unavailable",
                53 or 64 or 121 => "network_error",
                _ => "unknown_error"
            };
        }

        return "unknown_error";
    }

    private static string ParseSchemaName(string tableReference)
    {
        var split = SplitQualifiedTableName(tableReference);
        return split?.Schema ?? string.Empty;
    }

    private static (string Schema, string Table)? SplitQualifiedTableName(string tableReference)
    {
        if (string.IsNullOrWhiteSpace(tableReference))
            return null;

        var parts = tableReference.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        return (parts[0], parts[1]);
    }

    private sealed record NamedDataSourceProfile(string Name, string Provider, string? ConnectionString, string? DisplayName)
    {
        public static NamedDataSourceProfile Empty { get; } = new(string.Empty, string.Empty, null, null);

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
    }
}

public sealed record NamedDataSourceProfileDto(string Name, string Provider, string? DisplayName, bool Configured);

public sealed record DataSourceConnectionTestResponse(string ProfileName, string Provider, bool Success, string Category, string Message);

public sealed record DataSourceSchemasResponse(string ProfileName, string Provider, IReadOnlyList<string> Schemas);

public sealed record DataSourceTablesResponse(string ProfileName, string Provider, string Schema, IReadOnlyList<string> Tables);

public sealed record DataSourceColumnsResponse(string ProfileName, string Provider, string Schema, string Table, IReadOnlyList<string> Columns);
