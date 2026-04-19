using FluentAssertions;
using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class LeaderboardServiceTests
{
    [Fact]
    public async Task GetMonthlyAsync_uses_current_year_and_month_when_omitted()
    {
        var repo = new Mock<ILeaderboardRepository>();
        int? capturedYear = null;
        int? capturedMonth = null;
        int? capturedTake = null;
        repo.Setup(r => r.GetMonthlyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((int y, int m, int t, CancellationToken _) =>
            {
                capturedYear = y;
                capturedMonth = m;
                capturedTake = t;
            })
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>)Array.Empty<LeaderboardEntryDto>());

        var sut = new LeaderboardService(repo.Object);

        await sut.GetMonthlyAsync();

        var now = DateTime.UtcNow;
        capturedYear.Should().Be(now.Year);
        capturedMonth.Should().Be(now.Month);
        capturedTake.Should().Be(50);
    }

    [Fact]
    public async Task GetMonthlyAsync_clamps_take_in_range_1_to_200()
    {
        var repo = new Mock<ILeaderboardRepository>();
        var captured = new List<int>();
        repo.Setup(r => r.GetMonthlyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((int _, int _, int t, CancellationToken _) => captured.Add(t))
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>)Array.Empty<LeaderboardEntryDto>());

        var sut = new LeaderboardService(repo.Object);

        await sut.GetMonthlyAsync(2026, 3, take: 0);
        await sut.GetMonthlyAsync(2026, 3, take: 500);
        await sut.GetMonthlyAsync(2026, 3, take: 42);

        captured.Should().Equal(1, 200, 42);
    }

    [Fact]
    public async Task GetMonthlyAsync_passes_explicit_year_month_through()
    {
        var repo = new Mock<ILeaderboardRepository>();
        int? y = null, m = null;
        repo.Setup(r => r.GetMonthlyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((int py, int pm, int _, CancellationToken _) => { y = py; m = pm; })
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>)Array.Empty<LeaderboardEntryDto>());

        var sut = new LeaderboardService(repo.Object);
        await sut.GetMonthlyAsync(2026, 2);

        y.Should().Be(2026);
        m.Should().Be(2);
    }

    [Theory]
    [InlineData(2023, 1)]
    [InlineData(2101, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public async Task GetMonthlyAsync_rejects_out_of_range_year_or_month(int year, int month)
    {
        var repo = new Mock<ILeaderboardRepository>();
        var sut = new LeaderboardService(repo.Object);

        await FluentActions.Awaiting(() => sut.GetMonthlyAsync(year, month))
            .Should().ThrowAsync<ValidationException>();

        repo.Verify(r => r.GetMonthlyAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllTimeAsync_clamps_take()
    {
        var repo = new Mock<ILeaderboardRepository>();
        var captured = new List<int>();
        repo.Setup(r => r.GetAllTimeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((int t, CancellationToken _) => captured.Add(t))
            .ReturnsAsync((IReadOnlyList<LeaderboardEntryDto>)Array.Empty<LeaderboardEntryDto>());

        var sut = new LeaderboardService(repo.Object);

        await sut.GetAllTimeAsync();
        await sut.GetAllTimeAsync(take: 1000);
        await sut.GetAllTimeAsync(take: -5);

        captured.Should().Equal(50, 200, 1);
    }

    [Fact]
    public async Task GetUserSummaryAsync_passes_through_repo()
    {
        var userId = Guid.NewGuid();
        var summary = new UserScoreSummaryDto
        {
            TotalScore = 42,
            MonthlyScore = 10,
            Rank = 3,
            MonthlyRank = 2,
            FeedbackCount = 5,
            AverageScore = 8.4
        };

        var repo = new Mock<ILeaderboardRepository>();
        repo.Setup(r => r.GetUserSummaryAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var sut = new LeaderboardService(repo.Object);

        var result = await sut.GetUserSummaryAsync(userId);

        result.Should().BeSameAs(summary);
    }
}
