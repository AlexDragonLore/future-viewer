<script setup lang="ts">
defineProps<{ size?: number; disabled?: boolean }>()
defineEmits<{ (e: 'shuffle'): void }>()
</script>

<template>
  <button
    class="deck-button breathe"
    :disabled="disabled"
    @click="$emit('shuffle')"
    :style="{ width: (size ?? 140) + 'px', height: ((size ?? 140) * 1.65) + 'px' }"
  >
    <div class="deck-stack">
      <div class="deck-card deck-card-3"></div>
      <div class="deck-card deck-card-2"></div>
      <div class="deck-card deck-card-1">
        <div class="deck-emblem">✦</div>
      </div>
    </div>
  </button>
</template>

<style scoped>
.deck-button {
  background: transparent;
  border: none;
  cursor: pointer;
  position: relative;
  padding: 0;
}

.deck-button:disabled {
  cursor: wait;
  opacity: 0.6;
}

.deck-stack {
  position: relative;
  width: 100%;
  height: 100%;
}

.deck-card {
  position: absolute;
  inset: 0;
  border-radius: 12px;
  border: 1px solid rgba(245, 194, 107, 0.6);
  background:
    radial-gradient(circle at center, rgba(245, 194, 107, 0.25), transparent 65%),
    linear-gradient(135deg, #1a0a2e 0%, #2a1248 50%, #13082a 100%);
  box-shadow:
    0 0 24px rgba(245, 194, 107, 0.25),
    inset 0 0 30px rgba(245, 194, 107, 0.12);
}

.deck-card-1 {
  transform: translate(0, 0);
  z-index: 3;
}
.deck-card-2 {
  transform: translate(3px, -3px);
  z-index: 2;
  opacity: 0.85;
}
.deck-card-3 {
  transform: translate(6px, -6px);
  z-index: 1;
  opacity: 0.7;
}

.deck-emblem {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 3rem;
  color: rgba(245, 194, 107, 0.9);
  text-shadow: 0 0 20px rgba(245, 194, 107, 0.9);
}

.deck-button:hover:not(:disabled) .deck-card-1 {
  box-shadow:
    0 0 40px rgba(245, 194, 107, 0.6),
    inset 0 0 30px rgba(245, 194, 107, 0.25);
}
</style>
