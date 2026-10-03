import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { DeckType } from '@/types'
import { useDeckStore } from '@/stores/useDeckStore'
import { nextTick } from 'vue'
import { useCookiePreferences } from '@/composables/useCookiePreferences'

describe('useDeckStore', () => {
  beforeEach(() => {
    localStorage.clear()
    useCookiePreferences().resetCookiePreferences()
    setActivePinia(createPinia())
  })

  it('defaults to RWS when nothing is stored', () => {
    const store = useDeckStore()
    expect(store.current).toBe(DeckType.RWS)
  })

  it('persists the selected deck to localStorage', async () => {
    useCookiePreferences().saveDetailed({ preferences: true, analytics: false, marketing: false })
    const store = useDeckStore()
    store.select(DeckType.Marseille)
    await nextTick()
    expect(localStorage.getItem('fv_deck')).toBe(String(DeckType.Marseille))
  })

  it('loads previously-saved deck from localStorage', () => {
    useCookiePreferences().saveDetailed({ preferences: true, analytics: false, marketing: false })
    localStorage.setItem('fv_deck', String(DeckType.Thoth))
    setActivePinia(createPinia())
    const store = useDeckStore()
    expect(store.current).toBe(DeckType.Thoth)
  })

  it('falls back to RWS for invalid stored values', () => {
    useCookiePreferences().saveDetailed({ preferences: true, analytics: false, marketing: false })
    localStorage.setItem('fv_deck', '9999')
    setActivePinia(createPinia())
    const store = useDeckStore()
    expect(store.current).toBe(DeckType.RWS)
  })

  it('removes the optional deck preference when consent is withdrawn', () => {
    const cookies = useCookiePreferences()
    cookies.saveDetailed({ preferences: true, analytics: false, marketing: false })
    localStorage.setItem('fv_deck', String(DeckType.Thoth))

    cookies.saveDetailed({ preferences: false, analytics: false, marketing: false })

    expect(localStorage.getItem('fv_deck')).toBeNull()
  })
})
