<script setup lang="ts">
import { computed } from 'vue'
import type { ReadingCard } from '@/types'

const props = defineProps<{
  card?: ReadingCard
  faceUp?: boolean
  width?: number
}>()

const w = computed(() => props.width ?? 140)
const h = computed(() => Math.round((props.width ?? 140) * 1.65))

const rotation = computed(() => (props.card?.isReversed ? 180 : 0))
</script>

<template>
  <div
    class="card-flip relative select-none"
    :style="{ width: w + 'px', height: h + 'px', '--card-rotation': rotation + 'deg' }"
  >
    <div class="card-inner" :class="{ 'is-flipped': faceUp }">
      <div class="card-face card-back">
        <div class="card-back-pattern"></div>
        <div class="card-back-emblem">✦</div>
      </div>
      <div class="card-face card-front" :style="{ transform: `rotateY(180deg) rotate(var(--card-rotation))` }">
        <div v-if="card" class="card-front-inner">
          <img
            v-if="card.imagePath"
            :src="card.imagePath"
            :alt="card.cardName"
            class="card-img"
            draggable="false"
          />
          <div v-else class="card-image" aria-hidden="true">✵</div>
          <div class="card-name-overlay font-display">{{ card.cardName }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.card-flip {
  perspective: 1200px;
  will-change: transform;
}

.card-inner {
  position: relative;
  width: 100%;
  height: 100%;
  transform-style: preserve-3d;
  transition: transform 0.8s cubic-bezier(0.3, 0.9, 0.2, 1);
}

.card-inner.is-flipped {
  transform: rotateY(180deg);
}

.card-face {
  position: absolute;
  inset: 0;
  border-radius: 12px;
  backface-visibility: hidden;
  -webkit-backface-visibility: hidden;
  border: 1px solid rgba(245, 194, 107, 0.6);
  box-shadow:
    0 0 24px rgba(245, 194, 107, 0.25),
    inset 0 0 30px rgba(245, 194, 107, 0.12);
  overflow: hidden;
}

.card-back {
  background:
    radial-gradient(circle at center, rgba(245, 194, 107, 0.25), transparent 65%),
    linear-gradient(135deg, #1a0a2e 0%, #2a1248 50%, #13082a 100%);
}

.card-back-pattern {
  position: absolute;
  inset: 6px;
  border: 1px solid rgba(245, 194, 107, 0.35);
  border-radius: 8px;
  background:
    repeating-linear-gradient(45deg, rgba(245, 194, 107, 0.05) 0 2px, transparent 2px 8px);
}

.card-back-emblem {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 3rem;
  color: rgba(245, 194, 107, 0.8);
  text-shadow: 0 0 16px rgba(245, 194, 107, 0.9);
}

.card-front {
  background: linear-gradient(180deg, #2a1248 0%, #13082a 100%);
  display: flex;
  align-items: center;
  justify-content: center;
}

.card-front-inner {
  position: relative;
  width: 100%;
  height: 100%;
}

.card-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 11px;
  display: block;
  user-select: none;
}

.card-name-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 0.3rem 0.25rem;
  background: linear-gradient(transparent, rgba(10, 4, 20, 0.85));
  border-radius: 0 0 11px 11px;
  font-size: 0.6rem;
  text-align: center;
  color: #f5c26b;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  line-height: 1.2;
}

.card-image {
  font-size: 2.4rem;
  color: #f5c26b;
  text-shadow: 0 0 20px rgba(245, 194, 107, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
