using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Domain.Model.TrendShoes
{
    public class PexelsResponse
    {
        [JsonPropertyName("photos")]
        public List<PexelsPhoto> Photos { get; set; } = new();
    }
}
