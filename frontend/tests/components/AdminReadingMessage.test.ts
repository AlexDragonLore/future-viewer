import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import AdminReadingMessage from '@/components/admin/AdminReadingMessage.vue'
import { adminReadingFixtures } from '../fixtures/admin'
import { clearAccountSession } from '@/utils/accountSession'
const fetchAnswer = vi.hoisted(() => vi.fn())
vi.mock('@/api/adminApi', () => ({ adminApi: { getReadingInterpretation: fetchAnswer } }))
const reading = { ...adminReadingFixtures[0], interpretation: undefined, hasInterpretation: true }

describe('AdminReadingMessage', () => {
  beforeEach(() => { fetchAnswer.mockReset() })
  it('fetches and renders an answer only when opened, reuses it, and shows the full question immediately', async () => {
    fetchAnswer.mockResolvedValue({ id: reading.id, interpretation: '**Полный ответ**' })
    const wrapper = mount(AdminReadingMessage, { props: { reading } })
    expect(fetchAnswer).not.toHaveBeenCalled()
    expect(wrapper.get('[data-testid="admin-reading-expand"]').attributes('aria-label')).toBe('Показать ответ ИИ')
    expect(wrapper.find('[data-testid="admin-reading-answer"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="admin-question-expand"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="admin-reading-question"]').text()).toBe(reading.question)
    expect(wrapper.get('[data-testid="admin-reading-question"]').classes()).not.toContain('collapsed')
    await wrapper.get('[data-testid="admin-reading-expand"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="admin-reading-answer"] strong').text()).toBe('Полный ответ')
    await wrapper.get('[data-testid="admin-reading-expand"]').trigger('click')
    expect(wrapper.find('[data-testid="admin-reading-answer"]').exists()).toBe(false)
    await wrapper.get('[data-testid="admin-reading-expand"]').trigger('click')
    expect(fetchAnswer).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })
  it('does not render or request an embedded answer until expanded', async () => {
    const wrapper = mount(AdminReadingMessage, { props: { reading: adminReadingFixtures[0] } })
    expect(wrapper.find('[data-testid="admin-reading-answer"]').exists()).toBe(false)
    await wrapper.get('[data-testid="admin-reading-expand"]').trigger('click')
    expect(wrapper.find('[data-testid="admin-reading-answer"]').exists()).toBe(true)
    expect(fetchAnswer).not.toHaveBeenCalled()
    wrapper.unmount()
  })
  it('discards a pending private answer on logout', async () => {
    let finish!: (value: unknown) => void
    fetchAnswer.mockImplementation(() => new Promise(resolve => { finish = resolve }))
    const wrapper = mount(AdminReadingMessage, { props: { reading } })
    await wrapper.get('[data-testid="admin-reading-expand"]').trigger('click')
    const signal = fetchAnswer.mock.calls[0][1] as AbortSignal
    clearAccountSession()
    finish({ id: reading.id, interpretation: 'private answer' })
    await flushPromises()
    expect(signal.aborted).toBe(true)
    expect(wrapper.text()).not.toContain('private answer')
    expect(wrapper.find('[data-testid="admin-reading-answer"]').exists()).toBe(false)
    wrapper.unmount()
  })
  it('offers a retry after failure and ignores an answer for the previous row', async () => {
    fetchAnswer.mockRejectedValueOnce(new Error('network'))
    const wrapper = mount(AdminReadingMessage, { props: { reading } })
    await wrapper.get('[data-testid="admin-reading-expand"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    let finish!: (value: unknown) => void
    fetchAnswer.mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
    await wrapper.get('[role="alert"] button').trigger('click')
    await wrapper.setProps({ reading: { ...reading, id: 'another' } })
    finish({ id: reading.id, interpretation: 'previous answer' })
    await flushPromises()
    expect(wrapper.text()).not.toContain('previous answer')
    wrapper.unmount()
  })
})
