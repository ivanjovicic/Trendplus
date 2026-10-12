using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using System.Text.RegularExpressions;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsModelCatalogDriftPostgresTests
{
    private static readonly DateOnly AllowlistSunset = new(2026, 12, 31);

    private static readonly IReadOnlyDictionary<SchemaObjectKey, ColumnDriftAllowance> ColumnDriftAllowlist =
        new Dictionary<SchemaObjectKey, ColumnDriftAllowance>
        {
            [new("public", "analytics_refresh_runs", "JobKey")] = new(
                "character varying(120)", false,
                "character varying(128)", false,
                "RQ629 deliberately retained the pre-existing wider JobKey column to avoid rejecting legacy values.",
                "RQ630 / analytics migrations",
                AllowlistSunset),
            [new("public", "analytics_refresh_runs", "ErrorMessage")] = new(
                "character varying(2000)", true,
                "character varying(4000)", true,
                "RQ629 deliberately retained the pre-existing wider ErrorMessage column to avoid truncating legacy values.",
                "RQ630 / analytics migrations",
                AllowlistSunset),
            [new("public", "analytics_refresh_runs", "CorrelationId")] = new(
                "character varying(100)", true,
                "character varying(120)", true,
                "RQ629 deliberately retained the pre-existing wider CorrelationId column to avoid rejecting legacy values.",
                "RQ630 / analytics migrations",
                AllowlistSunset),
        };

    private static readonly IReadOnlyDictionary<SchemaObjectKey, IndexDriftAllowance> IndexDriftAllowlist =
        new Dictionary<SchemaObjectKey, IndexDriftAllowance>
        {
            [new("public", "analytics_refresh_runs", "idx_analytics_refresh_runs_job_started")] = new(
                new IndexShape(false, ["JobKey", "StartedAtUtc"], [false, true], null),
                new IndexShape(false, ["JobKey", "StartedAtUtc"], [false, false], null),
                "RQ629 documents the historical ascending StartedAtUtc index as a performance-only mismatch with the model's descending order.",
                "RQ630 / analytics migrations",
                AllowlistSunset),
        };

    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsModelCatalogDriftPostgresTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigratedPostgresCatalog_MatchesAnalyticsAndCoreEfModelsExceptReviewedDrift()
    {
        var database = await _fixture.CreateDatabaseAsync("ef_model_catalog_drift");
        await ApplyCiEfMigrationsAsync(database.ConnectionString);

        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var analyticsContext = new AnalyticsDbContext(options);

        var (columns, indexes) = BuildModelContracts(analyticsContext.Model);
        var coreOptions = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var coreContext = new TrendplusDbContext(coreOptions);
        var (coreColumns, coreIndexes) = BuildModelContracts(coreContext.Model);
        MergeContracts(columns, indexes, coreColumns, coreIndexes);

        var migrationFailures = await CompareCatalogAsync(database.ConnectionString, columns, indexes);
        Assert.True(
            migrationFailures.Count == 0,
            "Catalog differs from the EF models immediately after migrations:" +
            Environment.NewLine + string.Join(Environment.NewLine, migrationFailures));

        await ApplyCiStartupSqlAsync(database.ConnectionString);
        var startupSqlFailures = await CompareCatalogAsync(database.ConnectionString, columns, indexes);
        Assert.True(
            startupSqlFailures.Count == 0,
            "Catalog differs from the EF models after the CI startup SQL bootstrap:" +
            Environment.NewLine + string.Join(Environment.NewLine, startupSqlFailures));
    }

    private static async Task<IReadOnlyList<string>> CompareCatalogAsync(
        string connectionString,
        IReadOnlyDictionary<SchemaObjectKey, ColumnContract> columns,
        IReadOnlyDictionary<SchemaObjectKey, IndexContract> indexes)
    {
        var failures = new List<string>();
        var usedColumnAllowlist = new HashSet<SchemaObjectKey>();
        var usedIndexAllowlist = new HashSet<SchemaObjectKey>();
        ValidateAllowlistMetadata(DateOnly.FromDateTime(DateTime.UtcNow), failures);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var actualColumns = await ReadCatalogColumnsAsync(connection);
        foreach (var (key, expected) in columns)
        {
            if (!actualColumns.TryGetValue(key, out var actual))
            {
                failures.Add($"Missing model column {key} (expected {expected.StoreType}, nullable={expected.IsNullable}).");
                continue;
            }

            if (ColumnMatches(expected, actual))
                continue;

            if (ColumnDriftAllowlist.TryGetValue(key, out var allowance)
                && ColumnAllowanceMatches(expected, actual, allowance))
            {
                usedColumnAllowlist.Add(key);
                continue;
            }

            failures.Add(
                $"Column drift at {key}: model expects {expected.StoreType}, nullable={expected.IsNullable}; " +
                $"catalog has {actual.StoreType}, nullable={actual.IsNullable}.");
        }

        var actualIndexesByTable = new Dictionary<TableKey, IReadOnlyDictionary<SchemaObjectKey, ActualIndex>>();
        foreach (var expected in indexes.Values)
        {
            var tableKey = new TableKey(expected.Key.Schema, expected.Key.Table);
            if (!actualIndexesByTable.TryGetValue(tableKey, out var tableIndexes))
            {
                tableIndexes = await ReadCatalogIndexesAsync(connection, tableKey);
                actualIndexesByTable.Add(tableKey, tableIndexes);
            }

            if (!tableIndexes.TryGetValue(expected.Key, out var actual))
            {
                failures.Add($"Missing model index {expected.Key}.");
                continue;
            }

            if (!actual.IsValid)
            {
                failures.Add($"Model index {expected.Key} exists in the catalog but is invalid.");
                continue;
            }

            var expectedFilter = await CanonicalizeFilterAsync(connection, expected);
            var expectedShape = new IndexShape(
                expected.IsUnique,
                expected.Columns,
                expected.IsDescending,
                expectedFilter);

            if (IndexShapesMatch(expectedShape, actual.Shape))
                continue;

            if (IndexDriftAllowlist.TryGetValue(expected.Key, out var allowance)
                && IndexAllowanceMatches(expectedShape, actual.Shape, allowance))
            {
                usedIndexAllowlist.Add(expected.Key);
                continue;
            }

            failures.Add(
                $"Index drift at {expected.Key}: model expects {FormatIndex(expectedShape)}; " +
                $"catalog has {FormatIndex(actual.Shape)}.");
        }

        AddUnusedAllowlistFailures(ColumnDriftAllowlist.Keys, usedColumnAllowlist, "column", failures);
        AddUnusedAllowlistFailures(IndexDriftAllowlist.Keys, usedIndexAllowlist, "index", failures);

        return failures;
    }

    private static async Task ApplyCiEfMigrationsAsync(string connectionString)
    {
        var coreOptions = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using (var coreContext = new TrendplusDbContext(coreOptions))
            await coreContext.Database.MigrateAsync();

        var analyticsOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using (var analyticsContext = new AnalyticsDbContext(analyticsOptions))
            await analyticsContext.Database.MigrateAsync();

    }

    private static async Task ApplyCiStartupSqlAsync(string connectionString)
    {
        var repositoryRoot = FindRepositoryRoot();
        var startupSqlFiles = new[]
        {
            "Database/Migrations/002_CreatePerformanceLogsTable.sql",
            "Database/Migrations/012_AddAccessImportSupport.sql",
            "Database/Migrations/017_CreateNightlyAnalyticsMaterializedViews.sql",
            "Database/Migrations/013_AddVendorSalesNivelacijaViews.sql",
            "Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql",
            "Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql",
            "Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql",
        };

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var relativePath in startupSqlFiles)
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(repositoryRoot, relativePath));
            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = 300,
            };
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                var workflowPath = Path.Combine(directory.FullName, ".github", "workflows", "analytics-tests.yml");
                if (File.Exists(workflowPath))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root containing analytics-tests.yml.");
    }

    private static (Dictionary<SchemaObjectKey, ColumnContract> Columns,
        Dictionary<SchemaObjectKey, IndexContract> Indexes) BuildModelContracts(IModel model)
    {
        var columns = new Dictionary<SchemaObjectKey, ColumnContract>();
        var indexes = new Dictionary<SchemaObjectKey, IndexContract>();

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
                continue;

            var schema = entityType.GetSchema() ?? "public";
            var storeObject = StoreObjectIdentifier.Table(tableName, schema);

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(columnName))
                    continue;

                var key = new SchemaObjectKey(schema, tableName, columnName);
                var contract = new ColumnContract(
                    property.GetColumnType() ?? property.GetRelationalTypeMapping().StoreType,
                    property.IsNullable);
                if (columns.TryGetValue(key, out var existing))
                    Assert.Equal(existing, contract);
                else
                    columns.Add(key, contract);
            }

            foreach (var index in entityType.GetIndexes())
            {
                var indexName = index.GetDatabaseName(storeObject);
                if (string.IsNullOrWhiteSpace(indexName))
                    continue;

                var propertyColumns = index.Properties
                    .Select(property => property.GetColumnName(storeObject)
                        ?? throw new InvalidOperationException(
                            $"Index {entityType.DisplayName()}.{indexName} contains an unmapped property."))
                    .ToArray();
                var configuredSort = index.IsDescending;
                var descending = configuredSort is null || configuredSort.Count == 0
                    ? new bool[propertyColumns.Length]
                    : configuredSort.ToArray();
                if (descending.Length != propertyColumns.Length)
                {
                    throw new InvalidOperationException(
                        $"Index {entityType.DisplayName()}.{indexName} declares {descending.Length} sort flags for " +
                        $"{propertyColumns.Length} properties.");
                }

                var key = new SchemaObjectKey(schema, tableName, indexName);
                var contract = new IndexContract(
                    key,
                    index.IsUnique,
                    propertyColumns,
                    descending,
                    index.GetFilter());
                if (indexes.TryGetValue(key, out var existing))
                {
                    Assert.Equal(existing.IsUnique, contract.IsUnique);
                    Assert.Equal(existing.Columns, contract.Columns);
                    Assert.Equal(existing.IsDescending, contract.IsDescending);
                    Assert.Equal(existing.Filter, contract.Filter);
                }
                else
                {
                    indexes.Add(key, contract);
                }
            }
        }

        return (columns, indexes);
    }

    private static void MergeContracts(
        IDictionary<SchemaObjectKey, ColumnContract> columns,
        IDictionary<SchemaObjectKey, IndexContract> indexes,
        IReadOnlyDictionary<SchemaObjectKey, ColumnContract> additionalColumns,
        IReadOnlyDictionary<SchemaObjectKey, IndexContract> additionalIndexes)
    {
        foreach (var (key, contract) in additionalColumns)
        {
            if (columns.TryGetValue(key, out var existing))
                Assert.Equal(existing, contract);
            else
                columns.Add(key, contract);
        }

        foreach (var (key, contract) in additionalIndexes)
        {
            if (indexes.TryGetValue(key, out var existing))
            {
                Assert.Equal(existing.IsUnique, contract.IsUnique);
                Assert.Equal(existing.Columns, contract.Columns);
                Assert.Equal(existing.IsDescending, contract.IsDescending);
                Assert.Equal(existing.Filter, contract.Filter);
            }
            else
            {
                indexes.Add(key, contract);
            }
        }
    }

    private static async Task<Dictionary<SchemaObjectKey, ActualColumn>> ReadCatalogColumnsAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT namespace.nspname,
                   relation.relname,
                   attribute.attname,
                   pg_catalog.format_type(attribute.atttypid, attribute.atttypmod),
                   NOT attribute.attnotnull
            FROM pg_catalog.pg_attribute AS attribute
            JOIN pg_catalog.pg_class AS relation
              ON relation.oid = attribute.attrelid
            JOIN pg_catalog.pg_namespace AS namespace
              ON namespace.oid = relation.relnamespace
            WHERE attribute.attnum > 0
              AND NOT attribute.attisdropped
              AND relation.relkind IN ('r', 'p');
            """,
            connection);

        var columns = new Dictionary<SchemaObjectKey, ActualColumn>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = new SchemaObjectKey(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            columns.Add(key, new ActualColumn(reader.GetString(3), reader.GetBoolean(4)));
        }

        return columns;
    }

    private static async Task<IReadOnlyDictionary<SchemaObjectKey, ActualIndex>> ReadCatalogIndexesAsync(
        NpgsqlConnection connection,
        TableKey table)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT index_relation.relname,
                   index_data.indisunique,
                   index_data.indisvalid,
                   pg_catalog.pg_get_expr(index_data.indpred, index_data.indrelid, true),
                   ARRAY(
                       SELECT attribute.attname
                       FROM unnest(index_data.indkey) WITH ORDINALITY AS key(attnum, ordinality)
                       LEFT JOIN pg_catalog.pg_attribute AS attribute
                         ON attribute.attrelid = index_data.indrelid
                        AND attribute.attnum = key.attnum
                       WHERE key.ordinality <= index_data.indnkeyatts
                       ORDER BY key.ordinality
                   ),
                   ARRAY(
                       SELECT pg_catalog.pg_get_indexdef(
                           index_data.indexrelid,
                           key.ordinality::integer,
                           true)
                       FROM generate_series(1, index_data.indnkeyatts::integer)
                           WITH ORDINALITY AS key(position, ordinality)
                       ORDER BY key.ordinality
                   )
            FROM pg_catalog.pg_index AS index_data
            JOIN pg_catalog.pg_class AS index_relation
              ON index_relation.oid = index_data.indexrelid
            JOIN pg_catalog.pg_class AS table_relation
              ON table_relation.oid = index_data.indrelid
            JOIN pg_catalog.pg_namespace AS namespace
              ON namespace.oid = table_relation.relnamespace
            WHERE namespace.nspname = @schema
              AND table_relation.relname = @table;
            """,
            connection);
        command.Parameters.AddWithValue("schema", table.Schema);
        command.Parameters.AddWithValue("table", table.Table);

        var indexes = new Dictionary<SchemaObjectKey, ActualIndex>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = new SchemaObjectKey(table.Schema, table.Table, reader.GetString(0));
            var columns = reader.GetFieldValue<string[]>(4)
                .Select(name => name ?? "<expression>")
                .ToArray();
            var definitions = reader.GetFieldValue<string[]>(5);
            var descending = definitions.Select(IsDescendingDefinition).ToArray();
            var filter = reader.IsDBNull(3) ? null : reader.GetString(3);
            indexes.Add(key, new ActualIndex(
                reader.GetBoolean(2),
                new IndexShape(reader.GetBoolean(1), columns, descending, filter)));
        }

        return indexes;
    }

    private static async Task<string?> CanonicalizeFilterAsync(
        NpgsqlConnection connection,
        IndexContract expected)
    {
        if (string.IsNullOrWhiteSpace(expected.Filter))
            return null;

        var probeName = $"rq630_filter_probe_{Guid.NewGuid():N}";
        var columns = string.Join(", ", expected.Columns.Select(QuoteIdentifier));
        var createSql =
            $"CREATE INDEX {QuoteIdentifier(probeName)} ON " +
            $"{QuoteIdentifier(expected.Key.Schema)}.{QuoteIdentifier(expected.Key.Table)} " +
            $"({columns}) WHERE {expected.Filter};";

        await using (var create = new NpgsqlCommand(createSql, connection))
            await create.ExecuteNonQueryAsync();

        try
        {
            await using var read = new NpgsqlCommand(
                """
                SELECT pg_catalog.pg_get_expr(index_data.indpred, index_data.indrelid, true)
                FROM pg_catalog.pg_class AS index_relation
                JOIN pg_catalog.pg_index AS index_data
                  ON index_data.indexrelid = index_relation.oid
                JOIN pg_catalog.pg_namespace AS namespace
                  ON namespace.oid = index_relation.relnamespace
                WHERE namespace.nspname = @schema
                  AND index_relation.relname = @indexName;
                """,
                connection);
            read.Parameters.AddWithValue("schema", expected.Key.Schema);
            read.Parameters.AddWithValue("indexName", probeName);
            return (string?)await read.ExecuteScalarAsync();
        }
        finally
        {
            var dropSql =
                $"DROP INDEX IF EXISTS {QuoteIdentifier(expected.Key.Schema)}.{QuoteIdentifier(probeName)};";
            await using var drop = new NpgsqlCommand(dropSql, connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static bool ColumnMatches(ColumnContract expected, ActualColumn actual) =>
        NormalizeStoreType(expected.StoreType) == NormalizeStoreType(actual.StoreType)
        && expected.IsNullable == actual.IsNullable;

    private static bool ColumnAllowanceMatches(
        ColumnContract expected,
        ActualColumn actual,
        ColumnDriftAllowance allowance) =>
        NormalizeStoreType(expected.StoreType) == NormalizeStoreType(allowance.ExpectedStoreType)
        && expected.IsNullable == allowance.ExpectedNullable
        && NormalizeStoreType(actual.StoreType) == NormalizeStoreType(allowance.ActualStoreType)
        && actual.IsNullable == allowance.ActualNullable;

    private static bool IndexShapesMatch(IndexShape expected, IndexShape actual) =>
        expected.IsUnique == actual.IsUnique
        && expected.Columns.SequenceEqual(actual.Columns, StringComparer.Ordinal)
        && expected.IsDescending.SequenceEqual(actual.IsDescending)
        && string.Equals(expected.Filter, actual.Filter, StringComparison.Ordinal);

    private static bool IndexAllowanceMatches(
        IndexShape expected,
        IndexShape actual,
        IndexDriftAllowance allowance) =>
        IndexShapesMatch(expected, allowance.Expected)
        && IndexShapesMatch(actual, allowance.Actual);

    private static void ValidateAllowlistMetadata(DateOnly utcToday, ICollection<string> failures)
    {
        foreach (var (key, allowance) in ColumnDriftAllowlist)
        {
            if (allowance.SunsetUtc < utcToday)
                failures.Add($"Column allowlist entry {key} expired on {allowance.SunsetUtc:yyyy-MM-dd}.");
            if (string.IsNullOrWhiteSpace(allowance.Reason) || string.IsNullOrWhiteSpace(allowance.Owner))
                failures.Add($"Column allowlist entry {key} must include a reason and owner.");
        }

        foreach (var (key, allowance) in IndexDriftAllowlist)
        {
            if (allowance.SunsetUtc < utcToday)
                failures.Add($"Index allowlist entry {key} expired on {allowance.SunsetUtc:yyyy-MM-dd}.");
            if (string.IsNullOrWhiteSpace(allowance.Reason) || string.IsNullOrWhiteSpace(allowance.Owner))
                failures.Add($"Index allowlist entry {key} must include a reason and owner.");
        }
    }

    private static void AddUnusedAllowlistFailures(
        IEnumerable<SchemaObjectKey> allowanceKeys,
        ISet<SchemaObjectKey> usedKeys,
        string objectKind,
        ICollection<string> failures)
    {
        foreach (var key in allowanceKeys)
        {
            if (!usedKeys.Contains(key))
                failures.Add($"The {objectKind} allowlist entry {key} is no longer needed; remove it before its sunset.");
        }
    }

    private static bool IsDescendingDefinition(string definition) =>
        Regex.IsMatch(definition, @"\sDESC(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string NormalizeStoreType(string storeType) =>
        Regex.Replace(storeType.Trim(), @"\s+", " ").ToLowerInvariant();

    private static string FormatIndex(IndexShape shape) =>
        $"unique={shape.IsUnique}; columns=[{string.Join(", ", shape.Columns.Select((column, index) =>
            $"{column}{(shape.IsDescending[index] ? " DESC" : " ASC")}"))}]; filter={shape.Filter ?? "<none>"}";

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record ColumnContract(string StoreType, bool IsNullable);

    private sealed record ActualColumn(string StoreType, bool IsNullable);

    private sealed record IndexContract(
        SchemaObjectKey Key,
        bool IsUnique,
        string[] Columns,
        bool[] IsDescending,
        string? Filter);

    private sealed record IndexShape(
        bool IsUnique,
        string[] Columns,
        bool[] IsDescending,
        string? Filter);

    private sealed record ActualIndex(bool IsValid, IndexShape Shape);

    private sealed record ColumnDriftAllowance(
        string ExpectedStoreType,
        bool ExpectedNullable,
        string ActualStoreType,
        bool ActualNullable,
        string Reason,
        string Owner,
        DateOnly SunsetUtc);

    private sealed record IndexDriftAllowance(
        IndexShape Expected,
        IndexShape Actual,
        string Reason,
        string Owner,
        DateOnly SunsetUtc);

    private readonly record struct TableKey(string Schema, string Table);

    private readonly record struct SchemaObjectKey(string Schema, string Table, string Name)
    {
        public override string ToString() => $"{Schema}.{Table}.{Name}";
    }
}
