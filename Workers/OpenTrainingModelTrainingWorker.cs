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
    private static readonly string[] SupplierDecisionMaterializedViews =
    [
        "mv_supplier_decision_score_cache",
        "mv_supplier_recommendations_cache"
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OpenTrainingModelTrainingWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;
    private readonly OpenTrainingModelTrainingOptions _options;

    public OpenTrainingModelTrainingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OpenTrainingModelTrainingWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService,
        IOptions<OpenTrainingModelTrainingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
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

            var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
            var manualRunRequested = false;
            if (!policy.CanRunNow)
            {
                if (!paused)
                {
                    var reason = policy.PauseReason ?? "Paused - worker policy disabled execution.";
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

            if (policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
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
                    var delay = manualRunRequested
                        ? TimeSpan.FromSeconds(Math.Max(1, _options.PauseCheckSeconds))
                        : TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds));
                    await Task.Delay(delay, stoppingToken);
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
        var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("Open training connection string missing; training worker will stay idle.");
            return false;
        }

        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        if (csb.Timeout <= 0)
            csb.Timeout = 15;

        _logger.LogInformation("{Worker} connecting to Postgres: {@Conn}", WorkerName, new
        {
            Host = csb.Host,
            Port = csb.Port,
            Database = csb.Database,
            Username = csb.Username,
            Pooling = csb.Pooling,
            MaxPool = csb.MaxPoolSize,
            TimeoutSeconds = csb.Timeout,
            CommandTimeoutSeconds = csb.CommandTimeout
        });

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        try
        {
            await conn.OpenAsync(ct);
        }
        catch (TimeoutException tex)
        {
            _logger.LogError(tex, "Timed out while opening Postgres connection.");
            throw;
        }
        catch (NpgsqlException npex)
        {
            _logger.LogError(npex, "Failed to open Postgres connection.");
            throw;
        }

        (long RunId, string ModelType, int? DatasetId, string FeatureViewName, string? ParamsJson)? job = null;

        // Lock + fetch one queued run
        await using (var tx = await conn.BeginTransactionAsync(ct))
        {
            const string pickSql = """
                SELECT id, model_type, dataset_id, feature_view_name, params_json
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
                        FeatureViewName: r.IsDBNull(3) ? OpenTrainingModelCatalog.DefaultFeatureViewName : r.GetString(3),
                        ParamsJson: r.IsDBNull(4) ? null : r.GetString(4)
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

        var modelType = NormalizeModelType(job.Value.ModelType);
        var isEnterpriseRun = IsEnterpriseModelType(modelType);
        var isSupplierRankingRun = IsSupplierRankingModelType(modelType);
        var featureView = ResolveFeatureViewName(modelType, job.Value.FeatureViewName);
        var scriptPath = ResolveScriptPath(
            isEnterpriseRun
                ? OpenTrainingModelCatalog.EnterpriseTrainingScriptPath
                : isSupplierRankingRun
                    ? OpenTrainingModelCatalog.SupplierRankingTrainingScriptPath
                    : _options.TrainingScriptPath);
        var runtimeTuningJson = ExtractRuntimeTuningJson(job.Value.ParamsJson);
        if (isEnterpriseRun && string.IsNullOrWhiteSpace(runtimeTuningJson))
            runtimeTuningJson = BuildDefaultRuntimeTuningJson();

        _healthService.ReportRunning(WorkerName, $"Training run #{job.Value.RunId} ({modelType})...");
        _logger.LogInformation("🧠 Starting training run {RunId} model_type={ModelType}", job.Value.RunId, modelType);

        var datasetName = job.Value.DatasetId.HasValue
            ? await ResolveDatasetNameAsync(conn, job.Value.DatasetId.Value, ct)
            : null;

        var outputDir = Path.GetFullPath(_options.OutputDir);
        Directory.CreateDirectory(outputDir);
        long? persistedModelId = null;

        // If training takes longer than the heartbeat threshold (10 min), we want to keep the
        // worker marked as "running" so the health dashboard doesn't flag it as stale.
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        var heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (await heartbeatTimer.WaitForNextTickAsync(heartbeatCts.Token))
                {
                    _healthService.ReportRunning(WorkerName, $"Training run #{job.Value.RunId} still running...");
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
        }, heartbeatCts.Token);

        try
        {
            if (isSupplierRankingRun)
                await RefreshSupplierTrainingDatasetAsync(analyticsDb, ct);

            var pythonResult = isEnterpriseRun
                ? await RunEnterpriseTrainingAsync(
                    pythonExe: _options.PythonExe,
                    scriptPath: scriptPath,
                    featureViewName: featureView,
                    paramsJson: job.Value.ParamsJson,
                    connectionString: connectionString,
                    outputDir: outputDir,
                    take: Math.Max(1, _options.Take),
                    ct: ct)
                : await RunPythonTrainingAsync(
                    pythonExe: _options.PythonExe,
                    scriptPath: scriptPath,
                    featureViewName: featureView,
                    datasetName: datasetName,
                    connectionString: connectionString,
                    outputDir: outputDir,
                    take: Math.Max(1, _options.Take),
                    ct: ct);

            persistedModelId = await PersistModelVersionAsync(
                conn,
                job.Value.RunId,
                modelType,
                pythonResult,
                runtimeTuningJson,
                activate: _options.ActivateOnSuccess,
                ct: ct);

            if (isSupplierRankingRun)
            {
                await PersistSupplierPredictionsAsync(
                    analyticsDb,
                    pythonResult,
                    persistedModelId.Value,
                    ct);
                await RefreshSupplierDecisionCachesAsync(analyticsDb, ct);
            }
        }
        catch (Exception ex)
        {
            if (persistedModelId.HasValue)
                await CleanupFailedModelVersionAsync(conn, persistedModelId.Value, ct);
            await MarkFailedAsync(conn, job.Value.RunId, ex, ct);
            _logger.LogError(ex, "❌ Training run {RunId} failed", job.Value.RunId);
            _healthService.ReportError(WorkerName, ex);
            return true;
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // expected
            }
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

    private static string NormalizeModelType(string? modelType)
        => OpenTrainingModelCatalog.NormalizeModelType(modelType);

    private static bool IsEnterpriseModelType(string modelType)
        => OpenTrainingModelCatalog.IsEnterpriseModelType(modelType);

    private static bool IsSupplierRankingModelType(string modelType)
        => string.Equals(
            NormalizeModelType(modelType),
            OpenTrainingModelCatalog.SupplierRankingModelType,
            StringComparison.OrdinalIgnoreCase);

    private static string ResolveFeatureViewName(string modelType, string? featureViewName)
    {
        var resolved = string.IsNullOrWhiteSpace(featureViewName)
            ? OpenTrainingModelCatalog.DefaultFeatureViewName
            : featureViewName.Trim();

        if (IsSupplierRankingModelType(modelType) &&
            (string.IsNullOrWhiteSpace(featureViewName) ||
             string.Equals(resolved, OpenTrainingModelCatalog.DefaultFeatureViewName, StringComparison.OrdinalIgnoreCase)))
        {
            return OpenTrainingModelCatalog.SupplierRankingFeatureViewName;
        }

        if (IsEnterpriseModelType(modelType) &&
            (string.IsNullOrWhiteSpace(featureViewName) ||
             string.Equals(resolved, OpenTrainingModelCatalog.DefaultFeatureViewName, StringComparison.OrdinalIgnoreCase)))
        {
            return OpenTrainingModelCatalog.EnterpriseDefaultFeatureViewName;
        }

        return resolved;
    }

    private static readonly HashSet<string> RuntimeTuningKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "MarketplaceCoverageItemsPerUnit",
        "MarketplaceCoverageMaxUnits",
        "SourceCoverageNormalizationMaxUnits",
        "PriceFitExponentialDecay",
        "DealTanhMultiplier",
        "ConfidenceBase",
        "ConfidenceTrainingBonus",
        "ConfidencePerSource",
        "ConfidenceSourceCap",
        "ConfidenceImageDivisor",
        "ConfidenceImageCap",
        "ConfidenceBaselineBonus",
        "ConfidenceCap"
    };

    private static string? ExtractRuntimeTuningJson(string? paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (TryGetPropertyCaseInsensitive(root, "runtime_tuning", out var runtimeTuning) &&
                runtimeTuning.ValueKind == JsonValueKind.Object)
            {
                return runtimeTuning.GetRawText();
            }

            if (TryGetPropertyCaseInsensitive(root, "runtimeTuning", out runtimeTuning) &&
                runtimeTuning.ValueKind == JsonValueKind.Object)
            {
                return runtimeTuning.GetRawText();
            }

            if (TryGetPropertyCaseInsensitive(root, "tuning", out var tuningNode) &&
                tuningNode.ValueKind == JsonValueKind.Object)
            {
                var nested = FilterKnownTuningKeys(tuningNode);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }

            return FilterKnownTuningKeys(root);
        }
        catch
        {
            return null;
        }
    }

    private static string? FilterKnownTuningKeys(JsonElement source)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        var count = 0;
        foreach (var prop in source.EnumerateObject())
        {
            if (!RuntimeTuningKeys.Contains(prop.Name))
                continue;

            writer.WritePropertyName(prop.Name);
            prop.Value.WriteTo(writer);
            count++;
        }

        writer.WriteEndObject();
        writer.Flush();
        return count == 0 ? null : Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string BuildDefaultRuntimeTuningJson()
    {
        var defaults = new Dictionary<string, object>
        {
            ["MarketplaceCoverageItemsPerUnit"] = 200,
            ["MarketplaceCoverageMaxUnits"] = 3,
            ["SourceCoverageNormalizationMaxUnits"] = 6,
            ["PriceFitExponentialDecay"] = 3.0,
            ["DealTanhMultiplier"] = 4.0,
            ["ConfidenceBase"] = 20.0,
            ["ConfidenceTrainingBonus"] = 25.0,
            ["ConfidencePerSource"] = 8.0,
            ["ConfidenceSourceCap"] = 30.0,
            ["ConfidenceImageDivisor"] = 5.0,
            ["ConfidenceImageCap"] = 15.0,
            ["ConfidenceBaselineBonus"] = 10.0,
            ["ConfidenceCap"] = 95.0
        };

        return JsonSerializer.Serialize(defaults);
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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
        string MaxFeatureValuesJson,
        string? PredictionsPath);

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
        psi.ArgumentList.Add(string.IsNullOrWhiteSpace(featureViewName) ? OpenTrainingModelCatalog.DefaultFeatureViewName : featureViewName);
        psi.ArgumentList.Add("--take");
        psi.ArgumentList.Add(take.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(datasetName))
        {
            psi.ArgumentList.Add("--dataset-name");
            psi.ArgumentList.Add(datasetName);
        }

        return await ExecutePythonTrainingAsync(psi, ct);
    }

    private static async Task<PythonTrainResult> RunEnterpriseTrainingAsync(
        string pythonExe,
        string scriptPath,
        string featureViewName,
        string? paramsJson,
        string connectionString,
        string outputDir,
        int take,
        CancellationToken ct)
    {
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Training script not found: {scriptPath}");

        if (!TryParseEnterpriseOptions(paramsJson, out var enterpriseOptions))
            enterpriseOptions = new EnterpriseOptions();

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("--output-dir");
        psi.ArgumentList.Add(outputDir);

        if (!string.IsNullOrWhiteSpace(enterpriseOptions.InputCsv))
        {
            psi.ArgumentList.Add("--input-csv");
            psi.ArgumentList.Add(enterpriseOptions.InputCsv);
        }
        else
        {
            psi.ArgumentList.Add("--ado-net-connection-string");
            psi.ArgumentList.Add(connectionString);

            var query = !string.IsNullOrWhiteSpace(enterpriseOptions.SqlQuery)
                ? enterpriseOptions.SqlQuery
                : BuildEnterpriseSqlQuery(
                    !string.IsNullOrWhiteSpace(enterpriseOptions.Table)
                        ? enterpriseOptions.Table
                        : featureViewName,
                    take);

            psi.ArgumentList.Add("--sql-query");
            psi.ArgumentList.Add(query);
        }

        var targetColumn = string.IsNullOrWhiteSpace(enterpriseOptions.TargetColumn)
            ? OpenTrainingModelCatalog.EnterpriseDefaultTargetColumn
            : enterpriseOptions.TargetColumn;

        psi.ArgumentList.Add("--target-column");
        psi.ArgumentList.Add(targetColumn);

        psi.ArgumentList.Add("--feature-columns");
        psi.ArgumentList.Add(
            string.IsNullOrWhiteSpace(enterpriseOptions.FeatureColumnsCsv)
                ? OpenTrainingModelCatalog.EnterpriseDefaultFeatureColumnsCsv
                : enterpriseOptions.FeatureColumnsCsv);

        if (enterpriseOptions.TestSize.HasValue)
        {
            psi.ArgumentList.Add("--test-size");
            psi.ArgumentList.Add(enterpriseOptions.TestSize.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (enterpriseOptions.RandomState.HasValue)
        {
            psi.ArgumentList.Add("--random-state");
            psi.ArgumentList.Add(enterpriseOptions.RandomState.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (enterpriseOptions.PcaVariance.HasValue)
        {
            psi.ArgumentList.Add("--pca-variance");
            psi.ArgumentList.Add(enterpriseOptions.PcaVariance.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (enterpriseOptions.MaxIter.HasValue)
        {
            psi.ArgumentList.Add("--max-iter");
            psi.ArgumentList.Add(enterpriseOptions.MaxIter.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (enterpriseOptions.ClassWeightBalanced == true)
            psi.ArgumentList.Add("--class-weight-balanced");

        if (enterpriseOptions.UsePca.HasValue)
        {
            psi.ArgumentList.Add(enterpriseOptions.UsePca.Value ? "--use-pca" : "--no-pca");
        }

        return await ExecutePythonTrainingAsync(psi, ct);
    }

    private sealed record EnterpriseOptions(
        string? InputCsv = null,
        string? SqlQuery = null,
        string? Table = null,
        string? TargetColumn = null,
        string? FeatureColumnsCsv = null,
        double? TestSize = null,
        int? RandomState = null,
        double? PcaVariance = null,
        bool? UsePca = null,
        int? MaxIter = null,
        bool? ClassWeightBalanced = null);

    private static bool TryParseEnterpriseOptions(string? paramsJson, out EnterpriseOptions options)
    {
        options = new EnterpriseOptions();
        if (string.IsNullOrWhiteSpace(paramsJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var inputCsv = TryReadString(root, "input_csv", "inputCsv");
            var sqlQuery = TryReadString(root, "sql_query", "sqlQuery");
            var table = TryReadString(root, "table");
            var targetColumn = TryReadString(root, "target_column", "targetColumn");
            var featureColumnsCsv = TryReadFeatureColumns(root);
            var testSize = TryReadDouble(root, "test_size", "testSize");
            var randomState = TryReadInt(root, "random_state", "randomState");
            var pcaVariance = TryReadDouble(root, "pca_variance", "pcaVariance");
            var usePca = TryReadBool(root, "use_pca", "usePca");
            var maxIter = TryReadInt(root, "max_iter", "maxIter");
            var classWeightBalanced = TryReadBool(root, "class_weight_balanced", "classWeightBalanced");

            options = new EnterpriseOptions(
                InputCsv: inputCsv,
                SqlQuery: sqlQuery,
                Table: table,
                TargetColumn: targetColumn,
                FeatureColumnsCsv: featureColumnsCsv,
                TestSize: testSize,
                RandomState: randomState,
                PcaVariance: pcaVariance,
                UsePca: usePca,
                MaxIter: maxIter,
                ClassWeightBalanced: classWeightBalanced);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildEnterpriseSqlQuery(string tableName, int take)
    {
        var effective = string.IsNullOrWhiteSpace(tableName)
            ? OpenTrainingModelCatalog.EnterpriseDefaultFeatureViewName
            : tableName.Trim();

        if (!IsSimpleIdentifier(effective))
            throw new InvalidOperationException(
                $"Invalid enterprise training table/view name '{effective}'. Use a simple identifier.");

        var takeLiteral = Math.Max(1, take).ToString(CultureInfo.InvariantCulture);
        return $"""
            WITH src AS (
                SELECT *
                FROM {effective}
                WHERE sell_probability_rs_label IS NOT NULL
                LIMIT {takeLiteral}
            )
            SELECT
                CASE
                    WHEN COALESCE(src.typical_price_prior, 0) <= 0 OR COALESCE(src.price, 0) <= 0 THEN 0.5
                    ELSE LEAST(1.0, GREATEST(0.0,
                        1.0 - ABS(src.price - src.typical_price_prior) / NULLIF(src.typical_price_prior, 0)
                    ))
                END AS price_fit,
                CASE
                    WHEN COALESCE(src.typical_price_prior, 0) <= 0 OR COALESCE(src.price, 0) <= 0 THEN 0.5
                    ELSE LEAST(1.0, GREATEST(0.0,
                        ((src.price - src.typical_price_prior) / NULLIF(src.typical_price_prior * 0.60, 0)) + 0.5
                    ))
                END AS margin,
                CASE
                    WHEN src.popularity_prior IS NULL THEN 0.5
                    ELSE LEAST(1.0, GREATEST(0.0,
                        CASE WHEN ABS(src.popularity_prior) > 1.5
                            THEN src.popularity_prior / 100.0
                            ELSE src.popularity_prior
                        END
                    ))
                END AS popularity,
                CASE
                    WHEN src.momentum_30d IS NULL THEN 0.5
                    WHEN ABS(src.momentum_30d) <= 1.5
                        THEN LEAST(1.0, GREATEST(0.0, (src.momentum_30d + 1.0) / 2.0))
                    ELSE LEAST(1.0, GREATEST(0.0, ((src.momentum_30d / 100.0) + 1.0) / 2.0))
                END AS trend_momentum,
                LEAST(1.0, GREATEST(0.0,
                    (LEAST(COALESCE(src.review_count, 0), 200)::NUMERIC / 200.0) * 0.60 +
                    CASE
                        WHEN src.priors_level = 'brand_category' THEN 0.40
                        WHEN src.priors_level = 'category' THEN 0.30
                        WHEN src.priors_level = 'brand' THEN 0.20
                        ELSE 0.10
                    END
                )) AS source_coverage,
                CASE
                    WHEN src.supply_demand_ratio_30d IS NOT NULL THEN LEAST(1.0, GREATEST(0.0,
                        src.supply_demand_ratio_30d / NULLIF(src.supply_demand_ratio_30d + 1, 0)
                    ))
                    WHEN src.sell_through_velocity_30d IS NOT NULL THEN LEAST(1.0, GREATEST(0.0,
                        src.sell_through_velocity_30d / NULLIF(src.sell_through_velocity_30d + 1, 0)
                    ))
                    ELSE 0.5
                END AS local_demand,
                CASE WHEN src.has_image_embedding THEN 0.75 ELSE 0.35 END AS image_similarity,
                CASE
                    WHEN src.deal_score_prior IS NULL THEN 0.5
                    ELSE LEAST(1.0, GREATEST(0.0,
                        CASE WHEN ABS(src.deal_score_prior) > 1.5
                            THEN src.deal_score_prior / 100.0
                            ELSE src.deal_score_prior
                        END
                    ))
                END AS deal_score,
                CASE
                    WHEN src.priors_level = 'brand_category' THEN 0.80
                    WHEN src.priors_level = 'category' THEN 0.65
                    WHEN src.priors_level = 'brand' THEN 0.55
                    ELSE 0.45
                END AS supplier_score,
                LEAST(1.0, GREATEST(0.0,
                    0.5 + 0.25 * COS((2 * PI() * (EXTRACT(MONTH FROM COALESCE(src.updated_at, src.created_at)) - 1)) / 12.0)
                )) AS season_score,
                CASE WHEN COALESCE(src.sell_probability_rs_label, 0) >= 0.5 THEN 1 ELSE 0 END AS sold
            FROM src;
            """;
    }

    private static bool IsSimpleIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var first = value[0];
        if (!(char.IsLetter(first) || first == '_'))
            return false;

        for (var i = 1; i < value.Length; i++)
        {
            var ch = value[i];
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                return false;
        }

        return true;
    }

    private static string? TryReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetPropertyCaseInsensitive(root, name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }

        return null;
    }

    private static string? TryReadFeatureColumns(JsonElement root)
    {
        if (TryGetPropertyCaseInsensitive(root, "feature_columns", out var featureColumns) ||
            TryGetPropertyCaseInsensitive(root, "featureColumns", out featureColumns))
        {
            if (featureColumns.ValueKind == JsonValueKind.String)
            {
                var csv = featureColumns.GetString();
                return string.IsNullOrWhiteSpace(csv) ? null : csv.Trim();
            }

            if (featureColumns.ValueKind == JsonValueKind.Array)
            {
                var names = featureColumns
                    .EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                if (names.Length > 0)
                    return string.Join(",", names);
            }
        }

        return null;
    }

    private static double? TryReadDouble(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetPropertyCaseInsensitive(root, name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n))
                return n;

            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return null;
    }

    private static int? TryReadInt(JsonElement root, params string[] names)
    {
        var number = TryReadDouble(root, names);
        if (!number.HasValue)
            return null;

        return Convert.ToInt32(Math.Round(number.Value, MidpointRounding.AwayFromZero), CultureInfo.InvariantCulture);
    }

    private static bool? TryReadBool(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetPropertyCaseInsensitive(root, name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
                return n != 0;

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (bool.TryParse(text, out var b))
                    return b;
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ib))
                    return ib != 0;
            }
        }

        return null;
    }

    private static async Task<PythonTrainResult> ExecutePythonTrainingAsync(ProcessStartInfo psi, CancellationToken ct)
    {
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
        var predictionsPath = artifacts.TryGetProperty("predictions_path", out var pp) && pp.ValueKind == JsonValueKind.String
            ? pp.GetString()
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
            MaxFeatureValuesJson: ReadText(maxPath),
            PredictionsPath: !string.IsNullOrWhiteSpace(predictionsPath) ? Path.GetFullPath(predictionsPath) : null
        );
    }

    private static async Task<long> PersistModelVersionAsync(
        NpgsqlConnection conn,
        long runId,
        string modelType,
        PythonTrainResult result,
        string? runtimeTuningJson,
        bool activate,
        CancellationToken ct)
    {
        var mt = NormalizeModelType(modelType);

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
                runtime_tuning_json,
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
                @runtimeTuning::jsonb,
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
            ins.Parameters.AddWithValue("runtimeTuning", (object?)runtimeTuningJson ?? DBNull.Value);
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
        return modelId;
    }

    private static async Task RefreshSupplierTrainingDatasetAsync(AnalyticsDbContext analyticsDb, CancellationToken ct)
    {
        var connectionString = analyticsDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Analytics connection string missing for supplier ranking refresh.");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        try
        {
            await ExecuteNonQueryAsync(
                conn,
                "REFRESH MATERIALIZED VIEW CONCURRENTLY supplier_training_dataset_v1;",
                1800,
                ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "0A000")
        {
            await ExecuteNonQueryAsync(
                conn,
                "REFRESH MATERIALIZED VIEW supplier_training_dataset_v1;",
                1800,
                ct);
        }
    }

    private static async Task PersistSupplierPredictionsAsync(
        AnalyticsDbContext analyticsDb,
        PythonTrainResult result,
        long modelVersionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.PredictionsPath) || !File.Exists(result.PredictionsPath))
            throw new InvalidOperationException("Supplier ranking training completed without predictions.json artifact.");

        var payload = await File.ReadAllTextAsync(result.PredictionsPath, Encoding.UTF8, ct);
        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Supplier predictions artifact must be a JSON array.");

        var connectionString = analyticsDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Analytics connection string missing for supplier predictions persistence.");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string sql = """
            INSERT INTO supplier_ml_predictions (
                supplier_id,
                snapshot_date,
                model_type,
                model_version_id,
                ml_supplier_score,
                predicted_supplier_success_score,
                predicted_revenue_next_30d,
                predicted_margin_next_30d,
                predicted_sellthrough_next_30d,
                success_probability,
                top_feature_1,
                top_feature_2,
                top_feature_3,
                explanation_text
            )
            VALUES (
                @supplierId,
                @snapshotDate,
                @modelType,
                @modelVersionId,
                @mlSupplierScore,
                @predictedSupplierSuccessScore,
                @predictedRevenueNext30d,
                @predictedMarginNext30d,
                @predictedSellthroughNext30d,
                @successProbability,
                @topFeature1,
                @topFeature2,
                @topFeature3,
                @explanationText
            )
            ON CONFLICT (supplier_id, snapshot_date, model_type)
            DO UPDATE SET
                model_version_id = EXCLUDED.model_version_id,
                ml_supplier_score = EXCLUDED.ml_supplier_score,
                predicted_supplier_success_score = EXCLUDED.predicted_supplier_success_score,
                predicted_revenue_next_30d = EXCLUDED.predicted_revenue_next_30d,
                predicted_margin_next_30d = EXCLUDED.predicted_margin_next_30d,
                predicted_sellthrough_next_30d = EXCLUDED.predicted_sellthrough_next_30d,
                success_probability = EXCLUDED.success_probability,
                top_feature_1 = EXCLUDED.top_feature_1,
                top_feature_2 = EXCLUDED.top_feature_2,
                top_feature_3 = EXCLUDED.top_feature_3,
                explanation_text = EXCLUDED.explanation_text,
                created_at = NOW();
            """;

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("supplierId", row.GetProperty("supplier_id").GetInt32());
            cmd.Parameters.AddWithValue("snapshotDate", DateOnly.Parse(row.GetProperty("snapshot_date").GetString() ?? string.Empty, CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("modelType", OpenTrainingModelCatalog.SupplierRankingModelType);
            cmd.Parameters.AddWithValue("modelVersionId", modelVersionId);
            cmd.Parameters.AddWithValue("mlSupplierScore", Convert.ToDecimal(row.GetProperty("ml_supplier_score").GetDouble(), CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("predictedSupplierSuccessScore", Convert.ToDecimal(row.GetProperty("predicted_supplier_success_score").GetDouble(), CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("predictedRevenueNext30d", Convert.ToDecimal(row.GetProperty("predicted_revenue_next_30d").GetDouble(), CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("predictedMarginNext30d", Convert.ToDecimal(row.GetProperty("predicted_margin_next_30d").GetDouble(), CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("predictedSellthroughNext30d", Convert.ToDecimal(row.GetProperty("predicted_sellthrough_next_30d").GetDouble(), CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("successProbability", Convert.ToDecimal(row.GetProperty("success_probability").GetDouble(), CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("topFeature1", (object?)(row.TryGetProperty("top_feature_1", out var top1) ? top1.GetString() : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("topFeature2", (object?)(row.TryGetProperty("top_feature_2", out var top2) ? top2.GetString() : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("topFeature3", (object?)(row.TryGetProperty("top_feature_3", out var top3) ? top3.GetString() : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("explanationText", (object?)(row.TryGetProperty("explanation_text", out var explanation) ? explanation.GetString() : null) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task RefreshSupplierDecisionCachesAsync(AnalyticsDbContext analyticsDb, CancellationToken ct)
    {
        var connectionString = analyticsDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Analytics connection string missing for supplier decision cache refresh.");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        foreach (var relation in SupplierDecisionMaterializedViews)
        {
            try
            {
                await ExecuteNonQueryAsync(
                    conn,
                    $"REFRESH MATERIALIZED VIEW CONCURRENTLY {relation};",
                    1800,
                    ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "0A000")
            {
                await ExecuteNonQueryAsync(
                    conn,
                    $"REFRESH MATERIALIZED VIEW {relation};",
                    1800,
                    ct);
            }
        }
    }

    private static async Task CleanupFailedModelVersionAsync(
        NpgsqlConnection conn,
        long modelVersionId,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE model_version
            SET is_active = FALSE,
                notes = COALESCE(notes, '') || ' | deactivated after supplier prediction persistence failure'
            WHERE id = @id;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", modelVersionId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection conn,
        string sql,
        int timeoutSeconds,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn)
        {
            CommandTimeout = timeoutSeconds
        };
        await cmd.ExecuteNonQueryAsync(ct);
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
