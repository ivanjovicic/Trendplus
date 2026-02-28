using System.Text.Json;
using Api.Models;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/scoring")]
[EnableRateLimiting("api-v1")]
[Produces("application/json")]
public sealed class ScoringV2Controller : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly IRuntimeScoringEngine _runtimeScoringEngine;
    private readonly OpenProductTrainingDbContext _openTrainingDb;
    private readonly ILogger<ScoringV2Controller> _logger;

    public ScoringV2Controller(
        IRuntimeScoringEngine runtimeScoringEngine,
        OpenProductTrainingDbContext openTrainingDb,
        ILogger<ScoringV2Controller> logger)
    {
        _runtimeScoringEngine = runtimeScoringEngine;
        _openTrainingDb = openTrainingDb;
        _logger = logger;
    }

    [HttpPost("evaluate")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(RuntimeScoringEvaluateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Evaluate(
        [FromForm] RuntimeScoringEvaluateRequest request,
        CancellationToken ct = default)
    {
        if (request.Image is null || request.Image.Length == 0)
            return BadRequest(new { error = "Image is required." });

        var extension = Path.GetExtension(request.Image.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return BadRequest(new { error = "Invalid image type. Allowed: jpg, jpeg, png, webp." });

        if (request.Image.Length > 12 * 1024 * 1024)
            return BadRequest(new { error = "Image is too large. Max size is 12MB." });

        var tempDir = Path.Combine(Path.GetTempPath(), "trendplus-runtime-scoring");
        Directory.CreateDirectory(tempDir);

        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await request.Image.CopyToAsync(fs, ct);
            }

            var input = new RuntimeScoringEngineInput(
                ImagePath: tempPath,
                Cost: request.Cost,
                TargetPrice: request.TargetPrice,
                Brand: request.Brand,
                Category: request.Category,
                Market: request.Market,
                ArtikalId: request.ArtikalId,
                DobavljacId: request.DobavljacId,
                TipObuceId: request.TipObuceId,
                SezonaId: request.SezonaId,
                Velicina: request.Velicina,
                Boja: request.Boja,
                Materijal: request.Materijal);

            var response = await _runtimeScoringEngine.EvaluateAsync(input, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime scoring v2 evaluate failed.");
            return Problem(
                title: "Runtime scoring failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp runtime scoring image.");
            }
        }
    }

    [HttpPost("debug")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(RuntimeScoringEvaluateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Debug(
        [FromBody] RuntimeScoringDebugRequestDto request,
        CancellationToken ct = default)
    {
        try
        {
            var input = new RuntimeScoringEngineInput(
                ImagePath: null,
                Cost: request.Cost,
                TargetPrice: request.TargetPrice,
                Brand: request.Brand,
                Category: request.Category,
                Market: request.Market,
                ArtikalId: request.ArtikalId,
                DobavljacId: request.DobavljacId,
                TipObuceId: request.TipObuceId,
                SezonaId: request.SezonaId,
                Velicina: request.Velicina,
                Boja: request.Boja,
                Materijal: request.Materijal);

            var response = await _runtimeScoringEngine.EvaluateAsync(input, ct);
            response.SimilarProducts = []; // debug path doesn't include image similarity
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime scoring v2 debug failed.");
            return Problem(title: "Scoring debug failed", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpGet("model-info")]
    public async Task<IActionResult> GetModelInfo(
        [FromQuery] string modelType = "sell_probability_rs",
        CancellationToken ct = default)
    {
        var mt = string.IsNullOrWhiteSpace(modelType) ? "sell_probability_rs" : modelType.Trim();

        var model = await _openTrainingDb.ModelVersions
            .AsNoTracking()
            .Where(m => m.ModelType == mt && m.IsActive)
            .OrderByDescending(m => m.Version)
            .FirstOrDefaultAsync(ct);

        if (model is null)
            return NotFound(new { message = $"No active model for modelType='{mt}'." });

        return Ok(new
        {
            model.ModelType,
            model.Version,
            model.IsActive,
            model.CreatedAt,
            model.OnnxPath,
            model.OnnxSha256,
            featureSchema = TryParseJson(model.FeatureSchemaJson),
            metrics = TryParseJson(model.MetricsJson),
            calibration = TryParseJson(model.CalibrationJson),
            featureImportance = TryParseJson(model.FeatureImportanceJson)
        });
    }

    [HttpGet("feature-importance")]
    public async Task<IActionResult> GetFeatureImportance(
        [FromQuery] string modelType = "sell_probability_rs",
        CancellationToken ct = default)
    {
        var mt = string.IsNullOrWhiteSpace(modelType) ? "sell_probability_rs" : modelType.Trim();

        var model = await _openTrainingDb.ModelVersions
            .AsNoTracking()
            .Where(m => m.ModelType == mt && m.IsActive)
            .OrderByDescending(m => m.Version)
            .FirstOrDefaultAsync(ct);

        if (model is null)
            return NotFound(new { message = $"No active model for modelType='{mt}'." });

        if (string.IsNullOrWhiteSpace(model.FeatureImportanceJson))
            return NotFound(new { message = "Active model has no feature importance payload." });

        return Ok(TryParseJson(model.FeatureImportanceJson));
    }

    private static object? TryParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return JsonSerializer.Deserialize<object>(raw);
        }
        catch
        {
            return new { raw };
        }
    }
}

public sealed record RuntimeScoringDebugRequestDto(
    decimal? Cost,
    decimal? TargetPrice,
    string? Brand,
    string? Category,
    string? Market,
    int? ArtikalId = null,
    int? DobavljacId = null,
    int? TipObuceId = null,
    int? SezonaId = null,
    string? Velicina = null,
    string? Boja = null,
    string? Materijal = null);
