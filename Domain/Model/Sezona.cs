namespace Domain.Model
{
    public class Sezona
    {
        public int Id { get; set; }
        public string Naziv { get; set; } = string.Empty; // npr. "Jesen/Zima 2024"
        public DateTime DatumOd { get; set; }
        public DateTime DatumDo { get; set; }
        public string DataOrigin { get; set; } = "existing";
    }
}
