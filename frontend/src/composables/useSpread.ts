import { SpreadType } from '@/types'

export interface SlotLayout {
  x: number
  y: number
}

export function computeSlots(type: SpreadType, boardWidth: number, boardHeight: number): SlotLayout[] {
  const cx = boardWidth / 2
  const cy = boardHeight / 2

  switch (type) {
    case SpreadType.SingleCard:
      return [{ x: cx, y: cy }]

    case SpreadType.ThreeCard: {
      const gap = Math.min(220, boardWidth / 4)
      return [
        { x: cx - gap, y: cy },
        { x: cx, y: cy },
        { x: cx + gap, y: cy },
      ]
    }

    case SpreadType.CelticCross: {
      const unit = Math.min(170, boardWidth / 8)
      return [
        { x: cx - unit, y: cy },
        { x: cx - unit, y: cy },
        { x: cx - unit, y: cy + unit * 1.3 },
        { x: cx - unit * 2.3, y: cy },
        { x: cx - unit, y: cy - unit * 1.3 },
        { x: cx + 0.3 * unit, y: cy },
        { x: cx + unit * 2, y: cy + unit * 1.3 },
        { x: cx + unit * 2, y: cy + unit * 0.4 },
        { x: cx + unit * 2, y: cy - unit * 0.5 },
        { x: cx + unit * 2, y: cy - unit * 1.4 },
      ]
    }

    default:
      return [{ x: cx, y: cy }]
  }
}
