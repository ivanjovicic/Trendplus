using Application.Common.Interfaces;
using Application.Artikli.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services;

/// <summary>
/// Mock implementation of embedding service
/// TODO: Replace with actual Python/FastAPI service or ML.NET implementation
/// </summary>
public class MockEmbeddingService : IEmbeddingService
{
    private readonly ITrendplusDbContext _db;
    private readonly ILogger<MockEmbeddingService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly Random _random = new();

    public MockEmbeddingService(
        ITrendplusDbContext db,
        ILogger<MockEmbeddingService> logger,
        IHostEnvironment environment)
    {
        _db = db;
        _logger = logger;
        _environment = environment;
    }

    public Task<float[]> GetEmbeddingAsync(string imagePath, CancellationToken ct = default)
    {
        if (_environment.IsProduction())
        {
            throw new InvalidOperationException("Mock embedding service is not allowed in production.");
        }

        _logger.LogWarning("Using MOCK embedding service. Replace with actual implementation!");

        // Generate a random 512-dimensional vector
        // In production, this should call a Python service running CLIP or similar model
        var embedding = new float[512];
        for (int i = 0; i < 512; i++)
        {
            embedding[i] = (float)(_random.NextDouble() * 2 - 1); // Random values between -1 and 1
        }

        return Task.FromResult(embedding);
    }

    public async Task<List<SimilarProduct>> FindSimilarProductsAsync(
        float[] embedding,
        float threshold = 0.8f,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Searching for similar products with threshold {Threshold}", threshold);

        // Use raw SQL with pgvector operators
        var sql = @"
            SELECT 
                pi.""ProductId"",
                a.""Naziv"" AS ProductName,
                pi.""FileName"" AS ImageFileName,
                1 - (pi.""Embedding"" <=> @embedding) AS Similarity
            FROM ""ProductImages"" pi
            JOIN ""Artikli"" a ON pi.""ProductId"" = a.""Id""
            WHERE pi.""Embedding"" IS NOT NULL
                AND 1 - (pi.""Embedding"" <=> @embedding) > @threshold
            ORDER BY pi.""Embedding"" <=> @embedding
            LIMIT @limit";

        var connection = _db.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Convert float[] to pgvector format
        var embeddingStr = "[" + string.Join(",", embedding) + "]";
        command.Parameters.Add(new NpgsqlParameter("embedding", NpgsqlDbType.Unknown)
        {
            Value = embeddingStr
        });
        command.Parameters.Add(new NpgsqlParameter("threshold", threshold));
        command.Parameters.Add(new NpgsqlParameter("limit", limit));

        var results = new List<SimilarProduct>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SimilarProduct(
                ProductId: reader.GetInt32(0),
                ProductName: reader.GetString(1),
                ImageFileName: reader.GetString(2),
                Similarity: reader.GetFloat(3)
            ));
        }

        _logger.LogInformation("Found {Count} similar products", results.Count);

        return results;
    }
}


/// <summary>
/// Production-ready embedding service that calls a Python FastAPI service
/// </summary>
public class PythonEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ITrendplusDbContext _db;
    private readonly ILogger<PythonEmbeddingService> _logger;

    public PythonEmbeddingService(
        HttpClient httpClient,
        ITrendplusDbContext db,
        ILogger<PythonEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _logger = logger;
    }

    public async Task<float[]> GetEmbeddingAsync(string imagePath, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Requesting embedding from Python service for {ImagePath}", imagePath);

            // Read image file
            var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);

            // Send to Python service
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetImageContentType(imagePath));
            content.Add(fileContent, "file", Path.GetFileName(imagePath));

            var response = await _httpClient.PostAsync("/embed", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Invalid embedding response from Python service");
            }

            _logger.LogInformation("Successfully received embedding with {Dimensions} dimensions", result.Embedding.Length);

            return result.Embedding;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout calling Python embedding service");
            throw new TimeoutException("Embedding service request timed out.", ex);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Embedding request was canceled by the caller");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Python embedding service");
            throw new InvalidOperationException("Failed to connect to embedding service. Ensure Python service is running at configured URL.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embedding from Python service");
            throw;
        }
    }

    private static string GetImageContentType(string imagePath)
    {
        return Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }

    public async Task<List<SimilarProduct>> FindSimilarProductsAsync(
        float[] embedding,
        float threshold = 0.8f,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Searching for similar products with threshold {Threshold}", threshold);

        // Use raw SQL with pgvector operators
        var sql = @"
            SELECT 
                pi.""ProductId"",
                a.""Naziv"" AS ProductName,
                pi.""FileName"" AS ImageFileName,
                1 - (pi.""Embedding"" <=> @embedding) AS Similarity
            FROM ""ProductImages"" pi
            JOIN ""Artikli"" a ON pi.""ProductId"" = a.""Id""
            WHERE pi.""Embedding"" IS NOT NULL
                AND 1 - (pi.""Embedding"" <=> @embedding) > @threshold
            ORDER BY pi.""Embedding"" <=> @embedding
            LIMIT @limit";

        var connection = _db.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Convert float[] to pgvector format
        var embeddingStr = "[" + string.Join(",", embedding) + "]";
        command.Parameters.Add(new NpgsqlParameter("embedding", NpgsqlDbType.Unknown)
        {
            Value = embeddingStr
        });
        command.Parameters.Add(new NpgsqlParameter("threshold", threshold));
        command.Parameters.Add(new NpgsqlParameter("limit", limit));

        var results = new List<SimilarProduct>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SimilarProduct(
                ProductId: reader.GetInt32(0),
                ProductName: reader.GetString(1),
                ImageFileName: reader.GetString(2),
                Similarity: reader.GetFloat(3)
            ));
        }

        _logger.LogInformation("Found {Count} similar products", results.Count);

        return results;
    }

    private record EmbeddingResponse(float[] Embedding);
}

/// <summary>
/// Quarantined embedding path: API can start, but similarity calls fail closed
/// without returning random mock vectors.
/// </summary>
public sealed class DisabledEmbeddingService : IEmbeddingService
{
    private readonly ILogger<DisabledEmbeddingService> _logger;

    public DisabledEmbeddingService(ILogger<DisabledEmbeddingService> logger)
    {
        _logger = logger;
    }

    public Task<float[]> GetEmbeddingAsync(string imagePath, CancellationToken ct = default)
    {
        _logger.LogWarning("Embedding service is disabled; rejecting embedding request for {ImagePath}", imagePath);
        throw new InvalidOperationException(
            "Image embedding is disabled in this environment. Configure a private EmbeddingService:BaseUrl to enable it.");
    }

    public Task<List<SimilarProduct>> FindSimilarProductsAsync(
        float[] embedding,
        float threshold = 0.8f,
        int limit = 10,
        CancellationToken ct = default)
    {
        _logger.LogWarning("Embedding service is disabled; rejecting similarity search");
        throw new InvalidOperationException(
            "Image embedding is disabled in this environment. Configure a private EmbeddingService:BaseUrl to enable it.");
    }
}
