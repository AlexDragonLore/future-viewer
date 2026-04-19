using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class AchievementServiceTests
{
    private static User NewUser(long? telegramChatId = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = "a@b.c",
        PasswordHash = "x",
        TelegramChatId = telegramChatId
    };

    private static Achievement A(string code, int sort = 0) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        NameRu = code,
        DescriptionRu = code,
        IconPath = $"/icons/{code}.svg",
        SortOrder = sort
    };

    private static IReadOnlyList<Achievement> AllAchievements() => new[]
    {
        A(AchievementService.Codes.FirstReading),
        A(AchievementService.Codes.FirstFeedback),
        A(AchievementService.Codes.TelegramLinked),
        A(AchievementService.Codes.Streak3),
        A(AchievementService.Codes.Streak7),
        A(AchievementService.Codes.Streak30),
        A(AchievementService.Codes.Total10),
        A(AchievementService.Codes.Total50),
        A(AchievementService.Codes.Total100),
        A(AchievementService.Codes.ScoreMaster),
        A(AchievementService.Codes.Perfect10),
        A(AchievementService.Codes.HighFive)
    };

    private sealed class Harness
    {
        public Mock<IAchievementRepository> Achievements { get; } = new();
        public Mock<IReadingRepository> Readings { get; } = new();
        public Mock<IFeedbackRepository> Feedbacks { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public List<UserAchievement> Granted { get; } = new();
        public List<UserAchievement> Existing { get; set; } = new();
        public IReadOnlyList<Achievement> All { get; } = AllAchievements();

        public AchievementService Build(User user, int readingCount, IReadOnlyList<ReadingFeedback> scoredFeedbacks, IReadOnlyList<DateTime>? dates = null)
        {
            Users.Setup(u => u.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            Readings.Setup(r => r.CountByUserAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(readingCount);
            Readings.Setup(r => r.GetDistinctReadingDatesAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dates ?? Array.Empty<DateTime>());
            Feedbacks.Setup(f => f.GetScoredByUserAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(scoredFeedbacks);

            Achievements.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(All);
            Achievements.Setup(a => a.GetByUserAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Existing);
            Achievements.Setup(a => a.GrantAsync(It.IsAny<UserAchievement>(), It.IsAny<CancellationToken>()))
                .Callback((UserAchievement ua, CancellationToken _) => Granted.Add(ua))
                .ReturnsAsync((UserAchievement ua, CancellationToken _) => ua);

            return new AchievementService(Achievements.Object, Readings.Object, Feedbacks.Object, Users.Object);
        }
    }

    private static ReadingFeedback ScoredFeedback(Guid userId, int score) => new()
    {
        ReadingId = Guid.NewGuid(),
        UserId = userId,
        Token = Guid.NewGuid().ToString("N"),
        ScheduledAt = DateTime.UtcNow,
        AiScore = score,
        IsSincere = true,
        Status = FeedbackStatus.Scored
    };

    [Fact]
    public async Task CheckAndGrantAsync_grants_first_reading_on_first_reading()
    {
        var harness = new Harness();
        var user = NewUser();
        var sut = harness.Build(user, readingCount: 1, scoredFeedbacks: Array.Empty<ReadingFeedback>());

        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Select(g => g.Code).Should().Contain(AchievementService.Codes.FirstReading);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_total_thresholds()
    {
        var harness = new Harness();
        var user = NewUser();
        var sut = harness.Build(user, readingCount: 50, scoredFeedbacks: Array.Empty<ReadingFeedback>());

        var granted = await sut.CheckAndGrantAsync(user.Id);
        var codes = granted.Select(g => g.Code).ToHashSet();

        codes.Should().Contain(AchievementService.Codes.FirstReading);
        codes.Should().Contain(AchievementService.Codes.Total10);
        codes.Should().Contain(AchievementService.Codes.Total50);
        codes.Should().NotContain(AchievementService.Codes.Total100);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_telegram_linked_when_chat_id_set()
    {
        var harness = new Harness();
        var user = NewUser(telegramChatId: 1234);
        var sut = harness.Build(user, readingCount: 0, scoredFeedbacks: Array.Empty<ReadingFeedback>());

        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Select(g => g.Code).Should().Contain(AchievementService.Codes.TelegramLinked);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_perfect_and_highfive_based_on_tens()
    {
        var harness = new Harness();
        var user = NewUser();
        var feedbacks = Enumerable.Range(0, 5).Select(_ => ScoredFeedback(user.Id, 10)).ToList();
        var sut = harness.Build(user, readingCount: 5, scoredFeedbacks: feedbacks);

        var granted = await sut.CheckAndGrantAsync(user.Id);
        var codes = granted.Select(g => g.Code).ToHashSet();

        codes.Should().Contain(AchievementService.Codes.FirstFeedback);
        codes.Should().Contain(AchievementService.Codes.Perfect10);
        codes.Should().Contain(AchievementService.Codes.HighFive);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_score_master_at_10_feedbacks_average_8()
    {
        var harness = new Harness();
        var user = NewUser();
        var feedbacks = Enumerable.Range(0, 10).Select(_ => ScoredFeedback(user.Id, 8)).ToList();
        var sut = harness.Build(user, readingCount: 10, scoredFeedbacks: feedbacks);

        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Select(g => g.Code).Should().Contain(AchievementService.Codes.ScoreMaster);
    }

    [Fact]
    public async Task CheckAndGrantAsync_does_not_grant_score_master_below_threshold()
    {
        var harness = new Harness();
        var user = NewUser();
        var feedbacks = Enumerable.Range(0, 10).Select(_ => ScoredFeedback(user.Id, 7)).ToList();
        var sut = harness.Build(user, readingCount: 10, scoredFeedbacks: feedbacks);

        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Select(g => g.Code).Should().NotContain(AchievementService.Codes.ScoreMaster);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_streak_3_with_three_consecutive_days_ending_today()
    {
        var harness = new Harness();
        var user = NewUser();
        var today = DateTime.UtcNow.Date;
        var dates = new[] { today, today.AddDays(-1), today.AddDays(-2) };
        var sut = harness.Build(user, readingCount: 3, scoredFeedbacks: Array.Empty<ReadingFeedback>(), dates: dates);

        var granted = await sut.CheckAndGrantAsync(user.Id);
        var codes = granted.Select(g => g.Code).ToHashSet();

        codes.Should().Contain(AchievementService.Codes.Streak3);
        codes.Should().NotContain(AchievementService.Codes.Streak7);
    }

    [Fact]
    public async Task CheckAndGrantAsync_no_streak_when_gap_older_than_yesterday()
    {
        var harness = new Harness();
        var user = NewUser();
        var today = DateTime.UtcNow.Date;
        var dates = new[] { today.AddDays(-3), today.AddDays(-4), today.AddDays(-5) };
        var sut = harness.Build(user, readingCount: 3, scoredFeedbacks: Array.Empty<ReadingFeedback>(), dates: dates);

        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Select(g => g.Code).Should().NotContain(AchievementService.Codes.Streak3);
    }

    [Fact]
    public async Task CheckAndGrantAsync_skips_already_granted()
    {
        var harness = new Harness();
        var user = NewUser();

        var firstReading = harness.All.First(a => a.Code == AchievementService.Codes.FirstReading);
        harness.Existing = new List<UserAchievement>
        {
            new()
            {
                UserId = user.Id,
                AchievementId = firstReading.Id
            }
        };

        var sut = harness.Build(user, readingCount: 1, scoredFeedbacks: Array.Empty<ReadingFeedback>());
        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Should().BeEmpty();
        harness.Granted.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllWithUserStatusAsync_marks_unlocked_dates()
    {
        var harness = new Harness();
        var user = NewUser();

        var allList = AllAchievements();
        var firstReading = allList.First(a => a.Code == AchievementService.Codes.FirstReading);
        var unlockedAt = DateTime.UtcNow.AddDays(-1);
        harness.Achievements.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allList);
        harness.Achievements.Setup(a => a.GetByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAchievement>
            {
                new()
                {
                    UserId = user.Id,
                    AchievementId = firstReading.Id,
                    UnlockedAt = unlockedAt
                }
            });

        var sut = new AchievementService(harness.Achievements.Object, harness.Readings.Object, harness.Feedbacks.Object, harness.Users.Object);

        var result = await sut.GetAllWithUserStatusAsync(user.Id);

        result.Should().HaveCount(12);
        result.First(r => r.Code == AchievementService.Codes.FirstReading).UnlockedAt.Should().Be(unlockedAt);
        result.First(r => r.Code == AchievementService.Codes.Total10).UnlockedAt.Should().BeNull();
    }
}
