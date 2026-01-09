using FluentValidation;

namespace Application.Artikli.Commands.CreateArtikal;

public class CreateArtikalCommandValidator : AbstractValidator<CreateArtikalCommand>
{
    public CreateArtikalCommandValidator()
    {
        RuleFor(x => x.Naziv)
            .NotEmpty()
            .WithMessage("Naziv artikla je obavezan.")
            .MaximumLength(200)
            .WithMessage("Naziv artikla ne može biti duži od 200 karaktera.");

        RuleFor(x => x.ProdajnaCena)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Prodajna cena mora biti ve?a ili jednaka 0.");

        RuleFor(x => x.NabavnaCena)
            .GreaterThanOrEqualTo(0)
            .When(x => x.NabavnaCena.HasValue)
            .WithMessage("Nabavna cena mora biti ve?a ili jednaka 0.");

        RuleFor(x => x.NabavnaCenaDin)
            .GreaterThanOrEqualTo(0)
            .When(x => x.NabavnaCenaDin.HasValue)
            .WithMessage("Nabavna cena (DIN) mora biti ve?a ili jednaka 0.");

        RuleFor(x => x.PrvaProdajnaCena)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PrvaProdajnaCena.HasValue)
            .WithMessage("Prva prodajna cena mora biti ve?a ili jednaka 0.");

        RuleFor(x => x.Kolicina)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Kolicina.HasValue)
            .WithMessage("Koli?ina mora biti ve?a ili jednaka 0.");

        RuleFor(x => x.Komentar)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.Komentar))
            .WithMessage("Komentar ne može biti duži od 1000 karaktera.");
    }
}
