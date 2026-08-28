using Api.Config;
using Api.Models;
using Api.Services.DataSources;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class DataSourceMappingPreviewServiceTests
{
    [Fact]
    public async Task PreviewAsync_ComputesStableFingerprintAndRejectsBrokenRows()
    {
        var service = CreateService(() => new ProbeSourceSession(
            columns:
            [
                new SourceColumnDefinition("Order ID", "bigint", false, 0),
                new SourceColumnDefinition("Updated At", "datetime2", true, 1),
                new SourceColumnDefinition("Product Name", "nvarchar", true, 2),
                new SourceColumnDefinition("Price", "nvarchar", true, 3),
                new SourceColumnDefinition("Quantity", "int", true, 4)
            ],
            rows:
            [
                CreateRow(["Order ID", "Updated At", "Product Name", "Price", "Quantity"], [1L, new DateTime(2026, 8, 27, 9, 0, 0, DateTimeKind.Utc), "  Boot  ", "12.50", 3]),
                CreateRow(["Order ID", "Updated At", "Product Name", "Price", "Quantity"], [2L, null, "   ", "oops", 1]),
                CreateRow(["Order ID", "Updated At", "Product Name", "Price", "Quantity"], [2L, new DateTime(2026, 8, 27, 11, 0, 0, DateTimeKind.Utc), "Sandal", "15.00", 2])
            ]));

        var request = new DataSourceMappingPreviewRequest
        {
            CanonicalEntity = "Product",
            Table = "[sales].[OrderPreview]",
            ExternalKeyColumns = ["Order ID"],
            Cursor = new DataSourceCursorSelection
            {
                Mode = "timestamp_then_id",
                TimestampColumn = "Updated At",
                TieBreakerColumn = "Order ID"
            },
            ColumnMappings =
            [
                new DataSourceFieldMappingSelection
                {
                    TargetField = "name",
                    SourceColumn = "Product Name",
                    Transforms = ["trim", "empty_to_null"]
                },
                new DataSourceFieldMappingSelection
                {
                    TargetField = "price",
                    SourceColumn = "Price"
                },
                new DataSourceFieldMappingSelection
                {
                    TargetField = "quantity",
                    SourceColumn = "Quantity"
                }
            ],
            SampleSize = 10
        };

        var first = await service.PreviewAsync("probe", request);
        var second = await service.PreviewAsync("probe", request);

        Assert.True(first.CanPreview);
        Assert.False(first.CanSync);
        Assert.Equal(first.SchemaFingerprint, second.SchemaFingerprint);
        Assert.Equal(3, first.RowCount);
        Assert.Equal("exact", first.RowCountMode);
        Assert.Equal(3, first.PreviewedRows);

        var nameMapping = Assert.Single(first.FieldMappings.Where(mapping => mapping.TargetField == "name"));
        Assert.Equal("mapped", nameMapping.Status);

        var optionalSku = Assert.Single(first.FieldMappings.Where(mapping => mapping.TargetField == "sku"));
        Assert.Equal("missing", optionalSku.Status);

        Assert.Collection(
            first.PreviewRows.OrderBy(row => row.RowIndex),
            row1 =>
            {
                Assert.Equal("accepted", row1.Status);
                Assert.Equal("1", row1.ExternalKey);
                var nameField = Assert.Single(row1.Fields.Where(field => field.TargetField == "name"));
                Assert.Equal("Boot", nameField.ParsedValue);
            },
            row2 =>
            {
                Assert.Equal("rejected", row2.Status);
                Assert.Contains(row2.RejectionReasons, reason => reason.Contains("Required field 'name' is empty.", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(row2.RejectionReasons, reason => reason.Contains("not a valid decimal", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(row2.RejectionReasons, reason => reason.Contains("Cursor timestamp", StringComparison.OrdinalIgnoreCase));
            },
            row3 =>
            {
                Assert.Equal("rejected", row3.Status);
                Assert.Contains(row3.RejectionReasons, reason => reason.Contains("Duplicate external key '2'", StringComparison.OrdinalIgnoreCase));
            });

        Assert.Contains(first.Warnings, warning => warning.Contains("duplicate external key '2'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PreviewAsync_FlagsDuplicateAndUnknownTargetMappings()
    {
        var service = CreateService(() => new ProbeSourceSession(
            columns:
            [
                new SourceColumnDefinition("Supplier ID", "bigint", false, 0),
                new SourceColumnDefinition("Supplier Name", "nvarchar", false, 1)
            ],
            rows:
            [
                CreateRow(["Supplier ID", "Supplier Name"], [10L, "Alpha"])
            ]));

        var response = await service.PreviewAsync("probe", new DataSourceMappingPreviewRequest
        {
            CanonicalEntity = "Supplier",
            Table = "[dbo].[Supplier]",
            ExternalKeyColumns = ["Supplier ID"],
            ColumnMappings =
            [
                new DataSourceFieldMappingSelection
                {
                    TargetField = "name",
                    SourceColumn = "Supplier Name"
                },
                new DataSourceFieldMappingSelection
                {
                    TargetField = "name",
                    SourceColumn = "Supplier Name"
                },
                new DataSourceFieldMappingSelection
                {
                    TargetField = "ghost_field",
                    SourceColumn = "Supplier Name"
                }
            ]
        });

        Assert.False(response.CanSync);
        Assert.Contains(response.FieldMappings, mapping => mapping.Status == "duplicate_target" && mapping.TargetField == "name");
        Assert.Contains(response.FieldMappings, mapping => mapping.Status == "invalid_target" && mapping.TargetField == "ghost_field");
    }

    private static SourceDataRow CreateRow(IReadOnlyList<string> columns, object?[] values)
        => new(new SourceDataSchema(columns), values);

    private static DataSourceMappingPreviewService CreateService(Func<ISourceDataSession> sessionFactory)
    {
        var options = Options.Create(new DataSourceOptions
        {
            PreviewSampleLimit = 5,
            PreviewTimeoutSeconds = 30
        });

        return new DataSourceMappingPreviewService(
            new ProbeCatalog(),
            new ProbeSessionFactory(sessionFactory),
            options);
    }

    private sealed class ProbeCatalog : IDataSourceProfileCatalog
    {
        private static readonly NamedDataSourceProfile Profile = new(
            "probe",
            "sqlserver",
            "probe",
            true,
            "Server=probe;Database=probe;",
            null,
            "dbo",
            "Probe profile",
            30);

        public IReadOnlyList<DataSourceProfileSummary> ListProfiles()
            => [new DataSourceProfileSummary(Profile.Name, Profile.Provider, Profile.Mode, Profile.Enabled, Profile.DefaultSchema, Profile.Description)];

        public bool TryGetProfile(string profileName, out NamedDataSourceProfile profile, out string? error)
        {
            if (string.Equals(profileName, Profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                profile = Profile;
                error = null;
                return true;
            }

            profile = default!;
            error = $"Data source profile '{profileName}' was not found.";
            return false;
        }
    }

    private sealed class ProbeSessionFactory : ISourceDataSessionFactory
    {
        private readonly Func<ISourceDataSession> _sessionFactory;

        public ProbeSessionFactory(Func<ISourceDataSession> sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public ISourceDataSession Create(NamedDataSourceProfile profile) => _sessionFactory();
    }

    private sealed class ProbeSourceSession : ISourceDataSession
    {
        private readonly IReadOnlyList<SourceColumnDefinition> _columns;
        private readonly IReadOnlyList<SourceDataRow> _rows;

        public ProbeSourceSession(IReadOnlyList<SourceColumnDefinition> columns, IReadOnlyList<SourceDataRow> rows)
        {
            _columns = columns;
            _rows = rows;
        }

        public string Provider => "sqlserver";

        public string Mode => "probe";

        public string SourceIdentity => "probe://source";

        public SourceCapabilities Capabilities => new();

        public Task TestConnectionAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetTablesAsync(bool includeTemporaryTables = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(["[sales].[OrderPreview]", "[dbo].[Supplier]"]);

        public Task<IReadOnlyList<SourceColumnDefinition>> GetColumnDefinitionsAsync(string table, CancellationToken ct = default)
            => Task.FromResult(_columns);

        public Task<IReadOnlyList<string>> GetColumnsAsync(string table, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(_columns.Select(column => column.Name).ToArray());

        public Task<SourceRowCountResult> TryGetRowCountAsync(string table, CancellationToken ct = default)
            => Task.FromResult(SourceRowCountResult.Exact(_rows.Count));

        public IAsyncEnumerable<SourceDataRow> ReadRowsAsync(string table, CancellationToken ct = default)
            => ReadRowsAsync(table, query: null, ct);

        public async IAsyncEnumerable<SourceDataRow> ReadRowsAsync(
            string table,
            SourceReadQuery? query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var row in _rows)
            {
                ct.ThrowIfCancellationRequested();
                yield return row;
                await Task.Yield();
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
