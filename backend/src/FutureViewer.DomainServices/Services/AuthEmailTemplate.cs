using System.Net;

namespace FutureViewer.DomainServices.Services;

/// <summary>Transactional email markup shared by registration and password recovery.</summary>
public static class AuthEmailTemplate
{
    public static string Verification(string link) => Render(
        link,
        "Подтвердите почту и откройте свою первую карту. Ссылка действует 24 часа.",
        "Знакомство начинается",
        "Один шаг —<br>и вы с нами",
        "Добро пожаловать в «Вуаль Грядущего». Подтвердите свою почту, чтобы завершить регистрацию и открыть личный кабинет.",
        "Подтвердить почту",
        "Ссылка действует 24 часа",
        "Если вы не регистрировались на сайте, просто проигнорируйте это письмо.");

    public static string PasswordReset(string link) => Render(
        link,
        "Создайте новый пароль для «Вуали Грядущего». Ссылка действует 1 час.",
        "Восстановление пароля",
        "Вернём вам<br>доступ",
        "Мы получили запрос на восстановление доступа к вашему аккаунту в «Вуали Грядущего». Нажмите на кнопку, чтобы задать новый пароль.",
        "Задать новый пароль",
        "Ссылка действует 1 час",
        "Если вы не запрашивали восстановление, просто проигнорируйте это письмо. Ваш пароль останется прежним.");

    private static string Render(
        string link, string preheader, string eyebrow, string heading,
        string description, string action, string expiry, string notice)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("An absolute HTTP(S) email action link is required.", nameof(link));

        var safeLink = WebUtility.HtmlEncode(link);
        var homeLink = WebUtility.HtmlEncode(uri.GetLeftPart(UriPartial.Authority));
        var domain = WebUtility.HtmlEncode(uri.Authority);

        // All copy above is static. Only the link and its origin come from configuration.
        // Inline styles and table layout keep the email usable when clients strip <style>.
        return $$"""
            <!doctype html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="light">
              <title>{{action}} · Вуаль Грядущего</title>
              <style>
                @media only screen and (max-width: 480px) {
                  .email-outer { padding: 16px 8px !important; }
                  .email-section { padding-left: 24px !important; padding-right: 24px !important; }
                  .email-heading { font-size: 34px !important; line-height: 39px !important; }
                }
              </style>
            </head>
            <body style="margin:0;padding:0;background-color:#f3f0f8;color:#30283e;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%;">
              <div style="display:none;font-size:1px;line-height:1px;color:#f3f0f8;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;">{{preheader}}</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#f3f0f8" style="width:100%;background-color:#f3f0f8;">
                <tr>
                  <td class="email-outer" align="center" style="padding:32px 16px;">
                    <!--[if mso]><table role="presentation" width="560" cellpadding="0" cellspacing="0" border="0"><tr><td><![endif]-->
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#ffffff" style="width:100%;max-width:560px;table-layout:fixed;background-color:#ffffff;border:1px solid #e7e0f0;border-radius:20px;border-spacing:0;">
                      <tr>
                        <td class="email-section" bgcolor="#27183f" style="padding:30px 40px 34px;background-color:#27183f;border-radius:19px 19px 0 0;">
                          <p style="margin:0 0 28px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px;letter-spacing:2px;color:#eee5ff;"><span aria-hidden="true" style="color:#e7c98b;font-size:18px;">✦</span>&nbsp; ВУАЛЬ ГРЯДУЩЕГО</p>
                          <p style="margin:0 0 12px;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;letter-spacing:1px;color:#dfc38e;">{{eyebrow}}</p>
                          <h1 class="email-heading" style="margin:0;font-family:Georgia,'Times New Roman',serif;font-size:40px;line-height:45px;font-weight:normal;color:#ffffff;">{{heading}}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td class="email-section" style="padding:30px 40px 32px;">
                          <p style="margin:0 0 12px;font-size:17px;line-height:26px;font-weight:bold;color:#30283e;">Здравствуйте!</p>
                          <p style="margin:0 0 26px;font-size:16px;line-height:26px;color:#544a62;">{{description}}</p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width:100%;">
                            <tr>
                              <td align="center" bgcolor="#7547b7" style="background-color:#7547b7;border-radius:10px;mso-padding-alt:17px 20px;">
                                <a href="{{safeLink}}" style="display:block;padding:17px 20px;border:1px solid #7547b7;border-radius:10px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:22px;font-weight:bold;color:#ffffff;text-decoration:none;text-align:center;">{{action}}</a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:13px 0 26px;text-align:center;font-size:13px;line-height:20px;color:#776a88;">{{expiry}}</p>
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="width:100%;">
                            <tr>
                              <td style="padding:20px 0 0;border-top:1px solid #ece6f2;">
                                <p style="margin:0;font-size:13px;line-height:21px;color:#776a88;">{{notice}}</p>
                                <p style="margin:24px 0 3px;font-family:Georgia,'Times New Roman',serif;font-size:22px;line-height:28px;color:#3c2759;">Ваш Магистр <span aria-hidden="true" style="font-size:16px;color:#ad8744;">✦</span></p>
                                <p style="margin:0;font-size:12px;line-height:20px;letter-spacing:0.5px;color:#8b7b9c;">Вуаль Грядущего</p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td class="email-section" bgcolor="#faf8fd" style="padding:20px 40px 24px;background-color:#faf8fd;border-top:1px solid #ece6f2;border-radius:0 0 19px 19px;">
                          <p style="margin:0 0 7px;font-size:12px;line-height:19px;color:#81728f;">Если кнопка не открывается, скопируйте ссылку в браузер:</p>
                          <p style="margin:0;word-break:break-all;overflow-wrap:anywhere;font-size:12px;line-height:19px;"><a href="{{safeLink}}" style="color:#7547b7;text-decoration:underline;word-break:break-all;overflow-wrap:anywhere;">{{safeLink}}</a></p>
                        </td>
                      </tr>
                    </table>
                    <!--[if mso]></td></tr></table><![endif]-->
                    <p style="margin:20px 0 0;font-size:11px;line-height:18px;color:#8b7d99;">Сервисное письмо от <a href="{{homeLink}}" style="color:#756486;text-decoration:underline;">{{domain}}</a><br>Отвечать на это письмо не нужно.</p>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
