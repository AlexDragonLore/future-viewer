<script setup lang="ts">
import { Clock, EyeOff, Layers, MessageSquare } from 'lucide-vue-next'
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { findSpreadMeta } from '@/data/spreads'
import { findDeckMeta } from '@/data/decks'
import { safeMarkdown } from '@/utils/safeMarkdown'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import { accountSessionVersion, resetOnAccountChange } from '@/utils/accountSession'
import type { AdminReadingSummary } from '@/types/admin'

const props = defineProps<{ reading: AdminReadingSummary & { expiresAt?: string | null } }>()
const answerId = useId()
const createdAtLabel = computed(() => new Date(props.reading.createdAt).toLocaleString('ru-RU'))
const shortCreatedAt = computed(() => new Date(props.reading.createdAt).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }))
const spreadLabel = computed(() => [findSpreadMeta(props.reading.spreadType)?.label ?? 'Расклад', findDeckMeta(props.reading.deckType)?.label].filter(Boolean).join(' · '))
const retentionLabel = computed(() => props.reading.expiresAt ? `Гостевой расклад хранится 30 дней, до ${new Date(props.reading.expiresAt).toLocaleString('ru-RU')}` : '')
const answerOpen = ref(false)
const answer = ref<string | null>(null)
const answerLoaded = ref(false)
const answerLoading = ref(false)
const answerError = ref<string | null>(null)
let controller: AbortController | undefined
const hasAnswer = computed(() => props.reading.hasInterpretation ?? Boolean(props.reading.interpretation))
const interpretationHtml = computed(() => answerOpen.value ? safeMarkdown(answer.value ?? props.reading.interpretation ?? '') : '')
onBeforeUnmount(() => controller?.abort())
function resetMessage() {
  controller?.abort()
  answerOpen.value = false; answer.value = null; answerLoaded.value = false
  answerLoading.value = false; answerError.value = null
}
watch(() => props.reading, resetMessage)
resetOnAccountChange({ answerOpen, answer, answerLoaded, answerLoading, answerError }, () => controller?.abort())

async function loadAnswer() {
  if (answerLoading.value || answerLoaded.value) return
  if (props.reading.interpretation != null) {
    answer.value = props.reading.interpretation
    answerLoaded.value = true
    return
  }
  const version = accountSessionVersion()
  const id = props.reading.id
  const request = new AbortController()
  controller?.abort()
  controller = request
  answerLoading.value = true
  answerError.value = null
  try {
    const result = await adminApi.getReadingInterpretation(id, request.signal)
    if (request.signal.aborted || version !== accountSessionVersion() || props.reading.id !== id) return
    answer.value = result.interpretation
    answerLoaded.value = true
  } catch (e) {
    if (!request.signal.aborted && version === accountSessionVersion()) answerError.value = extractApiError(e, 'Не удалось загрузить ответ')
  } finally {
    if (controller === request && version === accountSessionVersion()) answerLoading.value = false
  }
}
function toggleAnswer() {
  answerOpen.value = !answerOpen.value
  if (answerOpen.value) void loadAnswer()
}
</script>

