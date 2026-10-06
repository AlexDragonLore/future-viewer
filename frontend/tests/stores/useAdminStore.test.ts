import { describe, it, expect, beforeEach, vi } from 'vitest'
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

vi.mock('@/api/adminApi', () => ({
  adminApi: {
    listReadings: (...args: unknown[]) => readingsMock(...args),
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
  })

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
    expect(readingsMock).toHaveBeenLastCalledWith({ search: 'сложного разговора', page: 1, pageSize: 20 })
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
