namespace Api.Models;

public sealed class VendorSalesNivelacijaArticleStatDto
{
    public DateTime EventDate { get; set; }
    public int? VendorId { get; set; }
    public string VendorName { get; set; } = "N/A";
    public string Sku { get; set; } = string.Empty;
    public string ArticleName { get; set; } = string.Empty;
    public string Category { get; set; } = "N/A";
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public int PreQty { get; set; }
    public decimal PreRevenue { get; set; }
    public int PostQty { get; set; }
    public decimal PostRevenue { get; set; }
    public int ChangeQty { get; set; }
    public decimal ChangeRevenue { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal CoveragePre30 { get; set; }
    public decimal CoveragePost30 { get; set; }
    public bool HasSalesWindow { get; set; }
    public bool PriceChanged { get; set; }
    public decimal? PriceChangePercent { get; set; }

    // --- Advanced metrics ---
    public decimal? Rolling7dPreRevenue { get; set; }
    public decimal? Rolling7dPostRevenue { get; set; }
    public decimal? MomentumRevenue { get; set; }
    public decimal? PriceElasticity { get; set; }
    public decimal? DidRevenue { get; set; }
    public decimal? DidQty { get; set; }
    public decimal? LostSalesOOS { get; set; }
    public decimal? OOSRate { get; set; }
    public string? MetricReason { get; set; } // null if all metrics are valid, else reason for nulls
}

public sealed class VendorSalesNivelacijaVendorStatDto
{
    public int? VendorId { get; set; }
    public string VendorName { get; set; } = "N/A";
    public int PreQty { get; set; }
    public decimal PreRevenue { get; set; }
    public int PostQty { get; set; }
    public decimal PostRevenue { get; set; }
    public int ChangeQty { get; set; }
    public decimal ChangeRevenue { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal AbsoluteChangeRevenue { get; set; }
    public decimal ChangeSharePercent { get; set; }
    public decimal PostRevenueSharePercent { get; set; }
    public decimal AvgCoveragePre30 { get; set; }
    public decimal AvgCoveragePost30 { get; set; }
    public int ArticleCount { get; set; }
    public int ActiveArticlesCount { get; set; }
    public int IncreasedPriceArticlesCount { get; set; }
    public int DecreasedPriceArticlesCount { get; set; }
}

public sealed class VendorSalesNivelacijaTotalsDto
{
    public int PreQty { get; set; }
    public decimal PreRevenue { get; set; }
    public int PostQty { get; set; }
    public decimal PostRevenue { get; set; }
    public int ChangeQty { get; set; }
    public decimal ChangeRevenue { get; set; }
    public decimal ChangePercent { get; set; }
    public int VendorsCount { get; set; }
    public int ArticlesCount { get; set; }
    public int ActiveArticlesCount { get; set; }
    public decimal AvgRevenuePerArticlePre { get; set; }
    public decimal AvgRevenuePerArticlePost { get; set; }
    public decimal AvgPriceChangePercent { get; set; }
    public decimal AbsoluteChangeRevenue { get; set; }
    public decimal AvgCoveragePre30 { get; set; }
    public decimal AvgCoveragePost30 { get; set; }
}

public sealed class VendorSalesNivelacijaDataQualityDto
{
    public int RawRows { get; set; }
    public int DeduplicatedRows { get; set; }
    public int DuplicateRowsRemoved { get; set; }
    public int InactiveRows { get; set; }
    public int UnchangedPriceRows { get; set; }
    public int AnalyzedRows { get; set; }
    public decimal AnalyzedSharePercent { get; set; }
    public int LowPostCoverageRows { get; set; }
    public decimal AvgCoveragePre30 { get; set; }
    public decimal AvgCoveragePost30 { get; set; }
}

public sealed class VendorSalesNivelacijaCategoryStatDto
{
    public string Category { get; set; } = "N/A";
    public int ArticlesCount { get; set; }
    public int VendorsCount { get; set; }
    public int PreQty { get; set; }
    public decimal PreRevenue { get; set; }
    public int PostQty { get; set; }
    public decimal PostRevenue { get; set; }
    public int ChangeQty { get; set; }
    public decimal ChangeRevenue { get; set; }
    public decimal ChangePercent { get; set; }
}

public sealed class VendorSalesNivelacijaPriceDirectionStatDto
{
    public string Segment { get; set; } = string.Empty;
    public int ArticlesCount { get; set; }
    public int VendorsCount { get; set; }
    public decimal AvgPriceChangePercent { get; set; }
    public decimal ChangeRevenue { get; set; }
    public decimal ChangePercent { get; set; }
}

public sealed class VendorSalesNivelacijaInsightDto
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Tone { get; set; } = "neutral";
}

public sealed class VendorSalesNivelacijaResponseDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public int WindowDays { get; set; } = 30;
    public int? VendorId { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Category { get; set; }
    public bool IncludeInactive { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<VendorSalesNivelacijaVendorStatDto> VendorStats { get; set; } = [];
    public List<VendorSalesNivelacijaArticleStatDto> ArticleStats { get; set; } = [];
    public VendorSalesNivelacijaTotalsDto Totals { get; set; } = new();
    public VendorSalesNivelacijaDataQualityDto DataQuality { get; set; } = new();
    public List<VendorSalesNivelacijaCategoryStatDto> CategoryStats { get; set; } = [];
    public List<VendorSalesNivelacijaPriceDirectionStatDto> PriceDirectionStats { get; set; } = [];
    public List<VendorSalesNivelacijaInsightDto> Insights { get; set; } = [];

    // Advanced metrics summary
    public decimal? AvgMomentumRevenue { get; set; }
    public decimal? AvgElasticity { get; set; }
    public decimal? AvgDidRevenue { get; set; }
    public decimal? AvgLostSalesOOS { get; set; }
    public decimal? OOSRate { get; set; }
    public string? MetricsStatus { get; set; } // null if all metrics valid, else reason
}

public sealed class VendorSalesNivelacijaOptionDto
{
    public DateTime EventDate { get; set; }
    public int EventsCount { get; set; }
    public int VendorsCount { get; set; }
    public int ArticlesCount { get; set; }
    public int ActiveArticlesCount { get; set; }
    public bool HasSalesWindow { get; set; }
    public string Label { get; set; } = string.Empty;
}
