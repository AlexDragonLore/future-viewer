using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class PersonalizationServiceTests
{
    [Fact]
    public async Task SaveExtractedMemoryAsync_adds_unique_rules_and_enforces_limit()
    {
        var users = new Mock<IUserRepository>();
        var memory = new Mock<IUserMemoryRepository>();
        memory.Setup(m => m.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserMemoryRule { UserId = Guid.NewGuid(), Text = "Пользователь изучает новую работу." }
            });

        var sut = new PersonalizationService(users.Object, memory.Object);
        var userId = Guid.NewGuid();

        await sut.SaveExtractedMemoryAsync(userId, new[]
        {
            "Пользователь изучает новую работу.",
            "Пользователь выбирает между двумя проектами."
        });

        memory.Verify(m => m.AddAsync(
            It.Is<UserMemoryRule>(r => r.UserId == userId && r.Text == "Пользователь выбирает между двумя проектами."),
            It.IsAny<CancellationToken>()), Times.Once);
        memory.Verify(m => m.DeleteOldestBeyondLimitAsync(userId, PersonalizationService.MemoryLimit, It.IsAny<CancellationToken>()), Times.Once);
    }
}
