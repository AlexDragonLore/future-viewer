using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;

namespace FutureViewer.Domain.Entities;

public sealed class Spread
{
    public required SpreadType Type { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<CardPosition> Positions { get; init; }

    public int CardCount => Positions.Count;

    public static Spread SingleCard { get; } = new()
    {
        Type = SpreadType.SingleCard,
        Name = "Карта дня",
        Positions =
        [
            new CardPosition { Index = 0, Name = "Совет", Meaning = "Совет на текущий момент" }
        ]
    };

    public static Spread ThreeCard { get; } = new()
    {
        Type = SpreadType.ThreeCard,
        Name = "Прошлое — Настоящее — Будущее",
        Positions =
        [
            new CardPosition { Index = 0, Name = "Прошлое", Meaning = "Что привело к текущей ситуации" },
            new CardPosition { Index = 1, Name = "Настоящее", Meaning = "Что происходит сейчас" },
            new CardPosition { Index = 2, Name = "Будущее", Meaning = "К чему это ведёт" }
        ]
    };

    public static Spread CelticCross { get; } = new()
    {
        Type = SpreadType.CelticCross,
        Name = "Кельтский крест",
        Positions =
        [
            new CardPosition { Index = 0, Name = "Сердце вопроса", Meaning = "Суть ситуации" },
            new CardPosition { Index = 1, Name = "Вызов", Meaning = "Что противостоит" },
            new CardPosition { Index = 2, Name = "Основание", Meaning = "Корни ситуации" },
            new CardPosition { Index = 3, Name = "Прошлое", Meaning = "Уходящие влияния" },
            new CardPosition { Index = 4, Name = "Корона", Meaning = "Что может быть" },
            new CardPosition { Index = 5, Name = "Будущее", Meaning = "Что грядёт" },
            new CardPosition { Index = 6, Name = "Вы", Meaning = "Ваше состояние" },
            new CardPosition { Index = 7, Name = "Окружение", Meaning = "Внешние влияния" },
            new CardPosition { Index = 8, Name = "Надежды и страхи", Meaning = "Внутренний мир" },
            new CardPosition { Index = 9, Name = "Итог", Meaning = "Возможный исход" }
        ]
    };

    public static Spread From(SpreadType type) => type switch
    {
        SpreadType.SingleCard => SingleCard,
        SpreadType.ThreeCard => ThreeCard,
        SpreadType.CelticCross => CelticCross,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown spread type")
    };
}
