using FluentValidation;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Validation;

public sealed class UpdatePersonalizationRequestValidator : AbstractValidator<UpdatePersonalizationRequest>
{
    public UpdatePersonalizationRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.BirthDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Birth date cannot be in the future.");
    }
}
