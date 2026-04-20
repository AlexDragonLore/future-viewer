import { SpreadType } from '@/types'

export interface SlotLayout {
  x: number
  y: number
}

export function computeCardWidth(type: SpreadType, boardWidth: number): number {
  if (!boardWidth || boardWidth <= 0) return 140
  switch (type) {
    case SpreadType.SingleCard:
      return Math.max(96, Math.min(200, boardWidth * 0.55))
    case SpreadType.ThreeCard:
      return Math.max(72, Math.min(140, boardWidth / 3.6))
    case SpreadType.CelticCross:
      return Math.max(48, Math.min(140, boardWidth / 7.5))
    default:
      return 140
  }
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
