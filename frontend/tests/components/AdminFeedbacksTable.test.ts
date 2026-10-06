import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminStore } from '@/stores/useAdminStore'
import { FeedbackStatus } from '@/types'
import type { AdminFeedback } from '@/types/admin'
import AdminFeedbacksTable from '@/components/admin/AdminFeedbacksTable.vue'

const feedback: AdminFeedback = {
  id: 'feedback-1', readingId: 'reading-1', userId: 'user-1', userEmail: 'reader@example.com',
  question: 'Какой следующий шаг мне подходит?', selfReport: 'Попробовал новый подход и заметил результат.',
  aiScore: 7, aiScoreReason: 'Описано конкретное действие.', isSincere: null,
  scheduledAt: '2026-10-05T10:00:00Z', notifiedAt: null, answeredAt: '2026-10-06T10:00:00Z',
  status: FeedbackStatus.Scored, createdAt: '2026-10-05T10:00:00Z',
}

describe('AdminFeedbacksTable', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useAdminStore().feedbacks = [{ ...feedback }]
  })

  it('shows full review content while leaving editing controls closed', () => {
    const wrapper = mount(AdminFeedbacksTable)
    const content = wrapper.get('[data-testid="admin-feedback-content"]')
    expect(content.text()).toContain(feedback.question)
    expect(content.text()).toContain(feedback.selfReport)
    expect(content.text()).toContain(feedback.aiScoreReason)
    expect(wrapper.find('[data-testid="admin-feedback-score-input"]').exists()).toBe(false)
  })

  it('discards an unsaved edit when editing is cancelled and reopened', async () => {
    const wrapper = mount(AdminFeedbacksTable)
    await wrapper.get('[data-testid="admin-feedback-edit"]').trigger('click')
    await wrapper.get('[data-testid="admin-feedback-score-input"]').setValue(9)
    await wrapper.get('[data-testid="admin-feedback-edit"]').trigger('click')
    await wrapper.get('[data-testid="admin-feedback-edit"]').trigger('click')
    expect((wrapper.get('[data-testid="admin-feedback-score-input"]').element as HTMLInputElement).value).toBe('7')
  })

  it('preserves null sincerity and prevents duplicate saves while awaiting the server', async () => {
    const store = useAdminStore()
    let finish!: (result: AdminFeedback) => void
    const update = vi.spyOn(store, 'updateFeedback').mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const wrapper = mount(AdminFeedbacksTable)
    await wrapper.get('[data-testid="admin-feedback-edit"]').trigger('click')
    await wrapper.get('[data-testid="admin-feedback-score-input"]').setValue(9)
    await wrapper.get('[data-testid="admin-feedback-save"]').trigger('click')
    await wrapper.get('[data-testid="admin-feedback-save"]').trigger('click')
    expect(update).toHaveBeenCalledTimes(1)
    expect(update).toHaveBeenCalledWith(feedback.id, { aiScore: 9, status: FeedbackStatus.Scored, isSincere: null })
    finish({ ...feedback, aiScore: 9 })
    await flushPromises()
    expect(wrapper.find('[data-testid="admin-feedback-save"]').exists()).toBe(false)
  })

  it('does not delete a review when confirmation is cancelled', async () => {
    const remove = vi.spyOn(useAdminStore(), 'deleteFeedback').mockResolvedValue(true)
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(AdminFeedbacksTable)
    await wrapper.get('[data-testid="admin-feedback-delete"]').trigger('click')
    expect(remove).not.toHaveBeenCalled()
  })
})
