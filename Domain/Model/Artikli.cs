using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Domain.Model
{
    public class Artikli
    {
        [Key]
        public int Id { get; set; }
        public string? PLU { get; set; }
        public string Naziv { get; set; } = string.Empty;
        public int? IDTipObuce { get; set; }
        public int? IDDobavljac { get; set; }
        public decimal? NabavnaCena { get; set; }
        public decimal? NabavnaCenaDin { get; set; }
        public decimal? PrvaProdajnaCena { get; set; }
        public decimal? ProdajnaCena { get; set; }
        
        // Dodatna polja za cipele
        public string? Velicina { get; set; }   // Veličina cipela (npr. "42", "43", "EU 42")
        public string? Boja { get; set; }       // Boja cipela (npr. "Crna", "Braon", "Bela")
        
        public int? Kolicina { get; set; }
        public int? MinimalnaKolicina { get; set; } // Za analytics i reorder suggestions
        public string? Komentar { get; set; }
        public int? IDObjekat { get; set; }
        public int? IDSezona { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Kategorije za analitiku
        public string? Kategorija { get; set; } // "Patike", "Cipele", "Sandale", "Čizme", "Ostalo"
        public string? Pol { get; set; } // "Muško", "Žensko", "Dečije", "Unisex"
        public string? Materijal { get; set; } // "Koža", "Tekstil", "Sintetika", "Guma", "Nabuk", "Platno"

        // Origin of the row: "existing" or "access"
        public string DataOrigin { get; set; } = "existing";
        
        // Image support
        public string? ImagePath { get; set; } // Path to main product image
        
        // Navigation property for multiple images
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }

    /// <summary>
    /// Stores product images with AI embeddings for similarity search
    /// </summary>
    public class ProductImage
    {
        [Key]
        public Guid Id { get; set; }
        
        public int ProductId { get; set; }
        public Artikli Product { get; set; } = null!;
        
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// Vector embedding for image similarity search (512 dimensions)
        /// Stored as float array, mapped to pgvector in PostgreSQL via Pgvector.EntityFrameworkCore
        /// </summary>
        public float[]? Embedding { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsPrimary { get; set; } = false;
    }
}
