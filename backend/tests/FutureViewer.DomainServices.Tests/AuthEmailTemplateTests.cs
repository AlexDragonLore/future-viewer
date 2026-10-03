using FluentAssertions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.DomainServices.Tests;

public sealed class AuthEmailTemplateTests
{
    [Fact]
    public void Verification_explains_registration_and_its_expiry()
    {
        var html = AuthEmailTemplate.Verification("https://example.org/verify-email?token=demo");

        html.Should().Contain("Подтвердить почту")
            .And.Contain("24 часа")
            .And.Contain("Если вы не регистрировались")
            .And.Contain("Ваш Магистр")
            .And.NotContain("Задать новый пароль");
    }

    [Fact]
    public void Password_reset_explains_recovery_without_claiming_password_changed()
    {
        var html = AuthEmailTemplate.PasswordReset("https://example.org/reset-password?token=demo");

        html.Should().Contain("Задать новый пароль")
            .And.Contain("1 час")
            .And.Contain("Ваш пароль останется прежним")
            .And.NotContain("24 часа")
            .And.NotContain("Подтвердить почту");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Encodes_action_and_fallback_links_and_uses_configured_origin(bool verification)
    {
        const string link = "https://preview.example.org/verify-email?token=a%2Bb&hint=\"<test>\"";
        var html = verification ? AuthEmailTemplate.Verification(link) : AuthEmailTemplate.PasswordReset(link);

        const string encoded = "https://preview.example.org/verify-email?token=a%2Bb&amp;hint=&quot;&lt;test&gt;&quot;";
        html.Split($"href=\"{encoded}\"").Length.Should().Be(3, "the button and fallback must use the same safe URL");
        html.Should().Contain($">{encoded}</a>")
            .And.Contain("href=\"https://preview.example.org\"")
            .And.NotContain("hint=\"<test>\"")
            .And.NotContain("alex-taro.ru");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/verify-email?token=demo")]
    public void Rejects_non_web_or_relative_action_links(string link)
    {
        var render = () => AuthEmailTemplate.Verification(link);

        render.Should().Throw<ArgumentException>();
    }
}
