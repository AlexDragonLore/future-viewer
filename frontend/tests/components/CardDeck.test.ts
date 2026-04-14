import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import CardDeck from '@/components/cards/CardDeck.vue'

describe('CardDeck', () => {
  it('emits shuffle on click', async () => {
    const wrapper = mount(CardDeck)
    await wrapper.trigger('click')
    expect(wrapper.emitted('shuffle')).toHaveLength(1)
  })

  it('does not emit shuffle when disabled', async () => {
    const wrapper = mount(CardDeck, { props: { disabled: true } })
    await wrapper.trigger('click')
    expect(wrapper.emitted('shuffle')).toBeUndefined()
  })

  it('applies size prop to dimensions', () => {
    const wrapper = mount(CardDeck, { props: { size: 200 } })
    const btn = wrapper.element as HTMLElement
    expect(btn.style.width).toBe('200px')
    expect(btn.style.height).toBe('330px')
  })

  it('defaults to 140px when size is not provided', () => {
    const wrapper = mount(CardDeck)
    const btn = wrapper.element as HTMLElement
    expect(btn.style.width).toBe('140px')
  })
})
