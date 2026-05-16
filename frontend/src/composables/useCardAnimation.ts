import gsap from 'gsap'

export interface DealTarget {
  el: HTMLElement
  to: { x: number; y: number }
  from: { x: number; y: number }
}

export function dealCards(targets: DealTarget[], onComplete?: () => void): gsap.core.Timeline {
  const tl = gsap.timeline({
    onComplete: () => {
      targets.forEach((t) => gsap.set(t.el, { clearProps: 'willChange' }))
      onComplete?.()
    },
    defaults: { force3D: true, overwrite: 'auto' },
  })

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

    const horizontalSpread = Math.min(72, Math.abs(t.to.x - t.from.x) * 0.25 + 18)
    const direction = i % 2 === 0 ? 1 : -1
    const midX = (t.from.x + t.to.x) / 2 + direction * horizontalSpread * 0.5
    const arc = Math.max(48, Math.min(118, Math.abs(t.to.y - t.from.y) * 0.45 + 56))
    const midY = Math.min(t.from.y, t.to.y) - arc

    tl.to(
      t.el,
      {
        keyframes: [
          {
            x: midX,
            y: midY,
            scale: 1.04,
            rotation: direction * 5,
            duration: 0.55,
            ease: 'sine.out',
          },
          {
            x: t.to.x,
            y: t.to.y,
            scale: 1,
            rotation: 0,
            duration: 0.62,
            ease: 'sine.inOut',
          },
        ],
        opacity: 1,
      },
      i * 0.24,
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
  const tl = gsap.timeline({
    onComplete: () => {
      gsap.set(el, { clearProps: 'willChange' })
      onComplete?.()
    },
    defaults: { force3D: true, overwrite: 'auto' },
  })
  tl.to(el, { rotate: 360, duration: 1.55, ease: 'sine.inOut' })
    .to(el, { y: -12, duration: 0.78, yoyo: true, repeat: 1, ease: 'sine.inOut' }, 0)
    .to(el, { scale: 1.045, duration: 0.78, yoyo: true, repeat: 1, ease: 'sine.inOut' }, 0)
  return tl
}
