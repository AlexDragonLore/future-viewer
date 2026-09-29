using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FutureViewer.Infrastructure.Email;

/// <summary>
/// Temporary adapter for REG.RU's Roundcube interface when SMTP egress is unavailable.
/// Each delivery owns its cookies, CSRF token and compose identity. A send is never retried.
/// </summary>
public sealed class RegruWebmailEmailSender : IEmailSender
{
    private static readonly Uri Endpoint = new("https://webmail.hosting.reg.ru/");
    private const int MaximumResponseBytes = 2 * 1024 * 1024;
    private const int MaximumRedirects = 5;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex Forms = new(
        "<form\\b(?<attributes>(?:[^>\"']|\"[^\"]*\"|'[^']*')*)>(?<body>.*?)</form\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout);
    private static readonly Regex Inputs = new(
        "<input\\b(?<attributes>(?:[^>\"']|\"[^\"]*\"|'[^']*')*)>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout);
    private static readonly Regex Attributes = new(
        "(?<name>[^\\s=/>]+)(?:\\s*=\\s*(?:\"(?<quoted>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s\"'=<>`]+)))?",
        RegexOptions.Singleline, RegexTimeout);
    private static readonly Regex Scripts = new(
        "<script\\b[^>]*>(?<body>.*?)</script\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, RegexTimeout);

    private readonly EmailOptions _options;
    private readonly Func<HttpMessageHandler> _handlerFactory;

    public RegruWebmailEmailSender(
        IOptions<EmailOptions> options,
        Func<HttpMessageHandler>? handlerFactory = null)
    {
        _options = options.Value;
        if (_options.GetTransport() != "RegruWebmail" || !_options.IsConfigured)
            throw Failure("mailbox credentials and matching sender are required");
        _handlerFactory = handlerFactory ?? CreateSessionHandler;
    }

    public bool IsConfigured => _options.GetTransport() == "RegruWebmail" && _options.IsConfigured
        ? true : throw Failure("mailbox credentials and matching sender are required");

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw Failure("mailbox credentials and matching sender are required");
        if (to.Contains('\r') || to.Contains('\n') || !MailboxAddress.TryParse(to, out var recipient)
            || !recipient.Address.Contains('@'))
            throw Failure("recipient address is invalid");
        if (subject.Contains('\r') || subject.Contains('\n'))
            throw Failure("subject contains an invalid line break");

        // Do not use pooled handlers: Roundcube cookies must never cross deliveries.
        using var client = new HttpClient(_handlerFactory(), disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FutureViewer-TransactionalEmail/1.0");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            await SendCoreAsync(client, recipient.Address, subject, htmlBody, deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // A timeout after submission is ambiguous. Never retry here or expose a provider body.
            throw Failure("request timed out; delivery is not confirmed");
        }
        catch (HttpRequestException)
        {
            throw Failure("HTTPS request failed; delivery is not confirmed");
        }
        catch (JsonException)
        {
            throw Failure("unexpected webmail response");
        }
        catch (RegexMatchTimeoutException)
        {
            throw Failure("unexpected webmail response");
        }
    }

    private async Task SendCoreAsync(HttpClient client, string to, string subject, string htmlBody, CancellationToken ct)
    {
        var loginPage = await RequestAsync(client, Endpoint, null, followRedirects: true, ct);
        var loginForm = ReadForm(loginPage, "login");
        var loginEnvironment = ReadEnvironment(loginPage.Html);
        var loginToken = MatchingField(loginForm, loginEnvironment, "_token", "request_token");
        var login = new Dictionary<string, string>
        {
            ["_task"] = "login", ["_action"] = "login", ["_token"] = loginToken,
            ["_user"] = _options.Username.Trim(), ["_pass"] = _options.Password,
            ["_timezone"] = "_default_", ["_url"] = ""
        };
        var authenticatedPage = await RequestAsync(client, loginForm.Action, login, followRedirects: true, ct);
        var authenticated = ReadEnvironment(authenticatedPage.Html);
        if (GetString(authenticated, "task") != "mail" || !authenticated.ContainsKey("request_token"))
            throw Failure("authentication was not confirmed");

        var composePage = await RequestAsync(client, new Uri(Endpoint, "?_task=mail&_action=compose"),
            null, followRedirects: true, ct);
        var composeForm = ReadForm(composePage, "send");
        var composeEnvironment = ReadEnvironment(composePage.Html);
        if (GetString(composeEnvironment, "task") != "mail" || GetString(composeEnvironment, "action") != "compose")
            throw Failure("compose session was not confirmed");
        var token = MatchingField(composeForm, composeEnvironment, "_token", "request_token");
        var composeId = MatchingField(composeForm, composeEnvironment, "_id", "compose_id");
        var from = MailboxAddress.Parse(_options.From).Address;
        var identity = FindIdentity(composeEnvironment, from);
        var message = new Dictionary<string, string>
        {
            ["_task"] = "mail", ["_action"] = "send", ["_id"] = composeId, ["_token"] = token,
            ["_from"] = identity, ["_to"] = to, ["_subject"] = subject, ["_message"] = htmlBody,
            ["_charset"] = "UTF-8", ["_is_html"] = "1", ["_framed"] = "1",
            ["_draft"] = "", ["_draft_saveid"] = "", ["_attachments"] = "",
            ["_cc"] = "", ["_bcc"] = "", ["_replyto"] = ""
        };

        // Never follow a send redirect or replay this POST, including after an ambiguous timeout.
        var result = await RequestAsync(client, composeForm.Action, message, followRedirects: false, ct);
        if (!HasSendConfirmation(result.Html))
            throw Failure("delivery was not confirmed by webmail");
    }

    private static HttpMessageHandler CreateSessionHandler() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        // The platform's TLS certificate and hostname validation is intentionally unchanged.
    };

    private static async Task<Page> RequestAsync(HttpClient client, Uri uri,
        IReadOnlyDictionary<string, string>? form, bool followRedirects, CancellationToken ct)
    {
        for (var redirect = 0; ; redirect++)
        {
            EnsureOrigin(uri);
            using var request = new HttpRequestMessage(form is null ? HttpMethod.Get : HttpMethod.Post, uri);
            if (form is not null)
            {
                request.Content = new FormUrlEncodedContent(form);
                request.Headers.Referrer = Endpoint;
                request.Headers.TryAddWithoutValidation("Origin", Endpoint.GetLeftPart(UriPartial.Authority));
            }
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (!followRedirects || redirect >= MaximumRedirects || response.Headers.Location is null)
                    throw Failure("unexpected webmail redirect");
                if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther)
                    && !(form is null && response.StatusCode is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                    throw Failure("unexpected webmail redirect");
                var next = new Uri(uri, response.Headers.Location);
                EnsureOrigin(next);
                uri = next;
                form = null;
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw Failure("webmail returned an unsuccessful HTTP status");
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                throw Failure("webmail response is too large");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, ct)) != 0)
            {
                if (buffer.Length + count > MaximumResponseBytes)
                    throw Failure("webmail response is too large");
                buffer.Write(chunk, 0, count);
            }
            return new Page(uri, Encoding.UTF8.GetString(buffer.ToArray()));
        }
    }

    private static void EnsureOrigin(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443
            || !string.Equals(uri.Host, Endpoint.Host, StringComparison.OrdinalIgnoreCase)
            || uri.UserInfo.Length != 0 || uri.AbsolutePath is not ("/" or "/index.php"))
            throw Failure("webmail redirect or form points outside the fixed HTTPS origin");
    }

    private static Form ReadForm(Page page, string action)
    {
        Form? found = null;
        foreach (Match match in Forms.Matches(page.Html))
        {
            var attributes = ReadAttributes(match.Groups["attributes"].Value);
            var hidden = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match input in Inputs.Matches(match.Groups["body"].Value))
            {
                var inputAttributes = ReadAttributes(input.Groups["attributes"].Value);
                if (!inputAttributes.TryGetValue("name", out var name)
                    || !inputAttributes.TryGetValue("type", out var type)
                    || !string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase)) continue;
                if (!hidden.TryAdd(name, inputAttributes.GetValueOrDefault("value", "")))
                    throw Failure("ambiguous webmail form");
            }
            if (hidden.GetValueOrDefault("_action") != action) continue;
            if (found is not null || !string.Equals(attributes.GetValueOrDefault("method"), "post", StringComparison.OrdinalIgnoreCase)
                || !attributes.TryGetValue("action", out var target) || string.IsNullOrWhiteSpace(target))
                throw Failure("unexpected webmail form");
            var expectedTask = action == "login" ? "login" : "mail";
            if (hidden.GetValueOrDefault("_task") != expectedTask)
                throw Failure("unexpected webmail form task");
            var destination = new Uri(page.Uri, target);
            EnsureOrigin(destination);
            // Roundcube reads some route parameters from GET before POST. A compose or save-only
            // URL here could return HTTP 200 without submitting the intended message.
            var query = ReadQuery(destination);
            if (query.GetValueOrDefault("_task") != expectedTask
                || query.ContainsKey("_saveonly")
                || (query.TryGetValue("_action", out var routeAction) && routeAction != action))
                throw Failure("unexpected webmail form action");
            found = new Form(destination, hidden);
        }
        return found ?? throw Failure("expected webmail form is missing");
    }

    private static Dictionary<string, string> ReadAttributes(string source)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match attribute in Attributes.Matches(source))
        {
            var value = attribute.Groups["quoted"].Success ? attribute.Groups["quoted"].Value
                : attribute.Groups["single"].Success ? attribute.Groups["single"].Value : attribute.Groups["bare"].Value;
            if (!attributes.TryAdd(attribute.Groups["name"].Value, WebUtility.HtmlDecode(value)))
                throw Failure("ambiguous webmail form attributes");
        }
        return attributes;
    }

    private static Dictionary<string, string> ReadQuery(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (!result.TryAdd(WebUtility.UrlDecode(pair[0]), pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : ""))
                throw Failure("ambiguous webmail form route");
        }
        return result;
    }

    private static Dictionary<string, JsonElement> ReadEnvironment(string html)
    {
        var environment = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var args in ReadJsonCalls(html, "rcmail.set_env"))
        {
            if (args.Count != 1 || args[0].ValueKind != JsonValueKind.Object) continue;
            foreach (var property in args[0].EnumerateObject()) environment[property.Name] = property.Value.Clone();
        }
        return environment;
    }

    private static string GetString(Dictionary<string, JsonElement> environment, string key) =>
        environment.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : "";

    private static string MatchingField(Form form, Dictionary<string, JsonElement> environment, string field, string key)
    {
        var value = form.Hidden.GetValueOrDefault(field, "");
        if (string.IsNullOrWhiteSpace(value) || value != GetString(environment, key))
            throw Failure("webmail CSRF token or compose identifier mismatch");
        return value;
    }

    private static string FindIdentity(Dictionary<string, JsonElement> environment, string from)
    {
        if (environment.TryGetValue("identities", out var identities) && identities.ValueKind == JsonValueKind.Object)
        {
            foreach (var identity in identities.EnumerateObject())
                if (identity.Value.ValueKind == JsonValueKind.Object
                    && identity.Value.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String
                    && string.Equals(email.GetString(), from, StringComparison.OrdinalIgnoreCase)) return identity.Name;
        }
        throw Failure("authenticated mailbox has no matching sender identity");
    }

    private static bool HasSendConfirmation(string html)
    {
        if (ReadJsonCalls(html, "parent.rcmail.session_error").Count != 0) return false;
        return ReadJsonCalls(html, "parent.rcmail.sent_successfully").Any(args =>
            args.Count == 4 && args[0].ValueKind == JsonValueKind.String && args[0].GetString() == "confirmation"
            && args[1].ValueKind == JsonValueKind.String && args[2].ValueKind == JsonValueKind.Array
            && args[3].ValueKind is JsonValueKind.True or JsonValueKind.False);
        // save_error=true still confirms submission. Retrying would duplicate the message.
    }

    private static List<List<JsonElement>> ReadJsonCalls(string html, string method)
    {
        var calls = new List<List<JsonElement>>();
        foreach (Match script in Scripts.Matches(html))
        {
            var text = script.Groups["body"].Value;
            for (var index = 0; index < text.Length; index++)
            {
                var c = text[index];
                if (c is '\'' or '"' or '`')
                {
                    var quote = c;
                    while (++index < text.Length)
                    {
                        if (text[index] == '\\') index++;
                        else if (text[index] == quote) break;
                    }
                    continue;
                }
                if (c == '/' && index + 1 < text.Length && text[index + 1] == '/')
                {
                    while (index < text.Length && text[index] != '\n') index++;
                    continue;
                }
                if (c == '/' && index + 1 < text.Length && text[index + 1] == '*')
                {
                    var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    index = end < 0 ? text.Length : end + 1;
                    continue;
                }
                if (!text.AsSpan(index).StartsWith(method, StringComparison.Ordinal)
                    || (index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] is '_' or '$' or '.')))
                    continue;
                var cursor = index + method.Length;
                SkipWhitespace(text, ref cursor);
                if (cursor >= text.Length || text[cursor++] != '(') continue;
                var args = new List<JsonElement>();
                while (true)
                {
                    SkipWhitespace(text, ref cursor);
                    var bytes = Encoding.UTF8.GetBytes(text[cursor..]);
                    var reader = new Utf8JsonReader(bytes);
                    using var document = JsonDocument.ParseValue(ref reader);
                    args.Add(document.RootElement.Clone());
                    cursor += Encoding.UTF8.GetCharCount(bytes.AsSpan(0, (int)reader.BytesConsumed));
                    SkipWhitespace(text, ref cursor);
                    if (cursor < text.Length && text[cursor] == ')')
                    {
                        calls.Add(args);
                        index = cursor;
                        break;
                    }
                    if (args.Count >= 8 || cursor >= text.Length || text[cursor++] != ',')
                        throw Failure("unexpected webmail script response");
                }
            }
        }
        return calls;
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
    }

    private static InvalidOperationException Failure(string reason) => new($"REG.RU webmail: {reason}.");
    private sealed record Page(Uri Uri, string Html);
    private sealed record Form(Uri Action, Dictionary<string, string> Hidden);
}
