<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  score: number | null | undefined
  size?: 'sm' | 'md' | 'lg'
}>()

const sizeClass = computed(() => {
  switch (props.size) {
    case 'sm':
      return 'w-8 h-8 text-xs'
    case 'lg':
      return 'w-16 h-16 text-2xl'
    default:
      return 'w-12 h-12 text-base'
  }
})

const toneClass = computed(() => {
  const s = props.score
  if (s == null) return 'neutral'
  if (s >= 9) return 'excellent'
  if (s >= 7) return 'good'
  if (s >= 4) return 'okay'
  return 'poor'
})

const label = computed(() => {
  if (props.score == null) return '—'
  return String(props.score)
})
</script>

<template>
  <span class="score-badge" :class="[toneClass, sizeClass]" data-testid="score-badge">
    {{ label }}
  </span>
</template>

<style scoped>
.score-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  font-family: 'Cinzel', serif;
  font-weight: 600;
  letter-spacing: 0.05em;
  border: 1px solid transparent;
}
.score-badge.neutral {
  background: rgba(0, 0, 0, 0.25);
  border-color: rgba(224, 212, 186, 0.3);
  color: rgba(224, 212, 186, 0.75);
}
.score-badge.excellent {
  background: rgba(74, 222, 128, 0.15);
  border-color: rgba(74, 222, 128, 0.6);
  color: #86efac;
  text-shadow: 0 0 8px rgba(74, 222, 128, 0.4);
}
.score-badge.good {
  background: rgba(245, 194, 107, 0.15);
  border-color: rgba(245, 194, 107, 0.6);
  color: #f5c26b;
  text-shadow: 0 0 8px rgba(245, 194, 107, 0.4);
}
.score-badge.okay {
  background: rgba(251, 191, 36, 0.12);
  border-color: rgba(251, 191, 36, 0.5);
  color: #fcd34d;
}
.score-badge.poor {
  background: rgba(239, 68, 68, 0.1);
  border-color: rgba(239, 68, 68, 0.5);
  color: #fca5a5;
}
</style>
