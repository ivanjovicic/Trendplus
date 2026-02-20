namespace Trendplus2.Dtos
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
    }
}