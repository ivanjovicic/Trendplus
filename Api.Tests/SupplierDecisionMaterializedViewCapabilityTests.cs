using Infrastructure.Analytics;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class SupplierDecisionMaterializedViewCapabilityTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public SupplierDecisionMaterializedViewCapabilityTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InspectAsync_DistinguishesReadyMissingColumnsUnpopulatedAndMissingObject()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_mv_capability_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var setup = new NpgsqlCommand(
            """
            CREATE TABLE public.mv_capability_source (
                id integer NOT NULL,
                required_value text NULL
            );

            INSERT INTO public.mv_capability_source (id, required_value)
            VALUES (1, 'ready');

            CREATE MATERIALIZED VIEW public.mv_capability_ready AS
            SELECT id, required_value
            FROM public.mv_capability_source;

            CREATE MATERIALIZED VIEW public.mv_capability_missing_column AS
            SELECT id
            FROM public.mv_capability_source;

            CREATE MATERIALIZED VIEW public.mv_capability_unpopulated AS
            SELECT id, required_value
            FROM public.mv_capability_source
            WITH NO DATA;
            """,
            connection))
        {
            await setup.ExecuteNonQueryAsync();
        }

        var requiredColumns = new[] { "id", "required_value" };

        var ready = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_capability_ready",
            requiredColumns);
        var missingColumns = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_capability_missing_column",
            requiredColumns);
        var unpopulated = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_capability_unpopulated",
            requiredColumns);
        var missingObject = await PostgresMaterializedViewCapabilityReader.InspectAsync(
            connection,
            "mv_capability_absent",
            requiredColumns);

        Assert.True(ready.Exists);
        Assert.True(ready.IsPopulated);
        Assert.True(ready.HasRequiredColumns);
        Assert.True(ready.IsReady);
        Assert.Null(ready.ErrorCode);

        Assert.True(missingColumns.Exists);
        Assert.True(missingColumns.IsPopulated);
        Assert.False(missingColumns.IsReady);
        Assert.Equal("MISSING_COLUMNS", missingColumns.ErrorCode);
        Assert.Equal(new[] { "required_value" }, missingColumns.MissingColumns);

        Assert.True(unpopulated.Exists);
        Assert.False(unpopulated.IsPopulated);
        Assert.True(unpopulated.HasRequiredColumns);
        Assert.False(unpopulated.IsReady);
        Assert.Equal("NOT_POPULATED", unpopulated.ErrorCode);

        Assert.False(missingObject.Exists);
        Assert.False(missingObject.IsPopulated);
        Assert.False(missingObject.IsReady);
        Assert.Equal("MISSING_OBJECT", missingObject.ErrorCode);
        Assert.Equal(requiredColumns.OrderBy(value => value, StringComparer.Ordinal), missingObject.MissingColumns);
    }
}
