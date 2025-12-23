using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model
{
    public class StoresDim
    {
        public int StoreKey { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = null!;
        public string? City { get; set; }
        public string? Region { get; set; }
    }
}
