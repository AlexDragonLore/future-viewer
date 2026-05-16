<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import gsap from 'gsap'

const props = withDefaults(defineProps<{ active?: boolean }>(), {
  active: true,
})

const blob1 = ref<SVGCircleElement | null>(null)
const blob2 = ref<SVGCircleElement | null>(null)
const blob3 = ref<SVGCircleElement | null>(null)
let tweens: gsap.core.Tween[] = []

function stopMist() {
  tweens.forEach((tween) => tween.kill())
  tweens = []
}

function startMist() {
  stopMist()
  if (!props.active) return

  if (blob1.value) {
    tweens.push(gsap.to(blob1.value, { attr: { cx: '+=80', cy: '+=40' }, duration: 24, repeat: -1, yoyo: true, ease: 'sine.inOut' }))
  }
  if (blob2.value) {
    tweens.push(gsap.to(blob2.value, { attr: { cx: '-=60', cy: '+=50' }, duration: 22, repeat: -1, yoyo: true, ease: 'sine.inOut' }))
  }
  if (blob3.value) {
    tweens.push(gsap.to(blob3.value, { attr: { cx: '+=40', cy: '-=60' }, duration: 28, repeat: -1, yoyo: true, ease: 'sine.inOut' }))
  }
}

onMounted(() => {
  startMist()
  watch(
    () => props.active,
    () => startMist(),
  )
})

onBeforeUnmount(() => stopMist())
</script>

<template>
  <svg
    class="mist-layer fixed inset-0 w-full h-full pointer-events-none z-0"
    :class="{ 'is-inactive': !active }"
    preserveAspectRatio="xMidYMid slice"
    viewBox="0 0 1920 1080"
  >
    <defs>
      <filter id="mistBlur">
        <feGaussianBlur stdDeviation="120" />
      </filter>
      <radialGradient id="g1" cx="50%" cy="50%">
        <stop offset="0%" stop-color="#6d28d9" stop-opacity="0.5" />
        <stop offset="100%" stop-color="#6d28d9" stop-opacity="0" />
      </radialGradient>
      <radialGradient id="g2" cx="50%" cy="50%">
        <stop offset="0%" stop-color="#7c3aed" stop-opacity="0.35" />
        <stop offset="100%" stop-color="#7c3aed" stop-opacity="0" />
      </radialGradient>
      <radialGradient id="g3" cx="50%" cy="50%">
        <stop offset="0%" stop-color="#f5c26b" stop-opacity="0.12" />
        <stop offset="100%" stop-color="#f5c26b" stop-opacity="0" />
      </radialGradient>
    </defs>
    <g filter="url(#mistBlur)">
      <circle ref="blob1" cx="400" cy="300" r="340" fill="url(#g1)" />
      <circle ref="blob2" cx="1500" cy="700" r="420" fill="url(#g2)" />
      <circle ref="blob3" cx="960" cy="540" r="280" fill="url(#g3)" />
    </g>
  </svg>
</template>

<style scoped>
.mist-layer {
  opacity: 0.34;
  transition: opacity 0.25s ease;
}

.mist-layer.is-inactive {
  opacity: 0;
}
</style>
