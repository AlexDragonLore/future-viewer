export type SeoContentKind = 'card' | 'spread' | 'deck' | 'faq'

export interface TarotSeoCard {
  id: number
  number: number
  slug: string
  name: string
  nameEn: string
  aliases: string[]
  summary: string
  upright: string
  reversed: string
  advice: string
  uprightKeywords: string[]
  reversedKeywords: string[]
  group: string
  suitLabel: string
  suitSlug: string
  element: string
  imagePath: string
  relatedSlugs: string[]
}

export interface TarotSeoSpread {
  slug: string
  label: string
  cardCount: number
  summary: string
  description: string
  bestFor: string[]
  positions: string[]
}

export interface TarotSeoDeck {
  slug: string
  label: string
  period: string
  summary: string
  description: string
  bestFor: string[]
}

export interface FaqItem {
  question: string
  answer: string
}

export interface SeoContentRoute {
  name: string
  path: string
  title: string
  description: string
  priority: number
  changefreq: string
  type: string
  contentKind: SeoContentKind
  slug: string
}

export interface StructuredDataContext {
  siteUrl: string
  siteName: string
  defaultImage: string
}

export const TAROT_SEO_CARDS: TarotSeoCard[]
export const TAROT_SEO_SPREADS: TarotSeoSpread[]
export const TAROT_SEO_DECKS: TarotSeoDeck[]
export const FAQ_ITEMS: FaqItem[]
export const SEO_CONTENT_ROUTES: SeoContentRoute[]

export function findTarotSeoCardBySlug(slug: string): TarotSeoCard | undefined
export function findTarotSeoCardById(id: number): TarotSeoCard | undefined
export function findTarotSeoSpreadBySlug(slug: string): TarotSeoSpread | undefined
export function findTarotSeoDeckBySlug(slug: string): TarotSeoDeck | undefined
export function findSeoContentRouteByPath(path: string): SeoContentRoute | undefined
export function buildStructuredDataForRoute(route: SeoContentRoute, context: StructuredDataContext): object[]
