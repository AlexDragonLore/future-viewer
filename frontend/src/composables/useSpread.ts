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
      if (boardWidth <= 420) return Math.max(72, Math.min(120, (boardWidth - 36) / 3))
      return Math.max(72, Math.min(140, boardWidth / 3.6))
    case SpreadType.CelticCross:
      if (boardWidth <= 420) return Math.max(48, Math.min(68, (boardWidth - 52) / 4))
      return Math.max(48, Math.min(140, boardWidth / 7.5))
    default:
      return 140
  }
}

export function computeSlots(type: SpreadType, boardWidth: number, boardHeight: number): SlotLayout[] {
  const cx = boardWidth / 2
  const cy = boardHeight / 2
  const topSafe = Math.max(96, boardHeight * 0.18)
  const bottomSafe = Math.max(72, boardHeight * 0.12)
  const usableTop = topSafe
  const usableBottom = Math.max(usableTop + 120, boardHeight - bottomSafe)
  const usableCenter = (usableTop + usableBottom) / 2
  const layoutCenterY = boardWidth <= 640 ? usableCenter : cy

  switch (type) {
    case SpreadType.SingleCard:
      return [{ x: cx, y: layoutCenterY }]

    case SpreadType.ThreeCard: {
      const cardW = computeCardWidth(type, boardWidth)
      const gap = boardWidth <= 420 ? Math.min(cardW + 8, (boardWidth - cardW) / 2) : Math.min(220, boardWidth / 4)
      return [
        { x: cx - gap, y: layoutCenterY },
        { x: cx, y: layoutCenterY },
        { x: cx + gap, y: layoutCenterY },
      ]
    }

    case SpreadType.CelticCross: {
      if (boardWidth <= 420) {
        const cardW = computeCardWidth(type, boardWidth)
        const colGap = cardW + 8
        const rowGap = cardW * 1.65 + 10
        const startX = cx - colGap * 1.5
        const startY = usableTop + cardW * 0.9
        return Array.from({ length: 10 }, (_, i) => ({
          x: startX + (i % 4) * colGap,
          y: startY + Math.floor(i / 4) * rowGap,
        }))
      }
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