<template>
  <div class="reading-message">
    <div class="message-meta">
      <div v-if="$slots.identity" class="message-identity"><slot name="identity" /></div>
      <slot name="subscription" />
      <time class="message-time" :datetime="reading.createdAt" :title="createdAtLabel" :aria-label="createdAtLabel">{{ shortCreatedAt }}</time>
      <span class="meta-icon" role="img" :title="spreadLabel" :aria-label="spreadLabel" data-testid="admin-reading-spread"><Layers :size="14" aria-hidden="true" /></span>
      <span v-if="reading.expiresAt" class="meta-icon" role="img" :title="retentionLabel" :aria-label="retentionLabel" data-testid="admin-reading-retention"><Clock :size="14" aria-hidden="true" /></span>
      <span v-if="reading.deletedFromHistoryAt" class="meta-icon" role="img" title="Скрыт из истории пользователем" aria-label="Скрыт из истории пользователем" data-testid="admin-reading-hidden"><EyeOff :size="14" aria-hidden="true" /></span>
      <div class="message-actions">
        <button v-if="hasAnswer" class="icon-action" type="button" :aria-expanded="answerOpen" :aria-controls="answerId" :aria-label="answerOpen ? 'Скрыть ответ ИИ' : 'Показать ответ ИИ'" :title="answerOpen ? 'Скрыть ответ ИИ' : 'Показать ответ ИИ'" data-testid="admin-reading-expand" @click="toggleAnswer"><MessageSquare :size="16" aria-hidden="true" /></button>
      </div>
    </div>
    <p class="message-question" data-testid="admin-reading-question">{{ reading.question || 'Текст вопроса не сохранён' }}</p>
    <div v-if="answerOpen" :id="answerId" class="message-answer">
      <p v-if="answerLoading" role="status">Загружаю ответ…</p>
      <div v-else-if="answerError" role="alert"><p>{{ answerError }}</p><button type="button" @click="loadAnswer">Повторить</button></div>
      <div v-else-if="interpretationHtml" class="interpretation" data-testid="admin-reading-answer" v-html="interpretationHtml" />
      <p v-else class="message-empty">Ответ пока отсутствует</p>
    </div>
  </div>
</template>

<style scoped>
.reading-message { min-width: 0; overflow-wrap: anywhere; }
.message-meta { display: flex; align-items: center; gap: 0.4rem; min-width: 0; min-height: 28px; color: #8d829f; font-size: 0.68rem; line-height: 1.35; }
.message-identity { flex: 1 1 auto; min-width: 0; overflow: hidden; }
.message-time { flex-shrink: 0; white-space: nowrap; }
.meta-icon { display: inline-flex; align-items: center; flex-shrink: 0; opacity: 0.75; }
.message-question { margin: 0.1rem 0 0.35rem; white-space: pre-wrap; font-size: 0.875rem; line-height: 1.5; color: #eee8f3; }
.message-actions { display: flex; align-items: center; flex-shrink: 0; margin-left: auto; }
button { color: #f5c26b; font-size: 0.73rem; text-align: left; }
.icon-action { display: inline-flex; align-items: center; justify-content: center; width: 30px; min-width: 30px; height: 30px; border-radius: 7px; color: #e4c184; }
.icon-action:hover, .icon-action[aria-expanded="true"] { background: rgba(245, 194, 107, 0.1); }
button:focus-visible { outline: 2px solid #f5c26b; outline-offset: -2px; border-radius: 3px; }
.message-answer { border-top: 1px solid rgba(245, 194, 107, 0.15); padding: 0.6rem 0 0.4rem; }
.hidden-badge { color: #c8af8b; }
.message-empty { color: rgba(224, 212, 186, 0.5); font-size: 0.72rem; }
.interpretation { font-size: 0.875rem; line-height: 1.55; color: rgba(232, 213, 242, 0.9); }
.interpretation :deep(p), .interpretation :deep(ul), .interpretation :deep(ol) { margin-bottom: 0.5rem; }
.interpretation :deep(h2), .interpretation :deep(h3) { margin: 0.75rem 0 0.4rem; color: #f5c26b; font-size: 1rem; letter-spacing: 0.02em; }
.interpretation :deep(ul) { list-style: disc; padding-left: 1.3rem; }
.interpretation :deep(ol) { list-style: decimal; padding-left: 1.3rem; }
.interpretation :deep(a) { color: #f5c26b; text-decoration: underline; }
.interpretation :deep(pre) { white-space: pre-wrap; overflow-wrap: anywhere; }
@media (max-width: 640px) {
  .message-meta { gap: 0.15rem; }
  .icon-action { width: 44px; min-width: 44px; height: 44px; }
  .message-time { font-size: 0.64rem; }
}
</style>
