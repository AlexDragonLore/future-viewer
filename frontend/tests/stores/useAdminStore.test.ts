import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { FeedbackStatus } from '@/types'
import type { AdminFeedback } from '@/types/admin'

const listMock = vi.fn()
const createMock = vi.fn()
const createSyntheticMock = vi.fn()
const updateMock = vi.fn()
const deleteMock = vi.fn()
const runNotificationsMock = vi.fn()

vi.mock('@/api/adminApi', () => ({
  adminApi: {
    listFeedbacks: (...args: unknown[]) => listMock(...args),
    createFeedback: (...args: unknown[]) => createMock(...args),
    createSyntheticFeedback: (...args: unknown[]) => createSyntheticMock(...args),
    updateFeedback: (...args: unknown[]) => updateMock(...args),
    deleteFeedback: (...args: unknown[]) => deleteMock(...args),
    runNotifications: (...args: unknown[]) => runNotificationsMock(...args),
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
    token: 'tok-abc',
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
    runNotificationsMock.mockReset()
  })

  it('loadFeedbacks populates list and total', async () => {
    listMock.mockResolvedValue({ items: [buildFeedback()], total: 1 })
    const store = useAdminStore()
    await store.loadFeedbacks()
    expect(store.feedbacks).toHaveLength(1)
    expect(store.feedbackTotal).toBe(1)
    expect(store.feedbackError).toBeNull()
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

  it('runNotifications surfaces processed count', async () => {
    runNotificationsMock.mockResolvedValue({ processed: 4 })
    const store = useAdminStore()
    const result = await store.runNotifications()
    expect(result).toBe(4)
    expect(store.feedbackToast).toContain('4')
  })
})
