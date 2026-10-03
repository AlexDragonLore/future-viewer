using FluentValidation;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Validation;

public sealed class UpdatePersonalizationRequestValidator : AbstractValidator<UpdatePersonalizationRequest>
{
    public UpdatePersonalizationRequestValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(80);
        RuleFor(x => x.LastName).MaximumLength(80);
        RuleFor(x => x.BirthYear)
            .InclusiveBetween(1900, DateTime.UtcNow.Year)
            .When(x => x.BirthYear.HasValue)
            .WithMessage("Birth year is outside the supported range.");
    }
}
