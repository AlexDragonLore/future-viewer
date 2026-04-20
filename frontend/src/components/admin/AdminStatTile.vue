<script setup lang="ts">
defineProps<{
  label: string
  value: number | string
  hint?: string
  testId?: string
}>()
</script>

<template>
  <div class="stat-tile mystic-card" :data-testid="testId">
    <div class="label">{{ label }}</div>
    <div class="value">{{ value }}</div>
    <div v-if="hint" class="hint">{{ hint }}</div>
  </div>
</template>

<style scoped>
.stat-tile {
  padding: 1.25rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  min-height: 110px;
  justify-content: center;
}
.label {
  font-family: 'Cinzel', serif;
  font-size: 0.7rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.75);
}
.value {
  font-family: 'Cinzel', serif;
  font-size: 2.25rem;
  font-weight: 600;
  color: #f5c26b;
  font-variant-numeric: tabular-nums;
  line-height: 1.1;
}
.hint {
  font-size: 0.72rem;
  color: rgba(224, 212, 186, 0.55);
  letter-spacing: 0.03em;
}
</style>
