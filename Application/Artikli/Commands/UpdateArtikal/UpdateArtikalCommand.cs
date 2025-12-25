using MediatR;

namespace Application.Artikli.Commands.UpdateArtikal
{
    /// <summary>
    /// Komanda za izmenu postojećeg artikla.
    /// </summary>
    public sealed record UpdateArtikalCommand(
        int Id,
        string Naziv,
        int? TipObuceId,
        int? DobavljacId,
        decimal? NabavnaCena,
        decimal? NabavnaCenaDin,
        decimal? PrvaProdajnaCena,
        decimal ProdajnaCena,
        int? Kolicina,
        string? Komentar,
        int? IDObjekat,
        int? IDSezona
    ) : IRequest<Unit>;
}
