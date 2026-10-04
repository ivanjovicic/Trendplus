namespace Trendplus2.Dtos;

/// <summary>
/// Machine-readable counting and event-effect basis of an analytics surface.
/// Values are stable codes owned by <c>Api.Services.SupplierTabBasisPolicy</c>;
/// clients map them to user text and must not infer equality across tabs
/// unless the codes match.
/// </summary>
public sealed class SupplierTabBasisDto
{
    public required string Tab { get; init; }
    public required string Version { get; init; }
    public required string SupplierAttribution { get; init; }
    public required string CostBasis { get; init; }
    public required string ReceiptPopulation { get; init; }
    public required string Cohort { get; init; }
    public required string PeriodSemantics { get; init; }
    /// <summary>Metric used to describe the observed pre/post event effect, when applicable.</summary>
    public string? EffectMetric { get; init; }
    /// <summary>Rule used to select the event date used by the cohort, when applicable.</summary>
    public string? EventSelection { get; init; }
    public required string StoreScope { get; init; }
    public required string UnknownSupplierPolicy { get; init; }
    public required string AsOfDate { get; init; }
    public required string Timezone { get; init; }
}
