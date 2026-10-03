using System.Net;
using System.Text;
using FluentAssertions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.DependencyInjection;
using FutureViewer.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

/// <summary>
/// Exercises the HTTPS protocol using minimal fixtures derived from Roundcube 1.6's
/// login_form, compose_header_from, compose_body, and get_js_commands implementations.
/// These tests do not connect to REG.RU or require the database integration fixture.
/// </summary>
public sealed class RegruWebmailEmailSenderTests
{
    private const string SenderAddress = "no-reply@example.test";
    private const string RecipientAddress = "recipient@example.test";
    private const string Password = "test-password-not-for-logs";
    private const string Subject = "Подтвердите почту & продолжайте";
    private const string Body = "<p>Ссылка: <a href=\"https://example.test/verify?token=private-token&amp;a=1\">Подтвердить</a></p>";

    [Theory]
    [InlineData("", Password, SenderAddress)]
    [InlineData(SenderAddress, "", SenderAddress)]
    [InlineData(SenderAddress, "   ", SenderAddress)]
    [InlineData("not-an-email", Password, SenderAddress)]
    [InlineData(SenderAddress, Password, "different@example.test")]
    public void Constructor_rejects_invalid_explicit_transport_instead_of_disabling_verification(
        string username, string password, string from)
    {
        // AuthService requires real delivery for email ownership verification.
        // An explicitly selected transport must fail closed at construction.
        var options = Options.Create(new EmailOptions
        {
            Transport = "RegruWebmail", Username = username, Password = password, From = from
        });

        Assert.Throws<InvalidOperationException>(() => new RegruWebmailEmailSender(options));
    }

