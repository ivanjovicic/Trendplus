using System.Collections.Generic;

namespace Trendplus2.Dtos
{
    public class CommonProductsResponse
    {
        public int Count { get; set; }
        public List<CommonProductItemDto> Items { get; set; } = new();
    }

    public class ProductSideDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class CommonProductItemDto
    {
        public int Score { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        public ProductSideDto Zalando { get; set; } = new();
        public ProductSideDto Deichmann { get; set; } = new();
    }

    // Legacy shapes (kept for compatibility)
    public class CommonProductItem
    {
        public string Brand { get; set; } = "";
        public string Name_Zalando { get; set; } = "";
        public string Name_Deichmann { get; set; } = "";
        public string Price_Zalando { get; set; } = "";
        public string Price_Deichmann { get; set; } = "";
        public string Image_Zalando { get; set; } = "";
        public string Image_Deichmann { get; set; } = "";
        public string Url_Zalando { get; set; } = "";
        public string Url_Deichmann { get; set; } = "";
    }

    public class CommonProductsFilters
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
    }
}
