<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

const canvas = ref<HTMLCanvasElement | null>(null)
let rafId = 0

interface Star {
  x: number
  y: number
  z: number
  size: number
  twinklePhase: number
}

onMounted(() => {
  const el = canvas.value
  if (!el) return
  const ctx = el.getContext('2d')
  if (!ctx) return

  const dpr = window.devicePixelRatio || 1
  let w = 0
  let h = 0
  const stars: Star[] = []

  function resize() {
    w = window.innerWidth
    h = window.innerHeight
    el!.width = w * dpr
    el!.height = h * dpr
    el!.style.width = w + 'px'
    el!.style.height = h + 'px'
    ctx!.scale(dpr, dpr)

    stars.length = 0
    const count = Math.min(220, Math.floor((w * h) / 9000))
    for (let i = 0; i < count; i++) {
      stars.push({
        x: Math.random() * w,
        y: Math.random() * h,
        z: Math.random() * 0.8 + 0.2,
        size: Math.random() * 1.6 + 0.3,
        twinklePhase: Math.random() * Math.PI * 2,
      })
    }
  }

  resize()
  window.addEventListener('resize', resize)

  let mouseX = 0
  let mouseY = 0
  const onMouseMove = (e: MouseEvent) => {
    mouseX = (e.clientX / w - 0.5) * 20
    mouseY = (e.clientY / h - 0.5) * 20
  }
  window.addEventListener('mousemove', onMouseMove)

  function frame(t: number) {
    ctx!.clearRect(0, 0, w, h)
    for (const s of stars) {
      const twinkle = 0.6 + 0.4 * Math.sin(t * 0.002 + s.twinklePhase)
      const px = s.x + mouseX * s.z
      const py = s.y + mouseY * s.z
      ctx!.beginPath()
      ctx!.arc(px, py, s.size * s.z, 0, Math.PI * 2)
      ctx!.fillStyle = `rgba(232, 213, 242, ${twinkle * s.z})`
      ctx!.shadowBlur = 8 * s.z
      ctx!.shadowColor = 'rgba(245, 194, 107, 0.6)'
      ctx!.fill()
    }
    ctx!.shadowBlur = 0
    rafId = requestAnimationFrame(frame)
  }
  rafId = requestAnimationFrame(frame)

  onBeforeUnmount(() => {
    cancelAnimationFrame(rafId)
    window.removeEventListener('resize', resize)
    window.removeEventListener('mousemove', onMouseMove)
  })
})
</script>

<template>
  <canvas ref="canvas" class="fixed inset-0 pointer-events-none z-0" />
</template>
