using FluentValidation;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Validation;

public sealed class SubmitFeedbackValidator : AbstractValidator<SubmitFeedbackRequest>
{
    public SubmitFeedbackValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(64);
        RuleFor(x => x.SelfReport).NotEmpty().MinimumLength(10).MaximumLength(4000);
    }
}
