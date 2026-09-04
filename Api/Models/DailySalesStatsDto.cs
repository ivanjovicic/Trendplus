using Trendplus2.Dtos;

namespace Api.Models;

public sealed class DailySalesTableResponse
{
    public DateTime RequestedFrom { get; set; }
    public DateTime RequestedTo { get; set; }
    public int? StoreId { get; set; }
    public int TopN { get; set; }
    public string DataScope { get; set; } = "all";
    public List<DailySalesSupplierHeaderDto> TopSuppliers { get; set; } = [];
    public List<string> TopSuppliersOrder { get; set; } = [];
    public List<DailySalesRowDto> DateRows { get; set; } = [];
    public DailySalesMetadata Metadata { get; set; } = new();
    public AnalyticsResponseMetaDto Meta { get; set; } = AnalyticsResponseMetaFactory.Success();
}

public sealed class DailySalesSupplierHeaderDto
{
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "Nepoznato";
    public bool IsUnknown { get; set; }
    public int TotalQty { get; set; }
    public decimal TotalRevenue { get; set; }
}

public sealed class DailySalesRowDto
{
    public DateTime Date { get; set; }
    public int FirstShiftTotalItems { get; set; }
    public int SecondShiftTotalItems { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<int> TopSupplierCounts { get; set; } = [];
    public int OthersCount { get; set; }
    public int TotalItemsSold { get; set; }
}

public sealed class DailySalesMetadata
{
    public int TotalDays { get; set; }
    public int UniqueSuppliersInRange { get; set; }
    public decimal? UnknownSupplierPct { get; set; }
    public int UnknownSupplierItems { get; set; }
    public int OffShiftItems { get; set; }
    public decimal OffShiftRevenue { get; set; }
    public int TotalItemsInRange { get; set; }
    public int DuplicateReceiptGroupCount { get; set; }
    public int DuplicateReceiptHeaderCount { get; set; }
    public int ReceiptAmountMismatchCount { get; set; }
    public decimal ReceiptAmountMismatchRevenue { get; set; }
    public int NonStandardReceiptCount { get; set; }
    public decimal NonStandardReceiptRevenue { get; set; }
    public int DebtReceiptCount { get; set; }
    public decimal DebtReceiptRevenue { get; set; }
    /// <summary>Earliest available sale date in the whole dataset (null if no data at all).</summary>
    public DateTime? MinAvailableDate { get; set; }
    /// <summary>Latest available sale date in the whole dataset (null if no data at all).</summary>
    public DateTime? MaxAvailableDate { get; set; }
    public List<string> Warnings { get; set; } = [];
}
