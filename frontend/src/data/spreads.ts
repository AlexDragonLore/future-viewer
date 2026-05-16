import { SpreadType } from '@/types'

export interface SpreadMeta {
  type: SpreadType
  label: string
  anchorId: string
  seoPath: string
  cardCount: number
  shortDescription: string
  longDescription: string
}

export const SPREADS_META: SpreadMeta[] = [
  {
    type: SpreadType.SingleCard,
    label: 'Карта дня',
    anchorId: 'single-card',
    seoPath: '/tarot/spreads/karta-dnya',
    cardCount: 1,
    shortDescription:
      'Одна карта — один фокус дня: ключевой образ, с которым стоит идти в ближайшие часы.',
    longDescription:
      'Самый короткий расклад: одна карта отвечает на общий вопрос «на что обратить внимание сегодня» или даёт фокус к конкретному событию. Хорош для ежедневной практики, для первой пробы Таро и для ситуаций, когда уточнять нечего — нужен только намёк. В бесплатном режиме доступен без платного доступа.',
  },
  {
    type: SpreadType.ThreeCard,
    label: 'Три карты',
    anchorId: 'three-card',
    seoPath: '/tarot/spreads/tri-karty',
    cardCount: 3,
    shortDescription:
      'Прошлое → Настоящее → Будущее: динамика ситуации в трёх образах, классический «мини-расклад».',
    longDescription:
      'Три карты выкладываются слева направо и трактуются как прошлое, настоящее и будущее исследуемой темы. Даёт понимание, откуда пришла ситуация, в какой точке находится сейчас и куда склоняется вектор развития. Подходит для конкретных вопросов: отношения, проект, решение. Читается быстрее «Кельтского креста», но даёт достаточно глубины для ежедневного применения.',
  },
  {
    type: SpreadType.CelticCross,
    label: 'Кельтский крест',
    anchorId: 'celtic-cross',
    seoPath: '/tarot/spreads/keltskiy-krest',
    cardCount: 10,
    shortDescription:
      'Десять позиций: ситуация, вызов, сознательное и подсознательное, прошлое, будущее, влияние, страхи, окружение, итог.',
    longDescription:
      'Старейший из универсальных раскладов Таро, описан Артуром Эдвардом Уэйтом в «Иллюстрированном ключе к Таро» (1910). Десять позиций охватывают внутренний и внешний план: текущую ситуацию, противодействие, основание, недавнее прошлое и ближайшее будущее, установку кверента, внешние влияния, надежды и страхи, итоговый исход. Подходит для сложных вопросов, жизненных развилок и случаев, когда нужен развернутый ответ с учётом контекста.',
  },
]

export function findSpreadMeta(type: SpreadType): SpreadMeta | undefined {
  return SPREADS_META.find((s) => s.type === type)
}
