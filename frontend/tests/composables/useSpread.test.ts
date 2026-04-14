import { describe, it, expect } from 'vitest'
import { computeSlots } from '@/composables/useSpread'
import { SpreadType } from '@/types'

describe('computeSlots', () => {
  it('returns one centered slot for single card', () => {
    const slots = computeSlots(SpreadType.SingleCard, 1000, 600)
    expect(slots).toHaveLength(1)
    expect(slots[0]).toEqual({ x: 500, y: 300 })
  })

  it('returns three horizontally spaced slots for three-card spread', () => {
    const slots = computeSlots(SpreadType.ThreeCard, 1200, 800)
    expect(slots).toHaveLength(3)
    expect(slots[1]).toEqual({ x: 600, y: 400 })
    expect(slots[0].x).toBeLessThan(slots[1].x)
    expect(slots[2].x).toBeGreaterThan(slots[1].x)
    const gap1 = slots[1].x - slots[0].x
    const gap2 = slots[2].x - slots[1].x
    expect(gap1).toBeCloseTo(gap2)
  })

  it('returns ten slots for celtic cross', () => {
    const slots = computeSlots(SpreadType.CelticCross, 1600, 900)
    expect(slots).toHaveLength(10)
    for (const s of slots) {
      expect(Number.isFinite(s.x)).toBe(true)
      expect(Number.isFinite(s.y)).toBe(true)
    }
  })

  it('scales three-card gap down on narrow boards', () => {
    const narrow = computeSlots(SpreadType.ThreeCard, 400, 600)
    const gap = narrow[1].x - narrow[0].x
    expect(gap).toBeLessThanOrEqual(220)
    expect(gap).toBeGreaterThan(0)
  })

  it('falls back to a single slot for unknown spread type', () => {
    const slots = computeSlots(999 as unknown as SpreadType, 800, 600)
    expect(slots).toEqual([{ x: 400, y: 300 }])
  })
})
