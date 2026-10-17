import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { FeedbackStatus } from '@/types'
import type { AdminFeedback } from '@/types/admin'
import { clearAccountSession } from '@/utils/accountSession'
import { adminReadingFixtures, adminUserDetailFixture } from '../fixtures/admin'

const listMock = vi.fn()
const createMock = vi.fn()
const createSyntheticMock = vi.fn()
const updateMock = vi.fn()
const deleteMock = vi.fn()
const getStatsMock = vi.fn()
const readingsMock = vi.fn()
const userDetailMock = vi.fn()
const usersMock = vi.fn()
const subscriptionMock = vi.fn()

vi.mock('@/api/adminApi', () => ({
  adminApi: {
    listReadings: (...args: unknown[]) => readingsMock(...args),
    setUserSubscription: (...args: unknown[]) => subscriptionMock(...args),
    getUser: (...args: unknown[]) => userDetailMock(...args),
    listUsers: (...args: unknown[]) => usersMock(...args),
    listFeedbacks: (...args: unknown[]) => listMock(...args),
    createFeedback: (...args: unknown[]) => createMock(...args),
    createSyntheticFeedback: (...args: unknown[]) => createSyntheticMock(...args),
    updateFeedback: (...args: unknown[]) => updateMock(...args),
    deleteFeedback: (...args: unknown[]) => deleteMock(...args),
    getStats: (...args: unknown[]) => getStatsMock(...args),
  },
}))

import { useAdminStore } from '@/stores/useAdminStore'

function buildFeedback(overrides: Partial<AdminFeedback> = {}): AdminFeedback {
  return {
    id: 'fb-1',
    readingId: 'r-1',
    userId: 'u-1',
    userEmail: 'a@b.com',
    question: 'What now?',
    selfReport: null,
    aiScore: 5,
    aiScoreReason: null,
    isSincere: true,
    scheduledAt: '2026-04-19T10:00:00Z',
    notifiedAt: null,
    answeredAt: null,
    status: FeedbackStatus.Pending,
    createdAt: '2026-04-19T09:00:00Z',
    ...overrides,
  }
}

