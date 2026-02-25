using System;
using System.Collections.Generic;

namespace Api.Models
{
    public class CanonicalItemDto
    {
        public long ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string? Category { get; set; }
        public decimal? LatestScore { get; set; }
        public string? ImgUrl { get; set; }
    }

    public class CanonicalItemsResponse
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<CanonicalItemDto> Items { get; set; } = new();
    }

    public class TrendingItemDto
    {
        public long ItemId { get; set; }
        public decimal FinalScore { get; set; }
        public decimal BaseScore { get; set; }
        public decimal Momentum { get; set; }
        public decimal MomentumRaw { get; set; }
        public int? Rank { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImgUrl { get; set; }
    }

    public class TrendingResponse
    {
        public long RunId { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<TrendingItemDto> Items { get; set; } = new();
    }

    public class MomentumItemDto
    {
        public long ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal Momentum { get; set; }
        public decimal MomentumRaw { get; set; }
        public decimal BaseScore { get; set; }
        public decimal FinalScore { get; set; }
        public string? ImgUrl { get; set; }
    }

    public class MomentumResponse
    {
        public long RunId { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<MomentumItemDto> Items { get; set; } = new();
    }

    public class MarketTopItemDto
    {
        public long ItemId { get; set; }
        public decimal FinalScore { get; set; }
        public int? MarketRank { get; set; }
        public decimal MarketScore { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImgUrl { get; set; }
    }

    public class MarketsResponse
    {
        public long RunId { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public string Market { get; set; } = "DE";
        public string Currency { get; set; } = "EUR";
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<MarketTopItemDto> TopItems { get; set; } = new();
    }

    public class ScoreComponentDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal Weight { get; set; }
    }

    public class ItemSourceDto
    {
        public string Source { get; set; } = string.Empty;
        public string Market { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string? Currency { get; set; }
        public bool Available { get; set; }
        public string? ProductUrl { get; set; }
    }

    public class DebugScoreResponse
    {
        public long ItemId { get; set; }
        public long RunId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal BaseScore { get; set; }
        public decimal FinalScore { get; set; }
        public decimal Momentum { get; set; }
        public decimal MomentumRaw { get; set; }
        public List<ScoreComponentDto> Components { get; set; } = new();
        public List<ItemSourceDto> Sources { get; set; } = new();
    }
}

