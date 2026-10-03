using FluentAssertions;
using FutureViewer.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class EmailLinkBuilderTests
{
    [Fact]
    public void One_time_tokens_are_placed_in_url_fragments_not_query_strings()
    {
        var links = new EmailLinkBuilder(Options.Create(new EmailOptions
        {
            FrontendUrl = "https://alex-taro.ru"
        }));

        links.BuildVerificationLink("a+b").Should().Be("https://alex-taro.ru/verify-email#token=a%2Bb");
        links.BuildPasswordResetLink("a+b").Should().Be("https://alex-taro.ru/reset-password#token=a%2Bb");
    }
}
