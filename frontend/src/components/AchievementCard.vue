<script setup lang="ts">
import { computed } from 'vue'
import type { AchievementInfo } from '@/types'

const props = defineProps<{
  achievement: AchievementInfo
}>()

const unlocked = computed(() => props.achievement.unlockedAt != null)

const unlockedDate = computed(() => {
  if (!props.achievement.unlockedAt) return null
  return new Date(props.achievement.unlockedAt).toLocaleDateString()
})
</script>

<template>
  <div
    class="achievement-card"
    :class="{ unlocked, locked: !unlocked }"
    data-testid="achievement-card"
  >
    <div class="icon">
      <span class="icon-glyph">{{ unlocked ? '✦' : '✧' }}</span>
    </div>
    <div class="title">{{ achievement.name }}</div>
    <div class="desc">{{ achievement.description }}</div>
    <div v-if="unlocked" class="date">Получено: {{ unlockedDate }}</div>
    <div v-else class="locked-label">Ещё не открыто</div>
  </div>
</template>

<style scoped>
.achievement-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 1.25rem 1rem;
  border-radius: 14px;
  border: 1px solid rgba(245, 194, 107, 0.2);
  background: linear-gradient(180deg, rgba(42, 18, 72, 0.45), rgba(19, 8, 42, 0.45));
  transition: all 0.3s ease;
}
.achievement-card.locked {
  opacity: 0.55;
  filter: grayscale(0.8);
}
.achievement-card.unlocked {
  border-color: rgba(245, 194, 107, 0.6);
  box-shadow: 0 0 24px rgba(245, 194, 107, 0.2);
}
.achievement-card.unlocked:hover {
  transform: translateY(-2px);
  box-shadow: 0 0 36px rgba(245, 194, 107, 0.35);
}
.icon {
  width: 3.5rem;
  height: 3.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid rgba(245, 194, 107, 0.4);
  margin-bottom: 0.75rem;
}
.unlocked .icon {
  background: rgba(245, 194, 107, 0.12);
  border-color: #f5c26b;
  box-shadow: 0 0 16px rgba(245, 194, 107, 0.5);
}
.icon-glyph {
  font-size: 1.5rem;
  color: #f5c26b;
}
.title {
  font-family: 'Cinzel', serif;
  font-size: 0.95rem;
  letter-spacing: 0.08em;
  color: #f5c26b;
  margin-bottom: 0.4rem;
}
.desc {
  font-size: 0.8rem;
  color: rgba(224, 212, 186, 0.8);
  line-height: 1.4;
  margin-bottom: 0.5rem;
}
.date {
  font-size: 0.7rem;
  color: rgba(245, 194, 107, 0.75);
  letter-spacing: 0.06em;
}
.locked-label {
  font-size: 0.7rem;
  color: rgba(224, 212, 186, 0.5);
  font-style: italic;
}
</style>
