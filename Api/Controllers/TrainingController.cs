using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.Validators;
using FluentValidation;
using Infrastructure.DbContexts;
using Infrastructure.OpenProductTraining.V2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Controllers;

[ApiController]
[Route("api/training")]
[Produces("application/json")]
public sealed class TrainingController : ControllerBase
{
    private readonly OpenProductTrainingDbContext _db;
    private readonly ILogger<TrainingController> _logger;
    private readonly IValidator<StartTrainingRunRequestDto> _startValidator;
    private readonly IValidator<RecomputeSellProbabilityLabelsRequestDto> _labelsValidator;

    public TrainingController(
        OpenProductTrainingDbContext db,
        ILogger<TrainingController> logger,
        IValidator<StartTrainingRunRequestDto> startValidator,
        IValidator<RecomputeSellProbabilityLabelsRequestDto> labelsValidator)
    {
        _db = db;
        _logger = logger;
        _startValidator = startValidator;
        _labelsValidator = labelsValidator;
    }

    // Centralized validation method
    private async Task<IActionResult?> ValidateRequest<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Validation failed: {Errors}", validation.Errors);
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }
        return null;
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(TrainingRun), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartRun(
        [FromBody] StartTrainingRunRequestDto request,
        CancellationToken ct = default)
    {
        var validationResult = await ValidateRequest(_startValidator, request, ct);
        if (validationResult != null) return validationResult;

        int? datasetId = request.DatasetId;
        if (datasetId is null && !string.IsNullOrWhiteSpace(request.DatasetName))
        {
            var datasetName = request.DatasetName.Trim();
            datasetId = await _db.Datasets
                .AsNoTracking()
                .Where(d => d.Name.ToLower() == datasetName.ToLower())
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync(ct);
        }

        var now = DateTime.UtcNow;
        var run = new TrainingRun
        {
            ModelType = request.ModelType.Trim(),
            DatasetId = datasetId,
            FeatureViewName = request.FeatureViewName.Trim(),
            Status = "queued",
            CodeVersion = request.CodeVersion?.Trim(),
            ParamsJson = request.Params?.ValueKind is not null ? request.Params.Value.GetRawText() : null,
            Notes = request.Notes,
            CreatedAt = now
        };

        _db.TrainingRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Queued training run {RunId} for model_type={ModelType}", run.Id, run.ModelType);
        return Ok(run);
    }

    // Optimized SQL queries for better performance and maintainability

    const string recomputeLabelsSql = """
        WITH fs AS (
            SELECT
                product_id,
                dataset_name,
                supply_demand_ratio_30d,
                sell_through_velocity_30d
            FROM vw_feature_store
            WHERE local_product_id IS NOT NULL
              AND (@datasetNames IS NULL OR dataset_name = ANY(@datasetNames))
        ),
        scored AS (
            SELECT
                product_id,
                LEAST(1, GREATEST(0,
                    CASE
                        WHEN supply_demand_ratio_30d IS NOT NULL
                            THEN (supply_demand_ratio_30d / NULLIF(supply_demand_ratio_30d + 1, 0))
                        WHEN sell_through_velocity_30d IS NOT NULL
                            THEN (sell_through_velocity_30d / NULLIF(sell_through_velocity_30d + 1, 0))
                        ELSE 0
                    END
                ))::NUMERIC(10,6) AS label_value
            FROM fs
        )
        INSERT INTO training_label_sell_probability_rs (
            product_id,
            horizon_days,
            label_value,
            label_version,
            as_of_date,
            computed_at,
            source,
            notes
        )
        SELECT
            s.product_id,
            @horizonDays,
            s.label_value,
            @labelVersion,
            @asOfDate,
            NOW(),
            'rs_proxy',
            'computed from vw_feature_store rs_metrics'
        FROM scored s
        ON CONFLICT (product_id, horizon_days, label_version) DO UPDATE
        SET
            label_value  = EXCLUDED.label_value,
            as_of_date   = EXCLUDED.as_of_date,
            computed_at  = EXCLUDED.computed_at,
            source       = EXCLUDED.source,
            notes        = EXCLUDED.notes;
        """;

    const string exportSql = """
        SELECT product_id, dataset_name, dataset_split, sell_probability_rs_label
        FROM vw_product_training_export
        WHERE (@datasetName IS NULL OR dataset_name = @datasetName)
          AND (@split IS NULL OR dataset_split = @split)
          AND (@requireLabel = FALSE OR sell_probability_rs_label IS NOT NULL)
        LIMIT @take;
        """;

    [HttpPost("recompute-labels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecomputeLabels(
        [FromBody] RecomputeSellProbabilityLabelsRequestDto request,
        CancellationToken ct = default)
    {
        var validationResult = await ValidateRequest(_labelsValidator, request, ct);
        if (validationResult != null) return validationResult;

        var connectionString = _db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("Database connection string is missing.");
            return Problem(title: "Missing database connection", detail: "OpenProductTraining connection string is missing.", statusCode: 500);
        }

        var datasetNames = request.DatasetNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(recomputeLabelsSql, connection);
        cmd.Parameters.AddWithValue("datasetNames", (object?)datasetNames ?? DBNull.Value);
        cmd.Parameters.AddWithValue("horizonDays", request.HorizonDays);
        cmd.Parameters.AddWithValue("labelVersion", request.LabelVersion.Trim());
        cmd.Parameters.AddWithValue("asOfDate", (object?)request.AsOfDate ?? DBNull.Value);

        var affected = await cmd.ExecuteNonQueryAsync(ct);

        _logger.LogInformation("Recomputed labels for datasets: {DatasetNames}", (object?)(datasetNames ?? Array.Empty<string>()));

        return Ok(new
        {
            affectedRows = affected,
            datasetNames = datasetNames,
            horizonDays = request.HorizonDays,
            labelVersion = request.LabelVersion,
            asOfDate = request.AsOfDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }

    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(
        [FromQuery] string? datasetName = null,
        [FromQuery] string? split = null,
        [FromQuery] bool requireLabel = true,
        [FromQuery] int take = 5000,
        CancellationToken ct = default)
    {
        var connectionString = _db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("Database connection string is missing.");
            return Problem(title: "Missing database connection", detail: "OpenProductTraining connection string is missing.", statusCode: 500);
        }

        take = Math.Clamp(take, 1, 50_000);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(exportSql, connection);
        cmd.Parameters.AddWithValue("datasetName", (object?)datasetName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("split", (object?)split ?? DBNull.Value);
        cmd.Parameters.AddWithValue("requireLabel", requireLabel);
        cmd.Parameters.AddWithValue("take", take);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var sb = new StringBuilder(capacity: 1024 * 1024);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(CsvEscape(reader.GetName(i)));
        }
        sb.AppendLine();

        while (await reader.ReadAsync(ct))
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(CsvEscape(reader.GetValue(i)));
            }
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"open_training_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        _logger.LogInformation("Exported training data to file: {FileName}", fileName);
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    // Added CsvEscape method to resolve the error

    private static string CsvEscape(object? value)
    {
        if (value is null || value is DBNull) return string.Empty;
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (s.Contains('"')) s = s.Replace("\"", "\"\"");
        if (s.Contains(',') || s.Contains('\n') || s.Contains('\r')) s = $"\"{s}\"";
        return s;
    }
}

public sealed record StartTrainingRunRequestDto(
    string ModelType = "sell_probability_rs",
    string FeatureViewName = "vw_product_training_export",
    int? DatasetId = null,
    string? DatasetName = null,
    string? CodeVersion = null,
    JsonElement? Params = null,
    string? Notes = null);

public sealed record RecomputeSellProbabilityLabelsRequestDto(
    string[]? DatasetNames = null,
    int HorizonDays = 30,
    string LabelVersion = "v1",
    DateOnly? AsOfDate = null);
