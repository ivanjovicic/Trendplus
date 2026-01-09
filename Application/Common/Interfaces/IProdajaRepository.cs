using Application.Prodaja.Commands.ProdajArtikle;
using Application.Prodaja.Queries;

namespace Application.Common.Interfaces
{
    public interface IProdajaRepository
    {
        Task<int> ProdajAsync(
            ProdajArtikleCommand command,
            CancellationToken ct);

        Task<ProdajeListResponse> GetProdajeAsync(
            DateTime? fromDate,
            DateTime? toDate,
            int pageNumber,
            int pageSize,
            CancellationToken ct);
    }
}
