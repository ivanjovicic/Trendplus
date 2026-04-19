using System.ComponentModel.DataAnnotations;

namespace Domain.Model.Analytics;

public sealed class AnalyticsSaleLineCostSnapshot
{
    [Key]
    public long Id { get; set; }

    public long BatchId { get; set; }
    public int ProdajaStavkaId { get; set; }
    public decimal ResolvedUnitCost { get; set; }
    public short CostSource { get; set; }
    public decimal? ProductCostRsdAtSnapshot { get; set; }
    public decimal? ProductCostLegacyAtSnapshot { get; set; }
    public int ArtikalId { get; set; }

    // Navigation
    public AnalyticsCostSnapshotBatch Batch { get; set; } = null!;
}
