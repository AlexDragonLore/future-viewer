using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Host.Auth;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminReadingEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Anonymous_cannot_read_admin_messages()
    {
        var response = await fixture.CreateClient().GetAsync("/api/admin/readings");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Non_admin_cannot_read_admin_messages()
    {
        var (client, _) = await CreateClient();
        var response = await client.GetAsync("/api/admin/readings");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_reads_full_saved_content_with_search_owner_filter_and_hidden_status()
    {
        var (admin, _) = await CreateClient(admin: true);
        var (_, owner) = await CreateClient();
        var (_, other) = await CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var visible = Reading(owner.UserId, $"{marker} Вопрос про работу 50%_", "Полный ответ ИИ\nВторая строка ответа");
        var hidden = Reading(owner.UserId, $"{marker} Скрытый вопрос", "Сохранённый ответ на скрытый вопрос");
        hidden.DeletedFromHistoryAt = DateTime.UtcNow;
        var foreign = Reading(other.UserId, $"{marker} Другой пользователь", "Другой ответ");
        var unsaved = Reading(owner.UserId, $"{marker} Несохранённая запись", null);
        unsaved.SavedToHistory = false;
        var guest = Reading(null, $"{marker} Гостевая запись", "Ответ гостю");
        guest.SavedToHistory = false;
        await Seed(visible, hidden, foreign, unsaved, guest);

        var response = await admin.GetAsync($"/api/admin/readings?search={marker.ToUpperInvariant()}");
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var all = (await response.Content.ReadFromJsonAsync<AdminReadingListResult>())!;
        all.Total.Should().Be(4);
        all.Items.Select(r => r.Id).Should().BeEquivalentTo([visible.Id, hidden.Id, foreign.Id, guest.Id]);
        var item = all.Items.Single(r => r.Id == visible.Id);
        item.UserId.Should().Be(owner.UserId);
        item.UserEmail.Should().Be(owner.Email);
        item.Question.Should().Be(visible.Question);
        item.Interpretation.Should().Be(visible.AiInterpretation);
        item.DeletedFromHistoryAt.Should().BeNull();
        item.ExpiresAt.Should().BeNull();
        var guestItem = all.Items.Single(r => r.Id == guest.Id);
        guestItem.UserId.Should().BeNull();
        guestItem.UserEmail.Should().BeNull();
        guestItem.Interpretation.Should().Be(guest.AiInterpretation);
        guestItem.ExpiresAt.Should().BeCloseTo(guest.CreatedAt.AddHours(24), TimeSpan.FromMicroseconds(1));
        all.Items.Single(r => r.Id == hidden.Id).DeletedFromHistoryAt.Should().NotBeNull();

        var owned = await Read(admin, $"userId={owner.UserId}&search={marker}");
        owned.Total.Should().Be(2);
        owned.Items.Should().OnlyContain(r => r.UserId == owner.UserId);
        var byEmail = await Read(admin, $"search={Uri.EscapeDataString(owner.Email.ToUpperInvariant())}");
        byEmail.Items.Select(r => r.Id).Should().BeEquivalentTo([visible.Id, hidden.Id]);
        var literalWildcard = await Read(admin, $"userId={owner.UserId}&search={Uri.EscapeDataString("50%_")}");
        literalWildcard.Items.Should().ContainSingle().Which.Id.Should().Be(visible.Id);
        (await Read(admin, $"userId={owner.UserId}&search=no-match-{marker}")).Total.Should().Be(0);

        var detailResponse = await admin.GetAsync($"/api/admin/users/{owner.UserId}");
        detailResponse.EnsureSuccessStatusCode();
        detailResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        var detail = (await detailResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>())!;
        detail.RecentReadings.Select(r => r.Id).Should().BeEquivalentTo([visible.Id, hidden.Id]);
        detail.RecentReadings.Single(r => r.Id == visible.Id).Interpretation.Should().Be(visible.AiInterpretation);
        detail.RecentReadings.Single(r => r.Id == hidden.Id).DeletedFromHistoryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Admin_reads_actual_guest_content_and_claimed_reading_becomes_a_user_message()
    {
        var (admin, _) = await CreateClient(admin: true);
        var marker = Guid.NewGuid().ToString("N");
        var question = $"На что обратить внимание в работе {marker}?";
        var response = await fixture.CreateClient().PostAsJsonAsync("/api/readings/guest", new CreateReadingRequest
        {
            SpreadType = SpreadType.SingleCard, Question = question
        });
        response.EnsureSuccessStatusCode();
        var guest = (await response.Content.ReadFromJsonAsync<GuestReadingResponse>())!;
        var message = (await Read(admin, $"search={marker}")).Items.Should().ContainSingle().Which;
        message.UserId.Should().BeNull();
        message.Question.Should().Be(question);
        message.Interpretation.Should().NotBeNullOrWhiteSpace();
        message.Interpretation!.Length.Should().BeGreaterThan(guest.Reading.Interpretation!.Length);
        message.ExpiresAt.Should().BeCloseTo(guest.ExpiresAt.UtcDateTime, TimeSpan.FromMicroseconds(1));

        var (ownerClient, owner) = await CreateClient();
        (await ownerClient.PostAsJsonAsync("/api/readings/guest/unlock", new GuestReadingTicketRequest(guest.Ticket)))
            .EnsureSuccessStatusCode();
        var claimed = (await Read(admin, $"search={marker}")).Items.Should().ContainSingle().Which;
        claimed.Id.Should().Be(message.Id);
        claimed.UserId.Should().Be(owner.UserId);
        claimed.UserEmail.Should().Be(owner.Email);
        claimed.Question.Should().Be(message.Question);
        claimed.Interpretation.Should().Be(message.Interpretation);
        claimed.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task Admin_excludes_expired_guests_and_legacy_empty_rows_before_cleanup()
    {
        var (admin, _) = await CreateClient(admin: true);
        var marker = Guid.NewGuid().ToString("N");
        var fresh = Reading(null, $"{marker} Анонимный вопрос", "Свежий ответ");
        fresh.SavedToHistory = false;
        var expired = new Reading
        {
            SpreadType = SpreadType.SingleCard, Question = $"{marker} Просроченный вопрос",
            AiInterpretation = "Просроченный ответ", CreatedAt = DateTime.UtcNow.AddHours(-24)
        };
        var legacy = Reading(null, string.Empty, null);
        legacy.SavedToHistory = false;
        await Seed(fresh, expired, legacy);

        var result = await Read(admin, $"search={marker}&pageSize=1");
        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(fresh.Id);
        (await Read(admin, $"search={marker}&page=2&pageSize=1")).Items.Should().BeEmpty();
        (await Read(admin, "")).Items.Should().NotContain(r => r.Id == legacy.Id || r.Id == expired.Id);
    }

    [Fact]
    public async Task Admin_messages_page_by_newest_date_and_stable_id_and_clamp_invalid_page_values()
    {
        var (admin, _) = await CreateClient(admin: true);
        var (_, owner) = await CreateClient();
        var now = DateTime.UtcNow;
        var oldest = new Reading
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            UserId = owner.UserId, SavedToHistory = true, SpreadType = SpreadType.SingleCard,
            Question = "Старый вопрос", CreatedAt = now.AddDays(-1)
        };
        var firstAtSameTime = new Reading
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            UserId = owner.UserId, SavedToHistory = true, SpreadType = SpreadType.SingleCard,
            Question = "Новый вопрос 1", CreatedAt = now
        };
        var secondAtSameTime = new Reading
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            UserId = owner.UserId, SavedToHistory = true, SpreadType = SpreadType.SingleCard,
            Question = "Новый вопрос 2", CreatedAt = now
        };
        await Seed(oldest, firstAtSameTime, secondAtSameTime);

        var expected = new[] { secondAtSameTime.Id, firstAtSameTime.Id, oldest.Id };
        for (var page = 1; page <= expected.Length; page++)
        {
            var result = await Read(admin, $"userId={owner.UserId}&page={page}&pageSize=1");
            result.Total.Should().Be(3);
            result.Items.Should().ContainSingle().Which.Id.Should().Be(expected[page - 1]);
        }
        var normalized = await Read(admin, $"userId={owner.UserId}&page=0&pageSize=0");
        normalized.Items.Should().ContainSingle().Which.Id.Should().Be(expected[0]);
        var beyondLast = await Read(admin, $"userId={owner.UserId}&page={int.MaxValue}&pageSize=100");
        beyondLast.Total.Should().Be(3);
        beyondLast.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_messages_default_to_twenty_and_never_return_more_than_one_hundred()
    {
        var (admin, _) = await CreateClient(admin: true);
        var (_, owner) = await CreateClient();
        await Seed(Enumerable.Range(1, 101).Select(i => Reading(owner.UserId, $"Вопрос {i}", "Ответ")).ToArray());

        var defaults = await Read(admin, $"userId={owner.UserId}");
        defaults.Total.Should().Be(101);
        defaults.Items.Should().HaveCount(20);
        var capped = await Read(admin, $"userId={owner.UserId}&pageSize=1000");
        capped.Total.Should().Be(101);
        capped.Items.Should().HaveCount(100);
    }

    private static Reading Reading(Guid? userId, string question, string? interpretation) => new()
    {
        UserId = userId,
        SpreadType = SpreadType.SingleCard,
        Question = question,
        AiInterpretation = interpretation,
        SavedToHistory = true
    };

    private async Task Seed(params Reading[] readings)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Readings.AddRange(readings);
        await db.SaveChangesAsync();
    }

    private static async Task<AdminReadingListResult> Read(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/admin/readings?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminReadingListResult>())!;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateClient(bool admin = false)
    {
        var client = fixture.CreateClient();
        var email = $"messages-{Guid.NewGuid():N}@example.com";
        var auth = await fixture.RegisterAndLoginAsync(client, email, "password123");
        if (admin)
        {
            using var scope = fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = (await users.GetByIdAsync(auth.UserId))!;
            user.IsAdmin = true;
            await users.UpdateAsync(user);
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "password123" });
            response.EnsureSuccessStatusCode();
            auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }
}
