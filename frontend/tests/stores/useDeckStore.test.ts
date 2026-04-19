import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { DeckType } from '@/types'
import { useDeckStore } from '@/stores/useDeckStore'
import { nextTick } from 'vue'

describe('useDeckStore', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('defaults to RWS when nothing is stored', () => {
    const store = useDeckStore()
    expect(store.current).toBe(DeckType.RWS)
  })

  it('persists the selected deck to localStorage', async () => {
    const store = useDeckStore()
    store.select(DeckType.Marseille)
    await nextTick()
    expect(localStorage.getItem('fv_deck')).toBe(String(DeckType.Marseille))
  })

  it('loads previously-saved deck from localStorage', () => {
    localStorage.setItem('fv_deck', String(DeckType.Thoth))
    setActivePinia(createPinia())
    const store = useDeckStore()
    expect(store.current).toBe(DeckType.Thoth)
  })

  it('falls back to RWS for invalid stored values', () => {
    localStorage.setItem('fv_deck', '9999')
    setActivePinia(createPinia())
    const store = useDeckStore()
    expect(store.current).toBe(DeckType.RWS)
  })
})
