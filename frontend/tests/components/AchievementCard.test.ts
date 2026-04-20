import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import AchievementCard from '@/components/AchievementCard.vue'
import type { AchievementInfo } from '@/types'
import { ACHIEVEMENT_ICONS } from '@/data/achievementIcons'

function base(overrides: Partial<AchievementInfo> = {}): AchievementInfo {
  return {
    id: 'a1',
    code: 'first_reading',
    name: 'Первый расклад',
    description: 'Сделайте свой первый расклад',
    iconPath: '/icons/first.svg',
    sortOrder: 0,
    unlockedAt: null,
    ...overrides,
  }
}

describe('AchievementCard', () => {
  it('renders in locked state when unlockedAt is null', () => {
    const wrapper = mount(AchievementCard, { props: { achievement: base() } })
    const card = wrapper.get('[data-testid="achievement-card"]')
    expect(card.classes()).toContain('locked')
    expect(card.classes()).not.toContain('unlocked')
    expect(wrapper.text()).toContain('Ещё не открыто')
    expect(wrapper.text()).not.toContain('Получено')
  })

  it('renders in unlocked state with date when unlockedAt is set', () => {
    const unlockedAt = '2026-03-04T10:00:00Z'
    const wrapper = mount(AchievementCard, {
      props: { achievement: base({ unlockedAt }) },
    })
    const card = wrapper.get('[data-testid="achievement-card"]')
    expect(card.classes()).toContain('unlocked')
    expect(card.classes()).not.toContain('locked')
    expect(wrapper.text()).toContain('Получено:')
    expect(wrapper.text()).not.toContain('Ещё не открыто')
    const localDate = new Date(unlockedAt).toLocaleDateString()
    expect(wrapper.text()).toContain(localDate)
  })

  it('shows the title and description from props', () => {
    const wrapper = mount(AchievementCard, {
      props: {
        achievement: base({ name: 'Три дня подряд', description: 'три дня' }),
      },
    })
    expect(wrapper.text()).toContain('Три дня подряд')
    expect(wrapper.text()).toContain('три дня')
  })

  it.each(Object.keys(ACHIEVEMENT_ICONS))(
    'renders a lucide svg icon (not the legacy glyph) for code %s',
    (code) => {
      const wrapper = mount(AchievementCard, {
        props: { achievement: base({ code }) },
      })
      const icon = wrapper.get('[data-testid="achievement-icon"]')
      expect(icon.element.tagName.toLowerCase()).toBe('svg')
      expect(wrapper.text()).not.toContain('✦')
      expect(wrapper.text()).not.toContain('✧')
    },
  )

  it('falls back to a default lucide icon for an unknown code', () => {
    const wrapper = mount(AchievementCard, {
      props: { achievement: base({ code: 'unknown_code' }) },
    })
    const icon = wrapper.get('[data-testid="achievement-icon"]')
    expect(icon.element.tagName.toLowerCase()).toBe('svg')
  })
})
