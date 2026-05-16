import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

const listMock = vi.fn()
const getMock = vi.fn()

vi.mock('@/api/glossaryApi', () => ({
  glossaryApi: {
    list: (...args: unknown[]) => listMock(...args),
    get: (...args: unknown[]) => getMock(...args),
  },
}))

import { useGlossaryStore } from '@/stores/useGlossaryStore'

describe('useGlossaryStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    listMock.mockReset()
    getMock.mockReset()
  })

  it('falls back to the static SEO card catalog when glossary API fails', async () => {
    listMock.mockRejectedValueOnce(new Error('backend unavailable'))

    const store = useGlossaryStore()
    await store.loadAll()

    expect(store.error).toBeNull()
    expect(store.cards).toHaveLength(78)
    expect(store.cards[0].name).toBe('Шут')
    expect(store.cards.find((card) => card.name === 'Туз Кубков')).toBeTruthy()
  })
})
