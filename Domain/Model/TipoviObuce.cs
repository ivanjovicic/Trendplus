using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Domain.Model
{
    [Table("TipoviObuce")]
    public class TipObuce
    {
        public int Id { get; set; }
        public string Naziv { get; set; }
    }
}
