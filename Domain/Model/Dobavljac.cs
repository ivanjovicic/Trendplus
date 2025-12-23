using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model
{
    [Table("Dobavljaci")]
    public class Dobavljac
    {
        public int Id { get; set; }
        public string? Naziv { get; set; }
        public string? Adresa { get; set; }
        public string? Telefon { get; set; }
        public string? Napomena { get; set; }
    }
}
