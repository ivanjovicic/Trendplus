using Api.Services.Access;
using Api.Services.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Config;

public enum ProcessType
{
    Web,
    Worker
}

public static class WorkerRuntimeConfig
{
    public static ProcessType ResolveProcessType(IConfiguration configuration, out string source)
    {
        var processTypeRaw = configuration["PROCESS_TYPE"];
        if (!string.IsNullOrWhiteSpace(processTypeRaw))
        {
            if (processTypeRaw.Equals("worker", StringComparison.OrdinalIgnoreCase))
            {
                source = "PROCESS_TYPE";
                return ProcessType.Worker;
            }

            if (processTypeRaw.Equals("web", StringComparison.OrdinalIgnoreCase))
            {
                source = "PROCESS_TYPE";
                return ProcessType.Web;
            }

            source = "PROCESS_TYPE_INVALID";
            return ProcessType.Web;
        }

        if (TryParseBooleanFlag(configuration["WORKER_PROCESS"], out var aliasValue) && aliasValue)
        {
            source = "WORKER_PROCESS";
            return ProcessType.Worker;
        }

        source = "DEFAULT_WEB";
        return ProcessType.Web;
    }

    public static bool ResolveWorkersEnabled(
        bool? configuredWorkersEnabled,
        ProcessType processType,
        bool isDevelopment)
    {
        return configuredWorkersEnabled ?? (processType == ProcessType.Worker || isDevelopment);
    }

    public static string ResolveWorkersEnabledSource(
        bool? configuredWorkersEnabled,
        ProcessType processType,
        bool isDevelopment)
    {
        if (configuredWorkersEnabled.HasValue)
        {
            return "config";
        }

        if (processType == ProcessType.Worker)
        {
            return "worker-default";
        }

        return isDevelopment ? "development-default" : "web-default";
    }

    public static void RegisterWorkerHostedServices(IServiceCollection services, bool isWorkerProcess)
    {
        if (!isWorkerProcess)
        {
            return;
        }

        services.AddHostedService<Workers.SyncWorker>();
        services.AddHostedService<Workers.OutboxProcessorWorker>();
        services.AddHostedService<Workers.AnalyticsAggregationWorker>();
        services.AddHostedService<Workers.AnalyticsDataQualityHealthWorker>();
        services.AddHostedService<Workers.NightlyAnalyticsRefreshWorker>();
        services.AddHostedService<Workers.OpenTrainingModelTrainingWorker>();
        services.AddHostedService<Workers.TrendIngestionWorker>();
        services.AddHostedService<Workers.DocumentGenerationWorker>();
        services.AddHostedService<Workers.InventoryReportSchedulerWorker>();
        services.AddHostedService<AccessImportBackgroundWorker>();
        services.AddHostedService<DeferredStartupTasksHostedService>();
        // services.AddHostedService<Workers.DatabaseKeepAliveWorker>();
    }

    private static bool TryParseBooleanFlag(string? rawValue, out bool value)
    {
        if (bool.TryParse(rawValue, out value))
        {
            return true;
        }

        if (string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "y", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (string.Equals(rawValue, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "no", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rawValue, "n", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
}
