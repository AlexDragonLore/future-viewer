using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class AchievementServiceTests
{
    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "a@b.c",
        PasswordHash = "x",
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
        A(AchievementService.Codes.Streak3),
        A(AchievementService.Codes.Streak7),
        A(AchievementService.Codes.Streak30),
        A(AchievementService.Codes.Total10),
        A(AchievementService.Codes.Total50),
        A(AchievementService.Codes.Total100),
    };

    private sealed class Harness
    {
        public Mock<IAchievementRepository> Achievements { get; } = new();
        public Mock<IReadingRepository> Readings { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public List<UserAchievement> Granted { get; } = new();
        public List<UserAchievement> Existing { get; set; } = new();
        public IReadOnlyList<Achievement> All { get; } = AllAchievements();

        public AchievementService Build(User user, int readingCount, IReadOnlyList<DateTime>? dates = null)
        {
            Users.Setup(u => u.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            Readings.Setup(r => r.CountByUserAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(readingCount);
            Readings.Setup(r => r.GetDistinctReadingDatesAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dates ?? Array.Empty<DateTime>());
            Achievements.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(All);
            Achievements.Setup(a => a.GetByUserAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Existing);
            Achievements.Setup(a => a.GrantManyAsync(It.IsAny<IReadOnlyCollection<UserAchievement>>(), It.IsAny<CancellationToken>()))
                .Callback((IReadOnlyCollection<UserAchievement> rows, CancellationToken _) => Granted.AddRange(rows))
                .ReturnsAsync((IReadOnlyCollection<UserAchievement> rows, CancellationToken _) => rows.ToArray());

            return new AchievementService(Achievements.Object, Readings.Object, Users.Object);
        }
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_first_reading_on_first_reading()
    {
        var harness = new Harness();
        var user = NewUser();
        var sut = harness.Build(user, readingCount: 1);

        var granted = await sut.CheckAndGrantAsync(user.Id);

        granted.Select(g => g.Code).Should().Contain(AchievementService.Codes.FirstReading);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_total_thresholds()
    {
        var harness = new Harness();
        var user = NewUser();
        var sut = harness.Build(user, readingCount: 50);

        var granted = await sut.CheckAndGrantAsync(user.Id);
        var codes = granted.Select(g => g.Code).ToHashSet();

        codes.Should().Contain(AchievementService.Codes.FirstReading);
        codes.Should().Contain(AchievementService.Codes.Total10);
        codes.Should().Contain(AchievementService.Codes.Total50);
        codes.Should().NotContain(AchievementService.Codes.Total100);
    }

    [Fact]
    public async Task CheckAndGrantAsync_grants_streak_3_with_three_consecutive_days_ending_today()
    {
        var harness = new Harness();
        var user = NewUser();
        var today = DateTime.UtcNow.Date;
        var dates = new[] { today, today.AddDays(-1), today.AddDays(-2) };
        var sut = harness.Build(user, readingCount: 3, dates: dates);

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
        var sut = harness.Build(user, readingCount: 3, dates: dates);

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

        var sut = harness.Build(user, readingCount: 1);
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

        var sut = new AchievementService(harness.Achievements.Object, harness.Readings.Object, harness.Users.Object);

        var result = await sut.GetAllWithUserStatusAsync(user.Id);

        result.Should().HaveCount(7);
        result.First(r => r.Code == AchievementService.Codes.FirstReading).UnlockedAt.Should().Be(unlockedAt);
        result.First(r => r.Code == AchievementService.Codes.Total10).UnlockedAt.Should().BeNull();
    }
    [Fact]
    public async Task Combined_check_reuses_catalog_and_ownership_and_returns_new_unlocks()
    {
        var harness = new Harness();
        var user = NewUser();
        var sut = harness.Build(user, 50);
        var result = await sut.CheckAndGetAllAsync(user.Id);
        result.Should().HaveCount(7);
        result.Where(a => a.UnlockedAt != null).Select(a => a.Code)
            .Should().BeEquivalentTo("first_reading", "total_10", "total_50");
        harness.Achievements.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Achievements.Verify(x => x.GetByUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        harness.Achievements.Verify(x => x.GrantManyAsync(It.Is<IReadOnlyCollection<UserAchievement>>(a => a.Count == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Combined_check_observes_unlock_won_by_concurrent_request()
    {
        var harness = new Harness();
        var user = NewUser();
        var sut = harness.Build(user, 1);
        var winning = new UserAchievement
        {
            UserId = user.Id, AchievementId = harness.All.Single(a => a.Code == "first_reading").Id,
            UnlockedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        harness.Achievements.SetupSequence(x => x.GetByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]).ReturnsAsync([winning]);
        harness.Achievements.Setup(x => x.GrantManyAsync(It.IsAny<IReadOnlyCollection<UserAchievement>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var result = await sut.CheckAndGetAllAsync(user.Id);
        result.Single(a => a.Code == "first_reading").UnlockedAt.Should().Be(winning.UnlockedAt);
        harness.Achievements.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

}
