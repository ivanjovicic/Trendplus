using System;

namespace Domain.Model
{
    public class CrossPlatformProductAvailability
    {
        public int Id { get; set; }
        public string Brand { get; set; } = "";
        public string NormalizedName { get; set; } = "";
        public string ZalandoUrl { get; set; } = "";
        public string DeichmannUrl { get; set; } = "";
        public decimal PriceZalando { get; set; }
        public decimal PriceDeichmann { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
