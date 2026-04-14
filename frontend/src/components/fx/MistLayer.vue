<script setup lang="ts">
import { onMounted, ref } from 'vue'
import gsap from 'gsap'

const blob1 = ref<SVGCircleElement | null>(null)
const blob2 = ref<SVGCircleElement | null>(null)
const blob3 = ref<SVGCircleElement | null>(null)

onMounted(() => {
  if (blob1.value) {
    gsap.to(blob1.value, { attr: { cx: '+=80', cy: '+=40' }, duration: 22, repeat: -1, yoyo: true, ease: 'sine.inOut' })
  }
  if (blob2.value) {
    gsap.to(blob2.value, { attr: { cx: '-=60', cy: '+=50' }, duration: 18, repeat: -1, yoyo: true, ease: 'sine.inOut' })
  }
  if (blob3.value) {
    gsap.to(blob3.value, { attr: { cx: '+=40', cy: '-=60' }, duration: 26, repeat: -1, yoyo: true, ease: 'sine.inOut' })
  }
})
</script>

<template>
  <svg
    class="fixed inset-0 w-full h-full pointer-events-none z-0 opacity-40"
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
