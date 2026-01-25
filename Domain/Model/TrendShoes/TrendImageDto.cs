using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model.TrendShoes
{
    public record TrendImageDto(
        int Id,
        string ImageUrl,
        string Source, // "unsplash" | "pexels"
        string? PhotographerName = null,
        string? PhotographerUrl = null,
        string? SourceUrl = null
    );
}
