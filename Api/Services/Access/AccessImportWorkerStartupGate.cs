using Api.Config;
using Api.Services.Startup;

namespace Api.Services.Access;

internal static class AccessImportWorkerStartupGate
{
    internal static bool ShouldGateQueueOperationsOnStartupReadiness(ProcessType processType)
        => processType == ProcessType.Web;

    internal static bool CanPerformQueueOperations(bool gateEnabled, StartupReadinessState readiness)
    {
        if (!gateEnabled)
        {
            return true;
        }

        return readiness.IsReady;
    }

    internal static bool IsTerminalStartupFailure(StartupReadinessState readiness)
        => string.Equals(readiness.Reason, "database_initialization_failed", StringComparison.Ordinal);
}
