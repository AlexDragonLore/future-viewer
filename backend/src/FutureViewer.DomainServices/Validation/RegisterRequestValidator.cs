using FluentValidation;
using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.OfferAccepted)
            .Equal(true)
            .WithMessage("The public offer must be accepted.");
        RuleFor(x => x.PrivacyAcknowledged)
            .Equal(true)
            .WithMessage("The privacy policy must be acknowledged.");
        RuleFor(x => x.PersonalDataConsentAccepted)
            .Equal(true)
            .WithMessage("Consent to personal data processing must be accepted.");
        RuleFor(x => x.AgeConfirmed18)
            .Equal(true)
            .WithMessage("You must confirm that you are at least 18 years old.");
        RuleFor(x => x.CollectionSource)
            .Equal("registration")
            .MaximumLength(64);
        RuleFor(x => x.DocumentVersions.Offer).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DocumentVersions.Privacy).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DocumentVersions.PersonalDataConsent).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DocumentVersions.MarketingConsent).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DocumentVersions.Cookies).NotEmpty().MaximumLength(64);
    }
}