    [Fact]
    public void IsConfigured_is_true_for_valid_https_mailbox_without_an_smtp_host()
    {
        CreateSender(() => new WebmailHandler()).IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void AddInfrastructure_rejects_incomplete_explicit_mail_transport_before_startup()
    {
        var services = new ServiceCollection();
        var configuration = EmailConfiguration("RegruWebmail", password: "");

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
    }

    [Fact]
    public void AddInfrastructure_rejects_unknown_transport_instead_of_falling_back_to_smtp()
    {
        var services = new ServiceCollection();
        var configuration = EmailConfiguration("UnknownMailTransport");

        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
    }

    [Fact]
    public void AddInfrastructure_resolves_configured_https_sender_without_custom_handler_registration()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(EmailConfiguration("RegruWebmail"));
        using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<IEmailSender>();

        sender.Should().BeOfType<RegruWebmailEmailSender>();
        sender.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_submits_unicode_html_with_authenticated_token_and_matching_identity()
    {
        var handler = new WebmailHandler();
        var factoryCalls = 0;
        var sender = CreateSender(() => { factoryCalls++; return handler; });

        await sender.SendAsync(RecipientAddress, Subject, Body);

        factoryCalls.Should().Be(1);
        var login = handler.Requests.Single(r => r.Form.GetValueOrDefault("_action") == "login");
        login.Form.Should().Contain("_user", SenderAddress)
            .And.Contain("_pass", Password)
            .And.Contain("_token", "login-token");
        var send = handler.SendRequests.Should().ContainSingle().Subject;
        send.Uri.Scheme.Should().Be("https");
        send.Uri.Host.Should().Be("webmail.hosting.reg.ru");
        ParseForm(send.Uri.Query.TrimStart('?')).GetValueOrDefault("_action").Should().NotBe("compose");
        send.Form.Should().Contain("_task", "mail")
            .And.Contain("_action", "send")
            .And.Contain("_token", "authenticated-token")
            .And.Contain("_id", "123456789abcdef")
            .And.Contain("_from", "42")
            .And.Contain("_to", RecipientAddress)
            .And.Contain("_subject", Subject)
            .And.Contain("_message", Body)
            .And.Contain("_is_html", "1");
        send.Form.GetValueOrDefault("_draft").Should().BeNullOrEmpty();
        send.Form.Should().NotContainKey("_saveonly");
        ParseForm(send.Uri.Query.TrimStart('?')).Should().NotContainKey("_saveonly");
        handler.Requests.Should().OnlyContain(r => r.Uri.Host == "webmail.hosting.reg.ru" && r.Uri.Scheme == "https");
        handler.Requests.Where(r => r.Method == HttpMethod.Get).Should().OnlyContain(r => r.Form.Count == 0);
    }

    [Fact]
    public async Task SendAsync_accepts_success_even_when_saving_sent_copy_failed_without_resending()
    {
        var handler = new WebmailHandler { SendResponse = SentButSaveFailedHtml };

        await CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body);

        handler.SendRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_uses_a_fresh_session_for_each_message()
    {
        var sessions = new List<WebmailHandler>();
        var sender = CreateSender(() =>
        {
            var handler = new WebmailHandler();
            sessions.Add(handler);
            return handler;
        });

        await sender.SendAsync(RecipientAddress, Subject, Body);
        await sender.SendAsync(RecipientAddress, Subject, Body);

        sessions.Should().HaveCount(2);
        foreach (var session in sessions)
        {
            session.Requests.Should().ContainSingle(r => r.Form.GetValueOrDefault("_action") == "login");
            session.SendRequests.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task SendAsync_rejects_login_failure_returned_as_http_200()
    {
        var handler = new WebmailHandler { LoginRejected = true };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.SendRequests.Should().BeEmpty();
        exception.Message.Should().NotContain(Password);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_rejects_expired_session_without_sending_again(bool expireAtCompose)
    {
        var handler = new WebmailHandler
        {
            ComposeResponse = expireAtCompose ? SessionExpiredHtml : ComposeHtml,
            SendResponse = expireAtCompose ? SuccessHtml : SessionExpiredHtml
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.SendRequests.Should().HaveCount(expireAtCompose ? 0 : 1);
    }

    [Fact]
    public async Task SendAsync_rejects_mailbox_when_no_identity_matches_configured_from()
    {
        var handler = new WebmailHandler
        {
            ComposeResponse = ComposeHtml.Replace(SenderAddress, "unrelated@example.test", StringComparison.Ordinal)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.SendRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_does_not_post_credentials_when_login_csrf_token_is_missing()
    {
        var handler = new WebmailHandler
        {
            LoginResponse = LoginHtml
                .Replace("\"request_token\":\"login-token\",", "", StringComparison.Ordinal)
                .Replace("<input type=\"hidden\" name=\"_token\" value=\"login-token\">", "", StringComparison.Ordinal)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.Requests.Should().OnlyContain(r => r.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task SendAsync_surfaces_localized_smtp_failure_without_leaking_message_or_credentials()
    {
        var handler = new WebmailHandler { SendResponse = SmtpErrorHtml };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.SendRequests.Should().ContainSingle();
        exception.Message.Should().NotContain(Password)
            .And.NotContain(RecipientAddress)
            .And.NotContain("private-token")
            .And.NotContain(Subject)
            .And.NotContain("Ошибка SMTP");
    }

    [Theory]
    [InlineData("https://attacker.example/collect")]
    [InlineData("http://webmail.hosting.reg.ru/collect")]
    [InlineData("https://webmail.hosting.reg.ru:8443/collect")]
    [InlineData("https://webmail.hosting.reg.ru.attacker.example/collect")]
    public async Task SendAsync_never_follows_redirect_outside_the_fixed_https_origin(string location)
    {
        var handler = new WebmailHandler { LoginRedirect = new Uri(location) };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.Requests.Should().OnlyContain(r =>
            r.Uri.Scheme == "https" && r.Uri.Host == "webmail.hosting.reg.ru" && r.Uri.Port == 443);
        handler.SendRequests.Should().BeEmpty();
        handler.Requests.Should().ContainSingle(r => r.Form.ContainsKey("_pass"));
    }

    [Fact]
    public async Task SendAsync_does_not_repeat_a_send_whose_response_was_lost()
    {
        var handler = new WebmailHandler { ThrowOnSend = new TaskCanceledException("Simulated send timeout") };

        var exception = await Record.ExceptionAsync(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        exception.Should().NotBeNull();
        handler.SendRequests.Should().ContainSingle();
        handler.Requests.Should().ContainSingle(r => r.Form.GetValueOrDefault("_action") == "login");
    }

    [Theory]
    [InlineData("<html><body>Service temporarily unavailable</body></html>")]
    [InlineData("<html><body>parent.rcmail.sent_successfully is only a help label</body></html>")]
    [InlineData("<script>var message = 'parent.rcmail.sent_successfully(\"confirmation\",\"fake\",[],false)';</script>")]
    [InlineData("<script>// parent.rcmail.sent_successfully(\"confirmation\",\"fake\",[],false);\n</script>")]
    [InlineData("<script>/* parent.rcmail.sent_successfully(\"confirmation\",\"fake\",[],false); */</script>")]
    public async Task SendAsync_does_not_treat_unrecognized_http_200_response_as_success(string response)
    {
        var handler = new WebmailHandler { SendResponse = response };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSender(() => handler).SendAsync(RecipientAddress, Subject, Body));

        handler.SendRequests.Should().ContainSingle();
    }

    private static RegruWebmailEmailSender CreateSender(Func<HttpMessageHandler> factory) => new(
        Options.Create(new EmailOptions
        {
            Transport = "RegruWebmail",
            Username = SenderAddress,
            Password = Password,
            From = SenderAddress
        }), factory);

    private static IConfiguration EmailConfiguration(string transport, string password = Password) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=unused_webmail_tests;Username=unused;Password=unused",
            ["Email:Transport"] = transport,
            ["Email:Username"] = SenderAddress,
            ["Email:Password"] = password,
            ["Email:From"] = SenderAddress
        }).Build();

    private sealed record RequestSnapshot(HttpMethod Method, Uri Uri, Dictionary<string, string> Form);

    private sealed class WebmailHandler : HttpMessageHandler
    {
        private bool _authenticated;
        public List<RequestSnapshot> Requests { get; } = [];
        public IEnumerable<RequestSnapshot> SendRequests => Requests.Where(r =>
            r.Method == HttpMethod.Post && r.Form.GetValueOrDefault("_action") == "send");
        public string LoginResponse { get; init; } = LoginHtml;
        public string ComposeResponse { get; init; } = ComposeHtml;
        public string SendResponse { get; init; } = SuccessHtml;
        public bool LoginRejected { get; init; }
        public Uri LoginRedirect { get; init; } = new("https://webmail.hosting.reg.ru/?_task=mail");
        public Exception? ThrowOnSend { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var form = request.Content is null
                ? new Dictionary<string, string>()
                : ParseForm(await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add(new RequestSnapshot(request.Method, uri, form));
            var query = ParseForm(uri.Query.TrimStart('?'));

            if (request.Method == HttpMethod.Post && form.GetValueOrDefault("_action") == "login")
            {
                if (LoginRejected) return Html(LoginHtml);
                _authenticated = true;
                var response = Redirect(LoginRedirect);
                response.Headers.TryAddWithoutValidation("Set-Cookie", "roundcube_sessid=authenticated-fixture; Path=/; Secure; HttpOnly");
                return response;
            }

            if (request.Method == HttpMethod.Post && form.GetValueOrDefault("_action") == "send")
            {
                if (ThrowOnSend is not null) throw ThrowOnSend;
                return Html(SendResponse);
            }

            if (request.Method == HttpMethod.Get)
            {
                if (!_authenticated)
                {
                    var response = Html(LoginResponse);
                    response.Headers.TryAddWithoutValidation("Set-Cookie", "roundcube_sessid=login-fixture; Path=/; Secure; HttpOnly");
                    return response;
                }
                if (query.GetValueOrDefault("_action") == "compose")
                {
                    return query.ContainsKey("_id")
                        ? Html(ComposeResponse)
                        : Redirect(new Uri("./?_task=mail&_action=compose&_id=123456789abcdef", UriKind.Relative));
                }
                return Html(AuthenticatedHtml);
            }

            throw new InvalidOperationException("Unexpected fake webmail request.");
        }

        private static HttpResponseMessage Html(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/html")
        };

        private static HttpResponseMessage Redirect(Uri location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = location;
            return response;
        }
    }

    private static Dictionary<string, string> ParseForm(string encoded) => encoded
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2))
        .ToDictionary(pair => WebUtility.UrlDecode(pair[0]),
            pair => WebUtility.UrlDecode(pair.Length > 1 ? pair[1] : ""), StringComparer.Ordinal);

    private const string LoginHtml = """
        <!DOCTYPE html><html><head><script>
        var rcmail = new rcube_webmail();
        rcmail.set_env({"task":"login","action":"","request_token":"login-token","comm_path":"./?_task=login"});
        </script></head><body>
        <form id="login-form" name="login-form" method="post" action="./?_task=login">
        <input type="hidden" name="_token" value="login-token">
        <input type="hidden" name="_task" value="login"><input type="hidden" name="_action" value="login">
        <input type="hidden" name="_timezone" value="_default_"><input type="hidden" name="_url" value="">
        <input name="_user" id="rcmloginuser" type="text"><input name="_pass" id="rcmloginpwd" type="password">
        </form></body></html>
        """;

    private const string AuthenticatedHtml = """
        <!DOCTYPE html><html><head><script>
        rcmail.set_env({"task":"mail","action":"","request_token":"authenticated-token","comm_path":"./?_task=mail"});
        </script></head><body>Mailbox</body></html>
        """;

    private const string ComposeHtml = """
        <!DOCTYPE html><html><head><script>
        rcmail.set_env({"task":"mail","action":"compose","compose_id":"123456789abcdef","request_token":"authenticated-token","identities":{"17":{"email":"other@example.test"},"42":{"email":"no-reply@example.test"}},"comm_path":"./?_task=mail"});
        </script></head><body><form name="form" method="post" action="./?_task=mail">
        <input type="hidden" name="_token" value="authenticated-token">
        <input type="hidden" name="_task" value="mail"><input type="hidden" name="_action" value="send"><input type="hidden" name="_id" value="123456789abcdef"><input type="hidden" name="_attachments" value="">
        <select name="_from" id="_from"><option value="17" selected="selected">Other &lt;other@example.test&gt;</option><option value="42">Future Viewer &lt;no-reply@example.test&gt;</option></select>
        <textarea name="_to"></textarea><textarea name="_cc"></textarea><textarea name="_bcc"></textarea><input name="_subject" type="text">
        <input type="hidden" name="_draft" value=""><input type="hidden" name="_draft_saveid" value=""><input type="hidden" name="_is_html" value="1"><input type="hidden" name="_framed" value="1">
        <textarea name="_message"></textarea></form></body></html>
        """;

    private const string SuccessHtml = """
        <!DOCTYPE html><html><head><script>
        if (window.parent && parent.rcmail) {
          parent.rcmail.iframe_loaded(0);
          parent.rcmail.remove_compose_data("123456789abcdef");
          parent.rcmail.sent_successfully("confirmation","Сообщение отправлено.",["Sent"],false);
        }
        </script></head><body></body></html>
        """;

    private const string SentButSaveFailedHtml = """
        <!DOCTYPE html><html><head><script>
        if (window.parent && parent.rcmail) {
          parent.rcmail.iframe_loaded(0);
          parent.rcmail.display_message("Не удалось сохранить копию сообщения.","error",0);
          parent.rcmail.sent_successfully("confirmation","Сообщение отправлено.",[],true);
        }
        </script></head><body></body></html>
        """;

    private const string SessionExpiredHtml = """
        <!DOCTYPE html><html><head><script>
        if (window.parent && parent.rcmail) {
          parent.rcmail.iframe_loaded(0);
          parent.rcmail.display_message("Сессия недействительна или истекла.","error",-1000);
          parent.rcmail.session_error("./?_task=mail&_err=session");
        }
        </script></head><body></body></html>
        """;

    private const string SmtpErrorHtml = """
        <!DOCTYPE html><html><head><script>
        if (window.parent && parent.rcmail) {
          parent.rcmail.iframe_loaded(0);
          parent.rcmail.display_message("Ошибка SMTP (535): авторизация не удалась.","error",0);
        }
        </script></head><body></body></html>
        """;
}
