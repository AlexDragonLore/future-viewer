using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.TarotPlus;

public static class TarotPlusSpreadCatalog
{
    private static readonly IReadOnlyDictionary<string, TarotPlusSpreadDefinition> Spreads =
        new List<TarotPlusSpreadDefinition>
        {
            new("current_core_7", "Текущее ядро", new[]
            {
                "Где человек находится сейчас",
                "Главное напряжение",
                "Скрытая опора",
                "Что требует честности",
                "Что уже созрело",
                "Ближайший ресурс",
                "Первый практический шаг"
            }),
            new("blind_spot_5", "Слепая зона", new[]
            {
                "Что пользователь недооценивает",
                "Что пользователь переоценивает",
                "Повторяющийся паттерн",
                "Цена старой стратегии",
                "Новый угол зрения"
            }),
            new("relationship_dynamic_7", "Динамика отношений", new[]
            {
                "Позиция пользователя",
                "Позиция другой стороны без чтения мыслей",
                "Общий фон",
                "Потребность пользователя",
                "Граница, которую важно увидеть",
                "Риск ближайшего периода",
                "Бережное действие"
            }),
            new("career_path_7", "Путь работы", new[]
            {
                "Текущая роль",
                "Сильная сторона",
                "Истощающий фактор",
                "Скрытая возможность",
                "Навык для развития",
                "Риск решения",
                "Следующий шаг"
            }),
            new("money_pattern_7", "Денежный паттерн", new[]
            {
                "Текущий денежный сценарий",
                "Что поддерживает стабильность",
                "Что распыляет ресурс",
                "Отношение к ценности",
                "Осторожность без финансовых гарантий",
                "Практика на 30 дней",
                "Долгий фокус"
            }),
            new("decision_ab", "Выбор A/B", new[]
            {
                "Суть выбора",
                "Вариант A: ресурс",
                "Вариант A: риск",
                "Вариант B: ресурс",
                "Вариант B: риск",
                "Критерий решения",
                "Что не стоит игнорировать"
            }),
            new("self_archetype_7", "Личный архетип", new[]
            {
                "Текущий образ себя",
                "Сила",
                "Тень",
                "Непрожитое желание",
                "Внутренний союзник",
                "Внешнее проявление",
                "Практика интеграции"
            }),
            new("general_life_7", "Жизненный обзор", new[]
            {
                "Главная тема периода",
                "Тело и ресурс",
                "Отношения",
                "Работа и дело",
                "Деньги и устойчивость",
                "Внутренний рост",
                "Ближайшая развилка"
            }),
            new("action_plan_6", "План действий", new[]
            {
                "На 7 дней",
                "На 30 дней",
                "На 90 дней",
                "Что прекратить",
                "Что усилить",
                "Как понять, что путь работает"
            })
        }.ToDictionary(x => x.Id, StringComparer.Ordinal);

    public static TarotPlusSpreadDefinition Get(string id) => Spreads[id];

    public static IReadOnlyList<TarotPlusSpreadDefinition> SelectFor(TarotPlusRoute route)
    {
        var routeSpread = route switch
        {
            TarotPlusRoute.Relationship => "relationship_dynamic_7",
            TarotPlusRoute.Career => "career_path_7",
            TarotPlusRoute.Money => "money_pattern_7",
            TarotPlusRoute.Decision => "decision_ab",
            TarotPlusRoute.SelfIdentity => "self_archetype_7",
            TarotPlusRoute.Family => "relationship_dynamic_7",
            TarotPlusRoute.ResourceState => "blind_spot_5",
            TarotPlusRoute.SafetySensitive => "general_life_7",
            _ => "general_life_7"
        };

        return new[]
        {
            Get("current_core_7"),
            Get(routeSpread),
            Get("action_plan_6")
        };
    }
}

public sealed record TarotPlusSpreadDefinition(
    string Id,
    string Name,
    IReadOnlyList<string> Positions)
{
    public int CardCount => Positions.Count;
}
