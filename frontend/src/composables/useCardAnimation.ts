import gsap from 'gsap'

export interface DealTarget {
  el: HTMLElement
  to: { x: number; y: number }
  from: { x: number; y: number }
}

export function dealCards(targets: DealTarget[], onComplete?: () => void): gsap.core.Timeline {
  const tl = gsap.timeline({ onComplete })

  targets.forEach((t, i) => {
    gsap.set(t.el, {
      x: t.from.x,
      y: t.from.y,
      scale: 0.85,
      rotation: 0,
      opacity: 0,
    })

    const midX = (t.from.x + t.to.x) / 2 + (Math.random() - 0.5) * 120
    const midY = Math.min(t.from.y, t.to.y) - 140 - Math.random() * 80

    tl.to(
      t.el,
      {
        keyframes: [
          { x: midX, y: midY, scale: 1.05, rotation: (Math.random() - 0.5) * 18, duration: 0.45, ease: 'power1.out' },
          { x: t.to.x, y: t.to.y, scale: 1, rotation: 0, duration: 0.45, ease: 'power2.in' },
        ],
        opacity: 1,
      },
      i * 0.28,
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
  const tl = gsap.timeline({ onComplete })
  tl.to(el, { rotate: 360, duration: 0.8, ease: 'power2.inOut' })
    .to(el, { scale: 1.08, duration: 0.2, yoyo: true, repeat: 1, ease: 'sine.inOut' }, 0)
  return tl
}
