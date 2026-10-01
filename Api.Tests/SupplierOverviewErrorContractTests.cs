using Api.Services;
using Npgsql;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class SupplierOverviewErrorContractTests
{
    [Fact]
    public void MissingRelation_IsClassifiedAsSchemaMissing()
    {
        var result = SupplierOverviewErrorContract.Classify(Postgres("42P01"));

        Assert.Equal("SUPPLIER_OVERVIEW_SCHEMA_MISSING", result.ErrorCode);
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain("42P01", result.Detail);
    }

    [Fact]
    public void MissingColumn_IsClassifiedAsSchemaInvalid()
    {
        var result = SupplierOverviewErrorContract.Classify(Postgres("42703"));

        Assert.Equal("SUPPLIER_OVERVIEW_SCHEMA_INVALID", result.ErrorCode);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void QueryCancellation_IsClassifiedAsTimeout()
    {
        var result = SupplierOverviewErrorContract.Classify(Postgres("57014"));

        Assert.Equal("ANALYTICS_TIMEOUT", result.ErrorCode);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void NpgsqlFailure_IsClassifiedAsDatabaseUnavailable()
    {
        var result = SupplierOverviewErrorContract.Classify(new NpgsqlException("connection failed"));

        Assert.Equal("ANALYTICS_DB_UNAVAILABLE", result.ErrorCode);
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain("connection failed", result.Detail);
    }

    [Fact]
    public void UnexpectedFailure_IsSafeAndTraceableByCaller()
    {
        var result = SupplierOverviewErrorContract.Classify(new InvalidOperationException("secret implementation detail"));

        Assert.Equal("SUPPLIER_OVERVIEW_UNEXPECTED_ERROR", result.ErrorCode);
        Assert.Equal(500, result.StatusCode);
        Assert.DoesNotContain("secret implementation detail", result.Detail);
    }

    private static PostgresException Postgres(string sqlState) =>
        new("database failure", "ERROR", "ERROR", sqlState);
}
