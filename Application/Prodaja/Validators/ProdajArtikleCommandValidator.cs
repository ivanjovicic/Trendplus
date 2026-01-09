using FluentValidation;
using Application.Prodaja.Commands.ProdajArtikle;

namespace Application.Prodaja.Validators;

public class ProdajArtikleCommandValidator : AbstractValidator<ProdajArtikleCommand>
{
    public ProdajArtikleCommandValidator()
    {
        RuleFor(x => x.Stavke)
            .NotNull()
            .WithMessage("Lista stavki ne može biti null.")
            .NotEmpty()
            .WithMessage("Prodaja mora imati bar jednu stavku.");

        RuleForEach(x => x.Stavke)
            .ChildRules(stavka =>
            {
                stavka.RuleFor(s => s.IdArtikal)
                    .GreaterThan(0)
                    .WithMessage("IdArtikal mora biti ve?i od 0.");

                stavka.RuleFor(s => s.Kolicina)
                    .GreaterThan(0)
                    .WithMessage("Koli?ina mora biti ve?a od 0.");

                stavka.RuleFor(s => s.Cena)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Cena mora biti ve?a ili jednaka 0.");
            });

        RuleFor(x => x.NacinPlacanja)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.NacinPlacanja))
            .WithMessage("Na?in pla?anja ne može biti duži od 50 karaktera.");
    }
}
