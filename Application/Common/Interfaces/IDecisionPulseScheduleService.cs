using Application.Analytics.DecisionPulse;

namespace Application.Common.Interfaces;

public interface IDecisionPulseScheduleService
{
    Task<IReadOnlyList<DecisionPulseScheduleDefinition>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DecisionPulseScheduleDefinition>> ListEnabledAsync(CancellationToken ct = default);

    Task<DecisionPulseScheduleDefinition?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<DecisionPulseScheduleDefinition> UpsertAsync(long? id, DecisionPulseScheduleUpsertRequest request, CancellationToken ct = default);

    Task MarkRunResultAsync(long id, DecisionPulseScheduleRunResult result, CancellationToken ct = default);
}
