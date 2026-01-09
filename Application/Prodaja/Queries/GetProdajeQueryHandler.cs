using Application.Common.Interfaces;
using MediatR;

namespace Application.Prodaja.Queries;

public class GetProdajeQueryHandler : IRequestHandler<GetProdajeQuery, ProdajeListResponse>
{
    private readonly IProdajaRepository _repository;

    public GetProdajeQueryHandler(IProdajaRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProdajeListResponse> Handle(GetProdajeQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetProdajeAsync(
            request.FromDate,
            request.ToDate,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}
