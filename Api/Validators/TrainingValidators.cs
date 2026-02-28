using Api.Controllers;
using FluentValidation;

namespace Api.Validators;

public sealed class StartTrainingRunRequestValidator : AbstractValidator<StartTrainingRunRequestDto>
{
    public StartTrainingRunRequestValidator()
    {
        RuleFor(x => x.ModelType)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.FeatureViewName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DatasetId)
            .GreaterThan(0)
            .When(x => x.DatasetId.HasValue);

        RuleFor(x => x.DatasetName)
            .MaximumLength(100)
            .When(x => x.DatasetName is not null);

        RuleFor(x => x.CodeVersion)
            .MaximumLength(200)
            .When(x => x.CodeVersion is not null);
    }
}

public sealed class RecomputeSellProbabilityLabelsRequestValidator : AbstractValidator<RecomputeSellProbabilityLabelsRequestDto>
{
    public RecomputeSellProbabilityLabelsRequestValidator()
    {
        RuleFor(x => x.HorizonDays)
            .InclusiveBetween(1, 365);

        RuleFor(x => x.LabelVersion)
            .NotEmpty()
            .MaximumLength(50);
    }
}

