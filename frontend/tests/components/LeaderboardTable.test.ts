import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import LeaderboardTable from '@/components/LeaderboardTable.vue'
import type { LeaderboardEntry } from '@/types'

function makeEntries(): LeaderboardEntry[] {
  return [
    { userId: 'u1', displayName: 'a***@mail.com', totalScore: 58, feedbackScore: 48, achievementScore: 10, feedbackCount: 8, averageScore: 7.25, rank: 1 },
    { userId: 'u2', displayName: 'b***@mail.com', totalScore: 40, feedbackScore: 40, achievementScore: 0, feedbackCount: 5, averageScore: 8, rank: 2 },
    { userId: 'u3', displayName: 'c***@mail.com', totalScore: 30, feedbackScore: 10, achievementScore: 20, feedbackCount: 5, averageScore: 6, rank: 3 },
    { userId: 'u4', displayName: 'd***@mail.com', totalScore: 20, feedbackScore: 0, achievementScore: 20, feedbackCount: 4, averageScore: 5, rank: 4 },
  ]
}

describe('LeaderboardTable', () => {
  it('renders an empty state when no entries are provided', () => {
    const wrapper = mount(LeaderboardTable, { props: { entries: [] } })
    expect(wrapper.find('[data-testid="leaderboard-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="leaderboard-table"]').exists()).toBe(false)
  })

  it('uses custom empty text when provided', () => {
    const wrapper = mount(LeaderboardTable, {
      props: { entries: [], emptyText: 'никого нет' },
    })
    expect(wrapper.find('[data-testid="leaderboard-empty"]').text()).toContain('никого нет')
  })

  it('renders a row for each entry with all fields', () => {
    const wrapper = mount(LeaderboardTable, { props: { entries: makeEntries() } })
    const rows = wrapper.findAll('[data-testid="leaderboard-row"]')
    expect(rows).toHaveLength(4)
    expect(rows[0].text()).toContain('a***@mail.com')
    expect(rows[0].text()).toContain('58')
    expect(rows[0].text()).toContain('8')
    expect(rows[0].text()).toContain('7.3')
  })

  it('shows medal icons for the top three ranks', () => {
    const wrapper = mount(LeaderboardTable, { props: { entries: makeEntries() } })
    const rows = wrapper.findAll('[data-testid="leaderboard-row"]')
    expect(rows[0].text()).toContain('🥇')
    expect(rows[1].text()).toContain('🥈')
    expect(rows[2].text()).toContain('🥉')
    expect(rows[3].text()).not.toContain('🥇')
    expect(rows[3].text()).toContain('4')
  })

  it('highlights the current user row when highlightUserId matches', () => {
    const wrapper = mount(LeaderboardTable, {
      props: { entries: makeEntries(), highlightUserId: 'u2' },
    })
    const rows = wrapper.findAll('[data-testid="leaderboard-row"]')
    expect(rows[0].classes()).not.toContain('highlight')
    expect(rows[1].classes()).toContain('highlight')
  })

  it('formats the average score with a single decimal place', () => {
    const wrapper = mount(LeaderboardTable, {
      props: {
        entries: [
          { userId: 'u1', displayName: 'x', totalScore: 10, feedbackScore: 10, achievementScore: 0, feedbackCount: 2, averageScore: 7, rank: 1 },
        ],
      },
    })
    expect(wrapper.text()).toContain('7.0')
  })

  it('shows the feedback/achievement score breakdown under the total', () => {
    const wrapper = mount(LeaderboardTable, { props: { entries: makeEntries() } })
    const rows = wrapper.findAll('[data-testid="leaderboard-row"]')
    const breakdowns = rows[0].findAll('[data-testid="score-breakdown"]')
    expect(breakdowns).toHaveLength(1)
    expect(breakdowns[0].text()).toContain('★ 48')
    expect(breakdowns[0].text()).toContain('✦ 10')
  })

  it('renders «Итог» column header', () => {
    const wrapper = mount(LeaderboardTable, { props: { entries: makeEntries() } })
    expect(wrapper.text()).toContain('Итог')
    expect(wrapper.text()).not.toContain('Сумма')
  })
})
