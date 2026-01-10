using MediatR;
using System.Collections.Generic;

namespace Application.Povracaj.Commands
{
    public record KreirajPovracajCommand(
        int IDDobavljac,
        string? RazlogPovracaja,
        string? Komentar,
        List<PovracajStavkaDto> Stavke
    ) : IRequest<KreirajPovracajResponse>;

    public record PovracajStavkaDto(
        int IdArtikal,
        int Kolicina,
        decimal Cena,
        string? Razlog,
        string? StanjeArtikla
    );

    public record KreirajPovracajResponse(
        int PovracajId,
        string BrojZapisnika,
        decimal UkupanIznos
    );
}
