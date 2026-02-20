using System.Collections.Generic;

namespace Api.Models
{
    public class CommonMatchesFilters
    {
        public string? Gender { get; set; }
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public int? PriceMin { get; set; }
        public int? PriceMax { get; set; }
        public string? Sort { get; set; }
        public bool? Sale { get; set; }
        public bool? IsNew { get; set; }
        public int Pages { get; set; } = 1;
        public int MinScore { get; set; } = 60; // default minimum match score
    }

    public class ProductSide
    {
        public string Name { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class CommonProductItem
    {
        public int Score { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public ProductSide Zalando { get; set; } = new ProductSide();
        public ProductSide Deichmann { get; set; } = new ProductSide();
    }

    public class CommonProductsResponse
    {
        public int Count { get; set; }
        public List<CommonProductItem> Items { get; set; } = new List<CommonProductItem>();
    }
}