describe('useAdminStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    listMock.mockReset()
    createMock.mockReset()
    createSyntheticMock.mockReset()
    updateMock.mockReset()
    deleteMock.mockReset()
    getStatsMock.mockReset()
    readingsMock.mockReset()
    userDetailMock.mockReset()
    usersMock.mockReset()
    subscriptionMock.mockReset()
  })

  afterEach(() => vi.useRealTimers())

  it('loadFeedbacks populates list and total', async () => {
    listMock.mockResolvedValue({ items: [buildFeedback()], total: 1 })
    const store = useAdminStore()
    await store.loadFeedbacks()
    expect(store.feedbacks).toHaveLength(1)
    expect(store.feedbackTotal).toBe(1)
    expect(store.feedbackError).toBeNull()
  })

  it('searches messages from the first page and ignores an older response', async () => {
    let finishOld!: (value: unknown) => void
    readingsMock.mockImplementationOnce(() => new Promise(resolve => { finishOld = resolve }))
    readingsMock.mockResolvedValueOnce({ items: [adminReadingFixtures[1]], total: 1 })
    const store = useAdminStore()
    store.setReadingPage(2)
    const older = store.loadReadings()
    store.setReadingSearch('  сложного разговора  ')
    await store.loadReadings()
    expect(readingsMock).toHaveBeenLastCalledWith({ search: 'сложного разговора', audience: 'all', page: 1, pageSize: 20, includeInterpretation: false }, expect.any(AbortSignal))
    finishOld({ items: [adminReadingFixtures[0]], total: 21 })
    await older
    expect(store.readings[0].id).toBe(adminReadingFixtures[1].id)
    expect(store.readingTotal).toBe(1)
  })

  it('does not restore message contents after the account session ends', async () => {
    let finish!: (value: unknown) => void
    readingsMock.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const store = useAdminStore()
    const loading = store.loadReadings()
    clearAccountSession()
    finish({ items: adminReadingFixtures, total: 21 })
    await loading
    expect(store.readings).toEqual([])
    expect(store.readingLoading).toBe(false)
  })

  it('clears stale message results when a new search fails', async () => {
    readingsMock.mockResolvedValueOnce({ items: adminReadingFixtures, total: 21 })
    readingsMock.mockRejectedValueOnce(new Error('network down'))
    const store = useAdminStore()
    await store.loadReadings()
    store.setReadingSearch('new query')
    await store.loadReadings()
    expect(store.readings).toEqual([])
    expect(store.readingError).toBeTruthy()
    expect(store.readingLoading).toBe(false)
  })

  it('keeps the visible page while loading and rolls back its page number on failure', async () => {
    readingsMock.mockResolvedValueOnce({ items: [adminReadingFixtures[0]], total: 21 })
    const store = useAdminStore()
    await store.loadReadings()
    let reject!: (error: Error) => void
    readingsMock.mockImplementationOnce(() => new Promise((_, fail) => { reject = fail }))
    store.setReadingPage(2)
    const next = store.loadReadings()
    expect(store.readings[0].id).toBe(adminReadingFixtures[0].id)
    expect(store.readingDisplayedPage).toBe(1)
    reject(new Error('network'))
    await next
    expect(store.readingPage).toBe(1)
    expect(store.readings).toHaveLength(1)
    expect(store.readingError).toBeTruthy()
  })

  it('reuses recent pages, expires them, and bypasses the cache on refresh and account change', async () => {
    vi.useFakeTimers()
    readingsMock.mockResolvedValue({ items: [adminReadingFixtures[0]], total: 21 })
    const store = useAdminStore()
    await store.loadReadings()
    store.setReadingPage(2)
    await store.loadReadings()
    store.setReadingPage(1)
    await store.loadReadings()
    expect(readingsMock).toHaveBeenCalledTimes(2)
    vi.advanceTimersByTime(30_001)
    await store.loadReadings()
    expect(readingsMock).toHaveBeenCalledTimes(3)
    await store.loadReadings(true)
    expect(readingsMock).toHaveBeenCalledTimes(4)
    clearAccountSession()
    await store.loadReadings()
    expect(readingsMock).toHaveBeenCalledTimes(5)
  })

  it('aborts the previous request and empties caches after subscription edits', async () => {
    readingsMock.mockResolvedValue({ items: [adminReadingFixtures[0]], total: 21 })
    usersMock.mockResolvedValue({ items: [adminUserDetailFixture], total: 1 })
    subscriptionMock.mockResolvedValue({ ...adminUserDetailFixture, subscription: { status: 0, isActive: false, expiresAt: null, lastPurchase: null } })
    const store = useAdminStore()
    await store.loadReadings()
    const signal = readingsMock.mock.calls[0][1] as AbortSignal
    await store.loadReadings()
    expect(signal.aborted).toBe(true)
    await store.loadUsers()
    await store.setUserSubscription(adminUserDetailFixture.id, { status: 0 })
    expect(store.readings[0].subscription?.isActive).toBe(false)
    await store.loadReadings()
    await store.loadUsers()
    expect(readingsMock).toHaveBeenCalledTimes(2)
    expect(usersMock).toHaveBeenCalledTimes(2)
  })

  it('switches audience from page two without losing search and ignores the aborted response', async () => {
    const store = useAdminStore()
    store.setReadingSearch('  разговор  ')
    readingsMock.mockResolvedValueOnce({ items: [adminReadingFixtures[0]], total: 41 })
    await store.loadReadings()
    let finishOld!: (value: unknown) => void
    readingsMock.mockImplementationOnce(() => new Promise(resolve => { finishOld = resolve }))
    store.setReadingPage(2)
    const old = store.loadReadings()
    const signal = readingsMock.mock.calls[1][1] as AbortSignal
    store.setReadingAudience('subscribers')
    expect(signal.aborted).toBe(true)
    expect(store.readings).toEqual([])
    expect(store.readingTotal).toBe(0)
    expect(store.readingPage).toBe(1)
    expect(store.readingDisplayedPage).toBe(1)
    expect(store.readingSearch).toBe('разговор')
    readingsMock.mockResolvedValueOnce({ items: [adminReadingFixtures[1]], total: 1 })
    await store.loadReadings()
    expect(readingsMock).toHaveBeenLastCalledWith({ search: 'разговор', audience: 'subscribers', page: 1, pageSize: 20, includeInterpretation: false }, expect.any(AbortSignal))
    finishOld({ items: [adminReadingFixtures[0]], total: 41 })
    await old
    expect(store.readings).toEqual([adminReadingFixtures[1]])
    expect(store.readingTotal).toBe(1)
  })

  it('never shows previous-audience rows when the new filter fails', async () => {
    readingsMock.mockResolvedValueOnce({ items: [adminReadingFixtures[0]], total: 41 })
    const store = useAdminStore()
    await store.loadReadings()
    store.setReadingAudience('guests')
    readingsMock.mockRejectedValueOnce(new Error('network'))
    await store.loadReadings()
    expect(store.readingAudience).toBe('guests')
    expect(store.readings).toEqual([])
    expect(store.readingTotal).toBe(0)
    expect(store.readingError).toBe('network')
    expect(store.readingLoading).toBe(false)
    expect(store.readingPage).toBe(1)
  })

  it('reuses pages within an audience but invalidates them when switching audiences', async () => {
    readingsMock.mockResolvedValue({ items: [adminReadingFixtures[0]], total: 41 })
    const store = useAdminStore()
    await store.loadReadings()
    store.setReadingAudience('registered')
    await store.loadReadings()
    store.setReadingPage(2)
    await store.loadReadings()
    store.setReadingPage(1)
    await store.loadReadings()
    expect(readingsMock).toHaveBeenCalledTimes(3)
    store.setReadingAudience('all')
    await store.loadReadings()
    expect(readingsMock).toHaveBeenCalledTimes(4)
    expect(readingsMock.mock.calls.map(call => call[0].audience)).toEqual(['all', 'registered', 'registered', 'all'])
  })

  it('resets the audience on account change and rejects an old filtered response', async () => {
    let finish!: (value: unknown) => void
    readingsMock.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    const store = useAdminStore()
    store.setReadingSearch('private search')
    store.setReadingAudience('subscribers')
    const pending = store.loadReadings()
    clearAccountSession()
    finish({ items: [adminReadingFixtures[0]], total: 1 })
    await pending
    expect(store.readingAudience).toBe('all')
    expect(store.readingSearch).toBeNull()
    expect(store.readings).toEqual([])
    expect(store.readingLoading).toBe(false)
    readingsMock.mockResolvedValueOnce({ items: [], total: 0 })
    await store.loadReadings()
    expect(readingsMock).toHaveBeenLastCalledWith(expect.objectContaining({ audience: 'all', search: null }), expect.any(AbortSignal))
  })

  it.each([false, true])('reloads subscriber membership once after access changes to active=%s', async active => {
    const detail = { ...adminUserDetailFixture, subscription: { ...adminUserDetailFixture.subscription, isActive: active } }
    subscriptionMock.mockResolvedValue(detail)
    const expectedRows = active ? [{ ...adminReadingFixtures[0], subscription: detail.subscription }] : []
    readingsMock.mockResolvedValue({ items: expectedRows, total: expectedRows.length })
    const store = useAdminStore()
    store.setReadingAudience('subscribers')
    store.setReadingSearch('вопрос')
    store.readings = [adminReadingFixtures[0]]
    store.readingPage = 2
    store.readingDisplayedPage = 2
    store.readingTotal = 21
    await store.setUserSubscription(detail.id, { status: active ? 1 : 0 })
    expect(readingsMock).toHaveBeenCalledOnce()
    expect(readingsMock).toHaveBeenCalledWith(expect.objectContaining({ audience: 'subscribers', search: 'вопрос', page: 1 }), expect.any(AbortSignal))
    expect(store.readingPage).toBe(1)
    expect(store.readingTotal).toBe(expectedRows.length)
    expect(store.readings).toEqual(expectedRows)
  })

  it('clears stale subscriber rows if the post-mutation reload fails', async () => {
    subscriptionMock.mockResolvedValue({ ...adminUserDetailFixture, subscription: { ...adminUserDetailFixture.subscription, isActive: false } })
    readingsMock.mockRejectedValue(new Error('network'))
    const store = useAdminStore()
    store.setReadingAudience('subscribers')
    store.readings = [adminReadingFixtures[0]]
    store.readingTotal = 1
    expect(await store.setUserSubscription(adminUserDetailFixture.id, { status: 0 })).toBe(true)
    expect(store.readings).toEqual([])
    expect(store.readingTotal).toBe(0)
    expect(store.readingError).toBe('network')
    expect(readingsMock).toHaveBeenCalledOnce()
  })

  it('keeps the latest selected user when requests finish out of order', async () => {
    let finishFirst!: (value: unknown) => void
    userDetailMock.mockImplementationOnce(() => new Promise(resolve => { finishFirst = resolve }))
    userDetailMock.mockResolvedValueOnce({ ...adminUserDetailFixture, id: 'new-user' })
    const store = useAdminStore()
    const first = store.loadUserDetail('old-user')
    await store.loadUserDetail('new-user')
    finishFirst(adminUserDetailFixture)
    await first
    expect(store.selectedUser?.id).toBe('new-user')
  })

  it('does not reopen a closed user detail after its request completes', async () => {
    let finish!: (value: unknown) => void
    userDetailMock.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const store = useAdminStore()
    const loading = store.loadUserDetail('user')
    store.clearUserDetail()
    finish(adminUserDetailFixture)
    await loading
    expect(store.selectedUser).toBeNull()
    expect(store.selectedUserLoading).toBe(false)
  })

  it('loadFeedbacks captures errors', async () => {
    listMock.mockRejectedValue(new Error('network down'))
    const store = useAdminStore()
    await store.loadFeedbacks()
    expect(store.feedbacks).toEqual([])
    expect(store.feedbackError).toBeTruthy()
  })

  it('setFeedbackUserFilter trims and resets to page 1', () => {
    const store = useAdminStore()
    store.setFeedbackPage(3)
    store.setFeedbackUserFilter('  user-id  ')
    expect(store.feedbackUserFilter).toBe('user-id')
    expect(store.feedbackPage).toBe(1)
  })

  it('updateFeedback patches the local row in place', async () => {
    listMock.mockResolvedValue({ items: [buildFeedback()], total: 1 })
    updateMock.mockResolvedValue(buildFeedback({ aiScore: 9, status: FeedbackStatus.Scored }))
    const store = useAdminStore()
    await store.loadFeedbacks()
    await store.updateFeedback('fb-1', { aiScore: 9, status: FeedbackStatus.Scored })
    expect(store.feedbacks[0].aiScore).toBe(9)
    expect(store.feedbacks[0].status).toBe(FeedbackStatus.Scored)
    expect(store.feedbackToast).toBe('Сохранено')
  })

  it('deleteFeedback removes the row and decrements total', async () => {
    listMock.mockResolvedValue({ items: [buildFeedback()], total: 1 })
    deleteMock.mockResolvedValue(undefined)
    const store = useAdminStore()
    await store.loadFeedbacks()
    const ok = await store.deleteFeedback('fb-1')
    expect(ok).toBe(true)
    expect(store.feedbacks).toHaveLength(0)
    expect(store.feedbackTotal).toBe(0)
  })


  it('loadStats populates stats and clears error', async () => {
    getStatsMock.mockResolvedValue({
      totalUsers: 10,
      adminCount: 2,
      activeSubscriptions: 3,
      readingsToday: 5,
      readingsThisWeek: 25,
      scoredFeedbacksThisMonth: 12,
    })
    const store = useAdminStore()
    await store.loadStats()
    expect(store.stats?.totalUsers).toBe(10)
    expect(store.stats?.scoredFeedbacksThisMonth).toBe(12)
    expect(store.statsError).toBeNull()
  })

  it('loadStats captures errors', async () => {
    getStatsMock.mockRejectedValue(new Error('boom'))
    const store = useAdminStore()
    await store.loadStats()
    expect(store.stats).toBeNull()
    expect(store.statsError).toBeTruthy()
  })
})
