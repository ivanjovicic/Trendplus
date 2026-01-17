using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Domain.Model.TrendShoes
{
    public class PexelsPhoto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("src")]
        public PexelsPhotoSrc Src { get; set; }
    }
}
