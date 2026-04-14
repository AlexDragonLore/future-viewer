using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;

namespace FutureViewer.Domain.Entities;

public class Spread
{
    public SpreadType Type { get; }
    public string Name { get; }
    public IReadOnlyList<CardPosition> Positions { get; }

    public Spread(SpreadType type, string name, IReadOnlyList<CardPosition> positions)
    {
        Type = type;
        Name = name;
        Positions = positions;
    }

    public int CardCount => Positions.Count;

    public static Spread SingleCard { get; } = new(
        SpreadType.SingleCard,
        "Карта дня",
        new[] { new CardPosition(0, "Совет", "Совет на текущий момент") });

    public static Spread ThreeCard { get; } = new(
        SpreadType.ThreeCard,
        "Прошлое — Настоящее — Будущее",
        new[]
        {
            new CardPosition(0, "Прошлое", "Что привело к текущей ситуации"),
            new CardPosition(1, "Настоящее", "Что происходит сейчас"),
            new CardPosition(2, "Будущее", "К чему это ведёт")
        });

    public static Spread CelticCross { get; } = new(
        SpreadType.CelticCross,
        "Кельтский крест",
        new[]
        {
            new CardPosition(0, "Сердце вопроса", "Суть ситуации"),
            new CardPosition(1, "Вызов", "Что противостоит"),
            new CardPosition(2, "Основание", "Корни ситуации"),
            new CardPosition(3, "Прошлое", "Уходящие влияния"),
            new CardPosition(4, "Корона", "Что может быть"),
            new CardPosition(5, "Будущее", "Что грядёт"),
            new CardPosition(6, "Вы", "Ваше состояние"),
            new CardPosition(7, "Окружение", "Внешние влияния"),
            new CardPosition(8, "Надежды и страхи", "Внутренний мир"),
            new CardPosition(9, "Итог", "Возможный исход")
        });

    public static Spread From(SpreadType type) => type switch
    {
        SpreadType.SingleCard => SingleCard,
        SpreadType.ThreeCard => ThreeCard,
        SpreadType.CelticCross => CelticCross,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown spread type")
    };
}
