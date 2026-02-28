using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Workers;

public sealed class OpenTrainingModelTrainingWorker : BackgroundService
{
    private const string WorkerName = "OpenTrainingModelTrainingWorker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpenTrainingModelTrainingWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly OpenTrainingModelTrainingOptions _options;

    public OpenTrainingModelTrainingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OpenTrainingModelTrainingWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        IOptions<OpenTrainingModelTrainingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🧠 {WorkerName} starting...", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting up...");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _healthService.ReportStopped(WorkerName, "Cancelled during startup delay");
            return;
        }

        var paused = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled || !_options.Enabled)
            {
                if (!paused)
                {
                    var reason = !_controlService.IsEnabled
                        ? "Paused - global workers switch OFF."
                        : "Paused - OpenTrainingModelTraining disabled in configuration.";
                    _logger.LogInformation("{WorkerName} paused. Reason: {Reason}", WorkerName, reason);
                    _healthService.ReportStopped(WorkerName, reason);
                    paused = true;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PauseCheckSeconds)), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            if (paused)
            {
                _logger.LogInformation("{WorkerName} resumed.", WorkerName);
                _healthService.ReportRunning(WorkerName, "Resumed after workers switch ON.");
                paused = false;
            }

            try
            {
                var didWork = await TryProcessOneAsync(stoppingToken);
                if (!didWork)
                {
                    _healthService.ReportHealthy(WorkerName, "Idle (no queued training runs).");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds)), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ {WorkerName} error", WorkerName);
                _healthService.ReportError(WorkerName, ex);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, _options.PollSeconds)), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
        _logger.LogInformation("🧠 {WorkerName} stopped", WorkerName);
    }

    private async Task<bool> TryProcessOneAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenProductTrainingDbContext>();
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Open training connection string missing; training worker will stay idle.");
            return false;
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        (long RunId, string ModelType, int? DatasetId, string FeatureViewName)? job = null;

        // Lock + fetch one queued run
        await using (var tx = await conn.BeginTransactionAsync(ct))
        {
            const string pickSql = """
                SELECT id, model_type, dataset_id, feature_view_name
                FROM training_run
                WHERE status = 'queued'
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1;
                """;

            await using (var cmd = new NpgsqlCommand(pickSql, conn, tx))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                if (await r.ReadAsync(ct))
                {
                    job = (
                        RunId: r.GetInt64(0),
                        ModelType: r.GetString(1),
                        DatasetId: r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                        FeatureViewName: r.IsDBNull(3) ? "vw_product_training_export" : r.GetString(3)
                    );
                }
            }

            if (job is null)
            {
                await tx.CommitAsync(ct);
                return false;
            }

            const string markRunningSql = """
                UPDATE training_run
                SET status = 'running', started_at = NOW(), error_message = NULL
                WHERE id = @id;
                """;

            await using (var u = new NpgsqlCommand(markRunningSql, conn, tx))
            {
                u.Parameters.AddWithValue("id", job.Value.RunId);
                await u.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }

        _healthService.ReportRunning(WorkerName, $"Training run #{job.Value.RunId} ({job.Value.ModelType})...");
        _logger.LogInformation("🧠 Starting training run {RunId} model_type={ModelType}", job.Value.RunId, job.Value.ModelType);

        var datasetName = job.Value.DatasetId.HasValue
            ? await ResolveDatasetNameAsync(conn, job.Value.DatasetId.Value, ct)
            : null;

        var scriptPath = ResolveScriptPath(_options.TrainingScriptPath);
        var outputDir = Path.GetFullPath(_options.OutputDir);
        Directory.CreateDirectory(outputDir);

        try
        {
            var pythonResult = await RunPythonTrainingAsync(
                pythonExe: _options.PythonExe,
                scriptPath: scriptPath,
                featureViewName: job.Value.FeatureViewName,
                datasetName: datasetName,
                connectionString: connectionString,
                outputDir: outputDir,
                take: Math.Max(1, _options.Take),
                ct: ct);

            await PersistModelVersionAsync(
                conn,
                job.Value.RunId,
                job.Value.ModelType,
                pythonResult,
                activate: _options.ActivateOnSuccess,
                ct: ct);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(conn, job.Value.RunId, ex, ct);
            _logger.LogError(ex, "❌ Training run {RunId} failed", job.Value.RunId);
            _healthService.ReportError(WorkerName, ex);
            return true;
        }

        _healthService.ReportHealthy(WorkerName, $"Completed training run #{job.Value.RunId}.");
        _logger.LogInformation("🧠 Training run {RunId} completed", job.Value.RunId);
        return true;
    }

    private static async Task<string?> ResolveDatasetNameAsync(NpgsqlConnection conn, int datasetId, CancellationToken ct)
    {
        const string sql = """SELECT name FROM dataset WHERE id = @id LIMIT 1;""";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", datasetId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is DBNull or null ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static string ResolveScriptPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        // Prefer resolving from current working directory (dev) and then from app base dir (publish).
        var fromCwd = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        if (File.Exists(fromCwd))
            return fromCwd;

        var fromBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
        return fromBase;
    }

    private sealed record PythonTrainResult(
        string ModelOnnxPath,
        string ModelOnnxSha256,
        string FeatureSchemaJson,
        string MetricsJson,
        string CalibrationJson,
        string FeatureImportanceJson,
        string? ShapSummaryJson,
        string MinFeatureValuesJson,
        string MaxFeatureValuesJson);

    private static async Task<PythonTrainResult> RunPythonTrainingAsync(
        string pythonExe,
        string scriptPath,
        string featureViewName,
        string? datasetName,
        string connectionString,
        string outputDir,
        int take,
        CancellationToken ct)
    {
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Training script not found: {scriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("--ado-net-connection-string");
        psi.ArgumentList.Add(connectionString);
        psi.ArgumentList.Add("--output-dir");
        psi.ArgumentList.Add(outputDir);
        psi.ArgumentList.Add("--feature-view");
        psi.ArgumentList.Add(string.IsNullOrWhiteSpace(featureViewName) ? "vw_product_training_export" : featureViewName);
        psi.ArgumentList.Add("--take");
        psi.ArgumentList.Add(take.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(datasetName))
        {
            psi.ArgumentList.Add("--dataset-name");
            psi.ArgumentList.Add(datasetName);
        }

        using var process = Process.Start(psi);
        if (process is null)
            throw new InvalidOperationException("Failed to start Python process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Python training failed (exit={process.ExitCode}). stderr={Truncate(stderr, 4000)} stdout={Truncate(stdout, 2000)}");
        }

        var payload = ExtractJsonPayload(stdout);
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException($"Python training returned invalid payload: {Truncate(payload, 4000)}");

        var artifacts = root.GetProperty("artifacts");

        string ReadText(string path) => File.ReadAllText(path, Encoding.UTF8);

        var onnxPath = artifacts.GetProperty("model_onnx_path").GetString() ?? throw new InvalidOperationException("Missing model_onnx_path.");
        var featureSchemaPath = artifacts.GetProperty("feature_schema_path").GetString() ?? throw new InvalidOperationException("Missing feature_schema_path.");
        var metricsPath = artifacts.GetProperty("metrics_path").GetString() ?? throw new InvalidOperationException("Missing metrics_path.");
        var calibrationPath = artifacts.GetProperty("calibration_path").GetString() ?? throw new InvalidOperationException("Missing calibration_path.");
        var fiPath = artifacts.GetProperty("feature_importance_path").GetString() ?? throw new InvalidOperationException("Missing feature_importance_path.");
        var minPath = artifacts.GetProperty("min_feature_values_path").GetString() ?? throw new InvalidOperationException("Missing min_feature_values_path.");
        var maxPath = artifacts.GetProperty("max_feature_values_path").GetString() ?? throw new InvalidOperationException("Missing max_feature_values_path.");

        var shapPath = artifacts.TryGetProperty("shap_summary_path", out var sp) && sp.ValueKind == JsonValueKind.String
            ? sp.GetString()
            : null;

        return new PythonTrainResult(
            ModelOnnxPath: Path.GetFullPath(onnxPath),
            ModelOnnxSha256: artifacts.GetProperty("model_onnx_sha256").GetString() ?? "",
            FeatureSchemaJson: ReadText(featureSchemaPath),
            MetricsJson: ReadText(metricsPath),
            CalibrationJson: ReadText(calibrationPath),
            FeatureImportanceJson: ReadText(fiPath),
            ShapSummaryJson: !string.IsNullOrWhiteSpace(shapPath) && File.Exists(shapPath) ? ReadText(shapPath) : null,
            MinFeatureValuesJson: ReadText(minPath),
            MaxFeatureValuesJson: ReadText(maxPath)
        );
    }

    private static async Task PersistModelVersionAsync(
        NpgsqlConnection conn,
        long runId,
        string modelType,
        PythonTrainResult result,
        bool activate,
        CancellationToken ct)
    {
        var mt = string.IsNullOrWhiteSpace(modelType) ? "sell_probability_rs" : modelType.Trim();

        await using var tx = await conn.BeginTransactionAsync(ct);

        // Determine next version
        const string nextSql = """SELECT COALESCE(MAX(version), 0) + 1 FROM model_version WHERE model_type = @mt;""";
        int nextVersion;
        await using (var cmd = new NpgsqlCommand(nextSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("mt", mt);
            nextVersion = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        }

        if (activate)
        {
            const string deactivateSql = """UPDATE model_version SET is_active = FALSE WHERE model_type = @mt AND is_active = TRUE;""";
            await using var deact = new NpgsqlCommand(deactivateSql, conn, tx);
            deact.Parameters.AddWithValue("mt", mt);
            await deact.ExecuteNonQueryAsync(ct);
        }

        const string insertSql = """
            INSERT INTO model_version (
                model_type,
                version,
                training_run_id,
                is_active,
                onnx_path,
                onnx_sha256,
                feature_schema_json,
                metrics_json,
                calibration_json,
                shap_summary_json,
                feature_importance_json,
                min_feature_values,
                max_feature_values,
                notes
            )
            VALUES (
                @modelType,
                @version,
                @runId,
                @isActive,
                @onnxPath,
                @onnxSha,
                @featureSchema::jsonb,
                @metrics::jsonb,
                @calibration::jsonb,
                @shap::jsonb,
                @fi::jsonb,
                @minv::jsonb,
                @maxv::jsonb,
                @notes
            )
            RETURNING id;
            """;

        long modelId;
        await using (var ins = new NpgsqlCommand(insertSql, conn, tx))
        {
            ins.Parameters.AddWithValue("modelType", mt);
            ins.Parameters.AddWithValue("version", nextVersion);
            ins.Parameters.AddWithValue("runId", runId);
            ins.Parameters.AddWithValue("isActive", activate);
            ins.Parameters.AddWithValue("onnxPath", (object?)result.ModelOnnxPath ?? DBNull.Value);
            ins.Parameters.AddWithValue("onnxSha", (object?)result.ModelOnnxSha256 ?? DBNull.Value);
            ins.Parameters.AddWithValue("featureSchema", (object?)result.FeatureSchemaJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("metrics", (object?)result.MetricsJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("calibration", (object?)result.CalibrationJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("shap", (object?)result.ShapSummaryJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("fi", (object?)result.FeatureImportanceJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("minv", (object?)result.MinFeatureValuesJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("maxv", (object?)result.MaxFeatureValuesJson ?? DBNull.Value);
            ins.Parameters.AddWithValue("notes", $"auto-trained by {WorkerName} @ {DateTime.UtcNow:O}");

            modelId = Convert.ToInt64(await ins.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        }

        const string markOkSql = """
            UPDATE training_run
            SET status = 'succeeded',
                completed_at = NOW(),
                metrics_json = @metrics::jsonb,
                artifact_uri = @artifactUri,
                error_message = NULL
            WHERE id = @id;
            """;

        await using (var ok = new NpgsqlCommand(markOkSql, conn, tx))
        {
            ok.Parameters.AddWithValue("metrics", (object?)result.MetricsJson ?? DBNull.Value);
            ok.Parameters.AddWithValue("artifactUri", (object?)Path.GetDirectoryName(result.ModelOnnxPath) ?? DBNull.Value);
            ok.Parameters.AddWithValue("id", runId);
            await ok.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task MarkFailedAsync(NpgsqlConnection conn, long runId, Exception ex, CancellationToken ct)
    {
        const string sql = """
            UPDATE training_run
            SET status = 'failed',
                completed_at = NOW(),
                error_message = @err
            WHERE id = @id;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("err", Truncate($"{ex.GetType().Name}: {ex.Message}", 4000));
        cmd.Parameters.AddWithValue("id", runId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string ExtractJsonPayload(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException("Python training produced empty stdout.");

        // best-effort: take the last non-empty line that looks like JSON
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("{", StringComparison.Ordinal) && line.EndsWith("}", StringComparison.Ordinal))
                return line;
        }

        // fallback to last '{' block
        var idx = stdout.LastIndexOf('{');
        if (idx >= 0)
            return stdout[idx..].Trim();

        throw new InvalidOperationException($"Unable to extract JSON payload from stdout: {Truncate(stdout, 2000)}");
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Length <= max) return s;
        return s.Substring(0, max) + "...";
    }
}
