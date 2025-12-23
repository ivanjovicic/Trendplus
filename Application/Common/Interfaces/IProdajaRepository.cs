using Application.Prodaja.Commands.ProdajArtikle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IProdajaRepository
    {
        Task<int> ProdajAsync(
            ProdajArtikleCommand command,
            CancellationToken ct);
    }

}
