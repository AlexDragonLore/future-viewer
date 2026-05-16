import { describe, expect, it } from 'vitest'
import {
  FAQ_ITEMS,
  SEO_CONTENT_ROUTES,
  TAROT_SEO_CARDS,
  TAROT_SEO_DECKS,
  TAROT_SEO_SPREADS,
  findTarotSeoCardById,
  findTarotSeoCardBySlug,
} from '@/data/tarotSeoCatalog.js'

describe('tarotSeoCatalog', () => {
  it('contains all planned public SEO pages without duplicate paths', () => {
    expect(TAROT_SEO_CARDS).toHaveLength(78)
    expect(TAROT_SEO_SPREADS).toHaveLength(3)
    expect(TAROT_SEO_DECKS).toHaveLength(5)
    expect(FAQ_ITEMS.length).toBeGreaterThanOrEqual(5)

    const paths = SEO_CONTENT_ROUTES.map((route) => route.path)
    expect(new Set(paths).size).toBe(paths.length)
    expect(paths).toContain('/tarot/cards/shut')
    expect(paths).toContain('/tarot/cards/mag')
    expect(paths).toContain('/tarot/cards/verhovnaya-zhritsa')
    expect(paths).toContain('/tarot/cards/tuz-kubkov')
    expect(paths).toContain('/tarot/spreads/tri-karty')
    expect(paths).toContain('/tarot/decks/rider-waite-smith')
    expect(paths).toContain('/faq')
  })

  it('maps backend card ids to stable SEO slugs', () => {
    expect(findTarotSeoCardById(1)?.slug).toBe('shut')
    expect(findTarotSeoCardById(37)?.slug).toBe('tuz-kubkov')
    expect(findTarotSeoCardBySlug('shut')?.id).toBe(1)
    expect(findTarotSeoCardBySlug('tuz-kubkov')?.id).toBe(37)
  })
})
