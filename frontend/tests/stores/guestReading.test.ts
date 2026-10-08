import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { DeckType, SpreadType, type Reading } from '@/types'
import { clearGuestContinuation, getGuestContinuation, saveGuestContinuation } from '@/utils/guestReading'
import { clearAccountSession } from '@/utils/accountSession'

const createGuest = vi.fn()
const guestPreview = vi.fn()
const unlockGuest = vi.fn()
vi.mock('@/api/readingApi', () => ({ readingApi: {
  createGuest: (...args: unknown[]) => createGuest(...args),
  guestPreview: (...args: unknown[]) => guestPreview(...args),
  unlockGuest: (...args: unknown[]) => unlockGuest(...args),
} }))
vi.mock('@/api/subscriptionApi', () => ({ subscriptionApi: { status: vi.fn(async () => null) } }))

import { useReadingStore } from '@/stores/useReadingStore'
import { useAuthStore } from '@/stores/useAuthStore'

const preview: Reading = {
  id: 'guest-1', spreadType: SpreadType.ThreeCard, spreadName: 'Три карты',
  question: 'С чего начать?', createdAt: '2026-10-03T12:00:00Z', cards: [],
  interpretation: 'Первая половина…', deckType: DeckType.RWS, isPreview: true,
}
const continuation = () => ({ ticket: 'server-encrypted-ticket', expiresAt: new Date(Date.now() + 86_400_000).toISOString() })

describe('guest reading continuation', () => {
  beforeEach(() => {
    localStorage.clear()
    clearGuestContinuation()
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('stores only an opaque ticket and shows the preview', async () => {
    createGuest.mockResolvedValue({ ...continuation(), reading: preview })
    const store = useReadingStore()
    const { cardsPromise, donePromise } = store.createGuest(preview.question)
    await Promise.all([cardsPromise, donePromise])
    expect(store.current).toEqual(preview)
    expect(store.streamingDone).toBe(true)
    const persisted = localStorage.getItem('fv_guest_reading_v1')!
    expect(persisted).toContain('server-encrypted-ticket')
    expect(persisted).not.toContain(preview.question)
    expect(persisted).not.toContain(preview.interpretation)
  })

  it('restores a preview after a reload without generating a new reading', async () => {
    saveGuestContinuation(continuation())
    guestPreview.mockResolvedValue(preview)
    const store = useReadingStore()
    expect(await store.restoreGuest()).toBe(true)
    expect(store.current).toEqual(preview)
    expect(guestPreview).toHaveBeenCalledWith('server-encrypted-ticket')
    expect(createGuest).not.toHaveBeenCalled()
    expect(unlockGuest).not.toHaveBeenCalled()
  })

  it('survives account state clearing on login and unlocks the same reading', async () => {
    saveGuestContinuation(continuation())
    const store = useReadingStore()
    store.current = preview
    clearAccountSession()
    useAuthStore().token = 'verified-session'
    unlockGuest.mockResolvedValue({ ...preview, interpretation: 'Полное толкование', isPreview: false })
    expect(await store.restoreGuest()).toBe(true)
    expect(store.current?.id).toBe(preview.id)
    expect(store.current?.isPreview).toBe(false)
    expect(unlockGuest).toHaveBeenCalledWith('server-encrypted-ticket')
    expect(getGuestContinuation()).toBeNull()
  })

  it('does not expose a response after the account changes', async () => {
    saveGuestContinuation(continuation())
    useAuthStore().token = 'verified-session'
    let resolve!: (reading: Reading) => void
    unlockGuest.mockImplementation(() => new Promise<Reading>(r => { resolve = r }))
    const store = useReadingStore()
    const pending = store.restoreGuest()
    clearAccountSession()
    resolve({ ...preview, isPreview: false, interpretation: 'Private complete text' })
    expect(await pending).toBe(false)
    expect(store.current).toBeNull()
  })

  it('preserves the ticket after a network error so resuming can be retried', async () => {
    saveGuestContinuation(continuation())
    guestPreview.mockRejectedValue(new Error('Network error'))
    const store = useReadingStore()
    expect(await store.restoreGuest()).toBe(false)
    expect(getGuestContinuation()).not.toBeNull()
    expect(store.error).toBeTruthy()
    expect(store.loading).toBe(false)
  })

  it.each([402, 429])('restores the preview after unlock is denied with %s after a reload', async (status) => {
    saveGuestContinuation(continuation())
    useAuthStore().token = 'verified-session'
    unlockGuest.mockRejectedValue({ response: { status, data: { error: status === 429 ? 'quota_exceeded' : 'subscription_required', message: 'Бесплатный расклад уже использован.' } } })
    guestPreview.mockResolvedValue(preview)
    const store = useReadingStore()
    expect(await store.restoreGuest()).toBe(false)
    expect(store.current).toEqual(preview)
    expect(store.guestUnlockBlocked).toBe(true)
    expect(store.streamingDone).toBe(true)
    expect(store.error).toBe('Бесплатный расклад уже использован.')
    expect(getGuestContinuation()).not.toBeNull()
    expect(guestPreview).toHaveBeenCalledWith('server-encrypted-ticket')
  })

  it('keeps a generic rate-limited unlock retryable while retaining the preview', async () => {
    saveGuestContinuation(continuation())
    useAuthStore().token = 'verified-session'
    unlockGuest.mockRejectedValue({ response: { status: 429, data: { message: 'Слишком много запросов. Повторите позже.' } } })
    guestPreview.mockResolvedValue(preview)
    const store = useReadingStore()
    expect(await store.restoreGuest()).toBe(false)
    expect(store.current).toEqual(preview)
    expect(store.guestUnlockBlocked).toBe(false)
    expect(getGuestContinuation()).not.toBeNull()
  })

  it('opens a previously denied preview when paid access later allows unlocking', async () => {
    saveGuestContinuation(continuation())
    useAuthStore().token = 'verified-session'
    const store = useReadingStore()
    store.guestUnlockBlocked = true
    unlockGuest.mockResolvedValue({ ...preview, isPreview: false })
    expect(await store.restoreGuest()).toBe(true)
    expect(store.guestUnlockBlocked).toBe(false)
    expect(store.current?.isPreview).toBe(false)
    expect(getGuestContinuation()).toBeNull()
  })

  it('discards expired or rejected tickets', async () => {
    saveGuestContinuation({ ...continuation(), expiresAt: new Date(Date.now() - 1000).toISOString() })
    expect(getGuestContinuation()).toBeNull()
    saveGuestContinuation(continuation())
    guestPreview.mockRejectedValue({ response: { status: 404, data: { message: 'Расклад недоступен' } } })
    expect(await useReadingStore().restoreGuest()).toBe(false)
    expect(getGuestContinuation()).toBeNull()
  })
})
