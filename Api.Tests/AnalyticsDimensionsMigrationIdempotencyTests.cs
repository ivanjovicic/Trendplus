using System.Linq;
using Infrastructure.Migrations.AnalyticsDb;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Api.Tests;

// Regression for 42701 "column PLU of relation ProductsDim already exists":
// databases bootstrapped before this migration was discoverable already carry its
// schema (created by DatabaseInitializer self-heal) without a history row, so every
// statement must be safe to run against an existing schema.
public sealed class AnalyticsDimensionsMigrationIdempotencyTests
{
    [Fact]
    public void Up_UsesOnlyIdempotentSql()
    {
        var migration = new AddAnalyticsDimensionsAndMovements();
        migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

        var operations = migration.UpOperations;

        Assert.NotEmpty(operations);
        Assert.All(operations, operation => Assert.IsType<SqlOperation>(operation));

        var statements = operations
            .Cast<SqlOperation>()
            .SelectMany(operation => operation.Sql.Split(';'))
            .Select(statement => statement.Trim())
            .Where(statement => statement.Length > 0)
            .ToArray();

        Assert.All(statements, statement => Assert.Contains("IF NOT EXISTS", statement));
        Assert.Contains(statements, statement => statement.Contains("\"ProductsDim\" ADD COLUMN IF NOT EXISTS \"PLU\" character varying(100)"));
        Assert.Contains(statements, statement => statement.Contains("CREATE TABLE IF NOT EXISTS \"InventoryMovementFacts\""));
    }
}
