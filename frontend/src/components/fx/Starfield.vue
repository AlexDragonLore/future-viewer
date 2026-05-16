<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

const props = withDefaults(defineProps<{ active?: boolean }>(), {
  active: true,
})

const canvas = ref<HTMLCanvasElement | null>(null)
let rafId = 0
let ctx: CanvasRenderingContext2D | null = null
let dpr = 1
let w = 0
let h = 0
const stars: Star[] = []

interface Star {
  x: number
  y: number
  z: number
  size: number
  twinklePhase: number
}

function cancelFrame() {
  if (rafId) cancelAnimationFrame(rafId)
  rafId = 0
}

function clearCanvas() {
  const el = canvas.value
  if (!el || !ctx) return
  ctx.clearRect(0, 0, w, h)
}

function resize() {
  const el = canvas.value
  if (!el || !ctx) return

  dpr = Math.min(window.devicePixelRatio || 1, 1.5)
  w = window.innerWidth
  h = window.innerHeight
  el.width = Math.ceil(w * dpr)
  el.height = Math.ceil(h * dpr)
  el.style.width = w + 'px'
  el.style.height = h + 'px'
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)

  stars.length = 0
  const count = Math.min(160, Math.floor((w * h) / 12000))
  for (let i = 0; i < count; i++) {
    stars.push({
      x: Math.random() * w,
      y: Math.random() * h,
      z: Math.random() * 0.8 + 0.2,
      size: Math.random() * 1.4 + 0.25,
      twinklePhase: Math.random() * Math.PI * 2,
    })
  }
}

let mouseX = 0
let mouseY = 0
const onMouseMove = (e: MouseEvent) => {
  if (!w || !h) return
  mouseX = (e.clientX / w - 0.5) * 14
  mouseY = (e.clientY / h - 0.5) * 14
}

function frame(t: number) {
  if (!ctx || !props.active) return

  ctx.clearRect(0, 0, w, h)
  ctx.shadowBlur = 3
  ctx.shadowColor = 'rgba(245, 194, 107, 0.45)'
  for (const s of stars) {
    const twinkle = 0.65 + 0.35 * Math.sin(t * 0.0016 + s.twinklePhase)
    const px = s.x + mouseX * s.z
    const py = s.y + mouseY * s.z
    ctx.beginPath()
    ctx.arc(px, py, s.size * s.z, 0, Math.PI * 2)
    ctx.fillStyle = `rgba(232, 213, 242, ${twinkle * s.z})`
    ctx.fill()
  }
  ctx.shadowBlur = 0
  rafId = requestAnimationFrame(frame)
}

function start() {
  cancelFrame()
  if (!props.active) {
    clearCanvas()
    return
  }
  resize()
  rafId = requestAnimationFrame(frame)
}

onMounted(() => {
  const el = canvas.value
  if (!el) return
  ctx = el.getContext('2d')
  if (!ctx) return

  window.addEventListener('resize', resize)
  window.addEventListener('mousemove', onMouseMove, { passive: true })
  start()

  watch(
    () => props.active,
    () => start(),
  )
})

onBeforeUnmount(() => {
  cancelFrame()
  if (typeof window !== 'undefined') {
    window.removeEventListener('resize', resize)
    window.removeEventListener('mousemove', onMouseMove)
  }
})
</script>

<template>
  <canvas ref="canvas" class="starfield fixed inset-0 pointer-events-none z-0" :class="{ 'is-inactive': !active }" />
</template>

<style scoped>
.starfield {
  opacity: 1;
  transition: opacity 0.25s ease;
}

.starfield.is-inactive {
  opacity: 0;
}
</style>
