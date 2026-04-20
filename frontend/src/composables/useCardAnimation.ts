import gsap from 'gsap'

export interface DealTarget {
  el: HTMLElement
  to: { x: number; y: number }
  from: { x: number; y: number }
}

export function dealCards(targets: DealTarget[], onComplete?: () => void): gsap.core.Timeline {
  const tl = gsap.timeline({ onComplete, defaults: { force3D: true } })

  targets.forEach((t, i) => {
    gsap.set(t.el, {
      x: t.from.x,
      y: t.from.y,
      scale: 0.82,
      rotation: 0,
      opacity: 0,
      force3D: true,
      willChange: 'transform, opacity',
    })

    const horizontalSpread = Math.min(100, Math.abs(t.to.x - t.from.x) * 0.4 + 24)
    const midX = (t.from.x + t.to.x) / 2 + (Math.random() - 0.5) * horizontalSpread
    const arc = Math.max(60, Math.min(160, Math.abs(t.to.y - t.from.y) * 0.6 + 80))
    const midY = Math.min(t.from.y, t.to.y) - arc - Math.random() * 40

    tl.to(
      t.el,
      {
        keyframes: [
          {
            x: midX,
            y: midY,
            scale: 1.04,
            rotation: (Math.random() - 0.5) * 14,
            duration: 1.0,
            ease: 'sine.out',
          },
          {
            x: t.to.x,
            y: t.to.y,
            scale: 1,
            rotation: 0,
            duration: 1.0,
            ease: 'sine.inOut',
          },
        ],
        opacity: 1,
      },
      i * 0.55,
    )
  })

  return tl
}

export function flipCards(cards: HTMLElement[]): gsap.core.Timeline {
  const tl = gsap.timeline()
  cards.forEach((el, i) => {
    tl.call(() => el.classList.add('is-flipped'), [], i * 0.35)
  })
  return tl
}

export function shuffleDeck(el: HTMLElement, onComplete?: () => void): gsap.core.Timeline {
  gsap.set(el, { force3D: true, willChange: 'transform', transformOrigin: '50% 50%' })
  const tl = gsap.timeline({ onComplete, defaults: { force3D: true } })
  tl.to(el, { rotate: 360, duration: 2.2, ease: 'sine.inOut' })
    .to(el, { y: -16, duration: 1.1, yoyo: true, repeat: 1, ease: 'sine.inOut' }, 0)
    .to(el, { scale: 1.06, duration: 1.1, yoyo: true, repeat: 1, ease: 'sine.inOut' }, 0)
  return tl
}
