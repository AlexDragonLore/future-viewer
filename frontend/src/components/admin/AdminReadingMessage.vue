<script setup lang="ts">
import { computed } from 'vue'
import { findSpreadMeta } from '@/data/spreads'
import { findDeckMeta } from '@/data/decks'
import { safeMarkdown } from '@/utils/safeMarkdown'
import type { AdminReadingSummary } from '@/types/admin'

const props = defineProps<{ reading: AdminReadingSummary & { expiresAt?: string | null } }>()
const interpretationHtml = computed(() => safeMarkdown(props.reading.interpretation))
</script>

<template>
  <div class="reading-message">
    <div class="message-meta">
      <span>{{ findSpreadMeta(reading.spreadType)?.label ?? 'Расклад' }}</span>
      <span>{{ findDeckMeta(reading.deckType)?.label }}</span>
      <time :datetime="reading.createdAt">{{ new Date(reading.createdAt).toLocaleString('ru-RU') }}</time>
    </div>
    <p v-if="reading.expiresAt" class="message-retention" data-testid="admin-reading-retention">
      Хранится 24 часа · до <time :datetime="reading.expiresAt">{{ new Date(reading.expiresAt).toLocaleString('ru-RU') }}</time>
    </p>
    <span v-if="reading.deletedFromHistoryAt" class="hidden-badge" data-testid="admin-reading-hidden">Скрыт из истории пользователем</span>
    <p class="message-label">Вопрос</p>
    <p class="message-question" data-testid="admin-reading-question">{{ reading.question || 'Текст вопроса не сохранён' }}</p>
    <details v-if="reading.interpretation" class="message-answer">
      <summary data-testid="admin-reading-expand">Ответ ИИ</summary>
      <div class="interpretation" data-testid="admin-reading-answer" v-html="interpretationHtml" />
    </details>
    <p v-else class="message-empty">Ответ ИИ пока отсутствует или не был сохранён.</p>
  </div>
</template>

<style scoped>
.reading-message { min-width: 0; overflow-wrap: anywhere; }
.message-meta { display: flex; flex-wrap: wrap; gap: 0.4rem 0.85rem; color: rgba(224, 212, 186, 0.6); font-size: 0.75rem; }
.message-meta time { margin-left: auto; }
.message-retention { margin-top: 0.7rem; color: rgba(224, 212, 186, 0.6); font-size: 0.75rem; }
.message-label { margin: 1rem 0 0.35rem; font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.08em; color: #f5c26b; }
.message-question { white-space: pre-wrap; line-height: 1.6; color: #eee0cf; }
.message-answer { margin-top: 1rem; border-top: 1px solid rgba(245, 194, 107, 0.15); }
summary { padding: 0.85rem 0; color: #f5c26b; cursor: pointer; min-height: 44px; }
summary:focus-visible { outline: 2px solid #f5c26b; outline-offset: 3px; border-radius: 4px; }
.hidden-badge { display: inline-block; margin-top: 0.7rem; padding: 0.25rem 0.6rem; border: 1px solid rgba(245, 194, 107, 0.25); border-radius: 6px; color: #e0c49c; font-size: 0.72rem; }
.message-empty { margin-top: 0.8rem; color: rgba(224, 212, 186, 0.5); font-size: 0.8rem; }
.interpretation { line-height: 1.7; color: rgba(232, 213, 242, 0.9); }
.interpretation :deep(p), .interpretation :deep(ul), .interpretation :deep(ol) { margin-bottom: 0.75rem; }
.interpretation :deep(h2), .interpretation :deep(h3) { margin: 0.75rem 0 0.4rem; color: #f5c26b; font-size: 1rem; letter-spacing: 0.02em; }
.interpretation :deep(ul) { list-style: disc; padding-left: 1.3rem; }
.interpretation :deep(ol) { list-style: decimal; padding-left: 1.3rem; }
.interpretation :deep(a) { color: #f5c26b; text-decoration: underline; }
.interpretation :deep(pre) { white-space: pre-wrap; overflow-wrap: anywhere; }
@media (max-width: 640px) {
  .message-meta time { margin-left: 0; flex-basis: 100%; }
}
</style>
