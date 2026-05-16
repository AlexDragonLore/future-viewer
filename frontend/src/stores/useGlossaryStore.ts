import { defineStore } from 'pinia'
import { ref } from 'vue'
import { glossaryApi } from '@/api/glossaryApi'
import { extractApiError } from '@/api/httpClient'
import { TAROT_SEO_CARDS, type TarotSeoCard } from '@/data/tarotSeoCatalog.js'
import { CardSuit, SuggestedTone, type CardGlossary } from '@/types'
import { isLocalStaticPreview } from '@/utils/runtime'

const suitBySlug: Record<string, CardSuit> = {
  major: CardSuit.MajorArcana,
  wands: CardSuit.Wands,
  cups: CardSuit.Cups,
  swords: CardSuit.Swords,
  pentacles: CardSuit.Pentacles,
}

function toGlossaryCard(card: TarotSeoCard): CardGlossary {
  return {
    id: card.id,
    name: card.name,
    nameEn: card.nameEn,
    suit: suitBySlug[card.suitSlug] ?? CardSuit.MajorArcana,
    number: card.number,
    imagePath: card.imagePath,
    descriptionUpright: card.upright,
    descriptionReversed: card.reversed,
    shortUpright: card.summary,
    shortReversed: card.reversed,
    uprightKeywords: card.uprightKeywords,
    reversedKeywords: card.reversedKeywords,
    suggestedTone: SuggestedTone.Neutral,
    aliases: card.aliases,
    deckVariants: [],
  }
}

const staticGlossaryCards = TAROT_SEO_CARDS.map(toGlossaryCard)

export const useGlossaryStore = defineStore('glossary', () => {
  const cards = ref<CardGlossary[]>([])
  const detail = ref<CardGlossary | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function loadAll() {
    error.value = null
    if (cards.value.length > 0) return
    if (isLocalStaticPreview()) {
      cards.value = staticGlossaryCards
      return
    }
    loading.value = true
    try {
      cards.value = await glossaryApi.list()
    } catch (e) {
      cards.value = staticGlossaryCards
      error.value = cards.value.length ? null : extractApiError(e, 'Не удалось загрузить глоссарий')
    } finally {
      loading.value = false
    }
  }

  async function loadDetail(id: number) {
    loading.value = true
    error.value = null
    detail.value = null
    try {
      detail.value = await glossaryApi.get(id)
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить карту')
    } finally {
      loading.value = false
    }
  }

  return { cards, detail, loading, error, loadAll, loadDetail }
})
