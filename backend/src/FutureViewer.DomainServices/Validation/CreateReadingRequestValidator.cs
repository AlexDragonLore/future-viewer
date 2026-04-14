using FluentValidation;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Validation;

public sealed class CreateReadingRequestValidator : AbstractValidator<CreateReadingRequest>
{
    public CreateReadingRequestValidator()
    {
        RuleFor(x => x.SpreadType).IsInEnum();
        RuleFor(x => x.Question).NotEmpty().MaximumLength(500);
    }
}
