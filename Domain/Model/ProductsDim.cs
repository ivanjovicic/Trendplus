using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model
{
    public class ProductsDim
    {
        //[Key]
        public int ProductKey { get; set; }     // identity
        public int ProductId { get; set; }      // Id iz write baze

        //public string? PLU { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;

        public int? FootwearTypeId { get; set; }
        public int? SupplierId { get; set; }
        public int? SeasonId { get; set; }

        public decimal? PurchasePrice { get; set; }
        public decimal? PurchasePriceRsd { get; set; }
        public decimal? FirstSalePrice { get; set; }
        public decimal? SalePrice { get; set; }

        public bool IsActive { get; set; } = true;
    }

}
