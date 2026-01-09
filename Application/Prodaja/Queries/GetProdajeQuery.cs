using MediatR;

namespace Application.Prodaja.Queries;

public record GetProdajeQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<ProdajeListResponse>;

public record ProdajeListResponse(
    List<ProdajaListItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public record ProdajaListItemDto(
    int Id,
    string BrojRacuna,
    DateTime DatumProdaje,
    decimal UkupanIznos,
    int BrojStavki,
    string NacinPlacanja
);
