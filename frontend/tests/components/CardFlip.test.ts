import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import CardFlip from '@/components/cards/CardFlip.vue'
import type { ReadingCard } from '@/types'

function makeCard(overrides: Partial<ReadingCard> = {}): ReadingCard {
  return {
    position: 0,
    positionName: 'Past',
    positionMeaning: 'Where you come from',
    cardId: 1,
    cardName: 'The Fool',
    imagePath: '/img/fool.png',
    isReversed: false,
    meaning: 'New beginnings',
    ...overrides,
  }
}

describe('CardFlip', () => {
  it('renders without a card (loading placeholder)', () => {
    const wrapper = mount(CardFlip, { props: { faceUp: false, width: 140 } })
    expect(wrapper.find('.card-back').exists()).toBe(true)
    expect(wrapper.find('.card-front-inner').exists()).toBe(false)
  })

  it('shows card name and orientation when face up', () => {
    const wrapper = mount(CardFlip, { props: { card: makeCard(), faceUp: true } })
    expect(wrapper.text()).toContain('The Fool')
    expect(wrapper.text()).toContain('прямая')
    expect(wrapper.find('.is-flipped').exists()).toBe(true)
  })

  it('marks reversed cards and sets rotation CSS var', () => {
    const wrapper = mount(CardFlip, { props: { card: makeCard({ isReversed: true }), faceUp: true } })
    expect(wrapper.text()).toContain('перевёрнутая')
    const root = wrapper.find('.card-flip').element as HTMLElement
    expect(root.style.getPropertyValue('--card-rotation')).toBe('180deg')
  })

  it('honors width prop for dimensions', () => {
    const wrapper = mount(CardFlip, { props: { faceUp: false, width: 200 } })
    const root = wrapper.find('.card-flip').element as HTMLElement
    expect(root.style.width).toBe('200px')
    expect(root.style.height).toBe('330px')
  })

  it('is not flipped when faceUp is false', () => {
    const wrapper = mount(CardFlip, { props: { card: makeCard(), faceUp: false } })
    expect(wrapper.find('.is-flipped').exists()).toBe(false)
  })
})
