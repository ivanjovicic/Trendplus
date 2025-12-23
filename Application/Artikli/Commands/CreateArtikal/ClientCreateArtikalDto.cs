using System.Text.Json.Serialization;

namespace Application.Artikli.Commands.CreateArtikal
{
    public sealed class ClientCreateArtikalDto
    {
        [JsonPropertyName("plu")]
        public string? PLU { get; set; }

        [JsonPropertyName("naziv")]
        public string Naziv { get; set; } = string.Empty;

        [JsonPropertyName("prodajnaCena")]
        public decimal ProdajnaCena { get; set; }

        [JsonPropertyName("nabavnaCena")]
        public decimal? NabavnaCena { get; set; }

        [JsonPropertyName("nabavnaCenaDin")]
        public decimal? NabavnaCenaDin { get; set; }

        [JsonPropertyName("prvaProdajnaCena")]
        public decimal? PrvaProdajnaCena { get; set; }

        [JsonPropertyName("kolicina")]
        public int? Kolicina { get; set; }

        [JsonPropertyName("komentar")]
        public string? Komentar { get; set; }

        [JsonPropertyName("tipObuceId")]
        public int? TipObuceId { get; set; }

        [JsonPropertyName("dobavljacId")]
        public int? DobavljacId { get; set; }

        [JsonPropertyName("idObjekat")]
        public int? IDObjekat { get; set; }

        [JsonPropertyName("idSezona")]
        public int? IDSezona { get; set; }
    }
}
