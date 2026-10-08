import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { DeckType, SpreadType, type Reading } from '@/types'
import { clearGuestContinuation, clearUnlockedGuestReading, getGuestContinuation, getUnlockedGuestReading, saveGuestContinuation, saveUnlockedGuestReading } from '@/utils/guestReading'
import { clearAccountSession } from '@/utils/accountSession'

const createGuest = vi.fn()
const guestPreview = vi.fn()
const unlockGuest = vi.fn()
const getReading = vi.fn()
vi.mock('@/api/readingApi', () => ({ readingApi: {
  createGuest: (...args: unknown[]) => createGuest(...args),
  guestPreview: (...args: unknown[]) => guestPreview(...args),
  unlockGuest: (...args: unknown[]) => unlockGuest(...args),
  get: (...args: unknown[]) => getReading(...args),
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
    clearUnlockedGuestReading()
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
    useAuthStore().userId = 'guest-owner'
    unlockGuest.mockResolvedValue({ ...preview, interpretation: 'Полное толкование', isPreview: false })
    expect(await store.restoreGuest()).toBe(true)
    expect(store.current?.id).toBe(preview.id)
    expect(store.current?.isPreview).toBe(false)
    expect(unlockGuest).toHaveBeenCalledWith('server-encrypted-ticket')
    expect(getGuestContinuation()).toBeNull()
    expect(getUnlockedGuestReading('guest-owner')).toEqual({ readingId: preview.id, ownerUserId: 'guest-owner', expiresAt: expect.any(String) })
    const persisted = localStorage.getItem('fv_guest_reading_unlocked_v1')!
    expect(persisted).not.toContain(preview.question)
    expect(persisted).not.toContain('Полное толкование')
  })

  it('restores the full unlocked reading after reload without another unlock or a new reading', async () => {
    const auth = useAuthStore()
    auth.token = 'verified-session'
    auth.userId = 'guest-owner'
    saveUnlockedGuestReading({ readingId: preview.id, ownerUserId: auth.userId, expiresAt: continuation().expiresAt })
    const full = { ...preview, interpretation: 'Полное толкование', isPreview: false }
    getReading.mockResolvedValue(full)

    expect(await useReadingStore().restoreGuest()).toBe(true)
    expect(useReadingStore().current).toEqual(full)
    expect(getReading).toHaveBeenCalledWith(preview.id)
    expect(unlockGuest).not.toHaveBeenCalled()
    expect(createGuest).not.toHaveBeenCalled()
  })

  it('does not restore an unlocked reference for a different account or an anonymous session', async () => {
    saveUnlockedGuestReading({ readingId: preview.id, ownerUserId: 'guest-owner', expiresAt: continuation().expiresAt })
    expect(await useReadingStore().restoreGuest()).toBe(false)
    useAuthStore().token = 'other-session'
    useAuthStore().userId = 'other-owner'
    expect(await useReadingStore().restoreGuest()).toBe(false)
    expect(getReading).not.toHaveBeenCalled()
    expect(useReadingStore().current).toBeNull()
    expect(getUnlockedGuestReading('guest-owner')?.readingId).toBe(preview.id)
  })

  it('expires the unlocked reference with the original guest ticket and clears it for a new preview', () => {
    const reference = { readingId: preview.id, ownerUserId: 'guest-owner', expiresAt: new Date(Date.now() - 1000).toISOString() }
    saveUnlockedGuestReading(reference)
    expect(getUnlockedGuestReading('guest-owner')).toBeNull()
    expect(localStorage.getItem('fv_guest_reading_unlocked_v1')).toBeNull()
    saveUnlockedGuestReading({ ...reference, expiresAt: continuation().expiresAt })
    saveGuestContinuation(continuation())
    expect(getUnlockedGuestReading('guest-owner')).toBeNull()
  })

  it('does not expose a restored full reading after the account changes', async () => {
    saveUnlockedGuestReading({ readingId: preview.id, ownerUserId: 'guest-owner', expiresAt: continuation().expiresAt })
    useAuthStore().token = 'verified-session'
    useAuthStore().userId = 'guest-owner'
    let resolve!: (reading: Reading) => void
    getReading.mockImplementation(() => new Promise<Reading>(r => { resolve = r }))
    const pending = useReadingStore().restoreGuest()
    clearAccountSession()
    resolve({ ...preview, isPreview: false, interpretation: 'Private complete text' })
    expect(await pending).toBe(false)
    expect(useReadingStore().current).toBeNull()
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
