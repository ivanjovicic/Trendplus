using Api.Services.Access;
using Api.Services.Startup;
using Infrastructure.Services;
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

        foreach (var definition in WorkerRegistryCatalog.Definitions.Where(d => d.RegistersInWorkerProcess))
        {
            RegisterByWorkerName(services, definition.WorkerName);
        }
    }

    public static void RegisterWebEligibleWorker(IServiceCollection services, string workerName)
    {
        RegisterByWorkerName(services, workerName);
    }

    public static bool ResolveAccessImportWorkerInWebProcess(
        bool? configuredRegisterInWebProcess,
        bool accessImportWorkerEnabled,
        bool workersExplicitlyDisabled,
        ProcessType processType)
    {
        if (processType != ProcessType.Web)
        {
            return false;
        }

        if (workersExplicitlyDisabled || !accessImportWorkerEnabled)
        {
            return false;
        }

        return configuredRegisterInWebProcess ?? true;
    }

    public static bool IsRegisteredInCurrentProcess(
        WorkerRegistryDefinition definition,
        ProcessType processType,
        bool registerAccessImportWorkerInWebProcess)
    {
        if (processType == ProcessType.Worker)
        {
            return definition.RegistersInWorkerProcess;
        }

        if (!definition.RegistersInWebProcess)
        {
            return false;
        }

        if (definition.RequiresWebAccessImportFlag)
        {
            return registerAccessImportWorkerInWebProcess;
        }

        return true;
    }

    private static void RegisterByWorkerName(IServiceCollection services, string workerName)
    {
        switch (workerName)
        {
            case "SyncWorker":
                services.AddHostedService<Workers.SyncWorker>();
                return;
            case "OutboxProcessorWorker":
                services.AddHostedService<Workers.OutboxProcessorWorker>();
                return;
            case "AnalyticsAggregationWorker":
                services.AddHostedService<Workers.AnalyticsAggregationWorker>();
                return;
            case "AnalyticsDataQualityHealthWorker":
                services.AddHostedService<Workers.AnalyticsDataQualityHealthWorker>();
                return;
            case "NightlyAnalyticsRefreshWorker":
                services.AddHostedService<Workers.NightlyAnalyticsRefreshWorker>();
                return;
            case "OpenTrainingModelTrainingWorker":
                services.AddHostedService<Workers.OpenTrainingModelTrainingWorker>();
                return;
            case "TrendIngestionWorker":
                services.AddHostedService<Workers.TrendIngestionWorker>();
                return;
            case "DocumentGenerationWorker":
                services.AddHostedService<Workers.DocumentGenerationWorker>();
                return;
            case "InventoryReportSchedulerWorker":
                services.AddHostedService<Workers.InventoryReportSchedulerWorker>();
                return;
            case "AccessImportBackgroundWorker":
                services.AddHostedService<AccessImportBackgroundWorker>();
                return;
            case "WorkerRuntimeSettingsSchemaBootstrapHostedService":
                services.AddHostedService<WorkerRuntimeSettingsSchemaBootstrapHostedService>();
                return;
            case "DeferredStartupTasksHostedService":
                services.AddHostedService<DeferredStartupTasksHostedService>();
                return;
            default:
                return;
        }
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
