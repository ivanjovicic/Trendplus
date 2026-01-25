using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Model.TrendShoes
{
    public sealed class SeasonalImageDto
    {
        public int Id { get; init; }
        public string ImageUrl { get; init; } = string.Empty;
    }
}
