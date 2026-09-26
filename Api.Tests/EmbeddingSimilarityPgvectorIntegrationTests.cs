using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class EmbeddingSimilarityPgvectorIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public EmbeddingSimilarityPgvectorIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindSimilarProducts_BindsTypedPgvectorParameterAndReturnsNearestRow()
    {
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_embedding_similarity_{Guid.NewGuid():N}");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        await using var db = new TrendplusDbContext(options);
        await db.Database.OpenConnectionAsync();

        var connection = Assert.IsType<NpgsqlConnection>(db.Database.GetDbConnection());
        await ExecuteAsync(
            connection,
            """
            CREATE EXTENSION IF NOT EXISTS vector;
            CREATE TEMP TABLE "Artikli" ("Id" integer PRIMARY KEY, "Naziv" text NOT NULL);
            CREATE TEMP TABLE "ProductImages" (
                "ProductId" integer NOT NULL,
                "FileName" text NOT NULL,
                "Embedding" vector(3) NOT NULL
            );
            INSERT INTO "Artikli" ("Id", "Naziv") VALUES (101, 'Closest'), (102, 'Different');
            INSERT INTO "ProductImages" ("ProductId", "FileName", "Embedding") VALUES
                (101, 'closest.png', '[1,0,0]'::vector),
                (102, 'different.png', '[0,1,0]'::vector);
            """);
        connection.ReloadTypes();

        var service = new MockEmbeddingService(
            db,
            NullLogger<MockEmbeddingService>.Instance,
            new DevelopmentHostEnvironment());

        var results = await service.FindSimilarProductsAsync(
            [1f, 0f, 0f],
            threshold: 0.8f,
            limit: 10);

        var result = Assert.Single(results);
        Assert.Equal(101, result.ProductId);
        Assert.Equal("Closest", result.ProductName);
        Assert.Equal("closest.png", result.ImageFileName);
        Assert.InRange(result.Similarity, 0.999f, 1.001f);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(EmbeddingSimilarityPgvectorIntegrationTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
