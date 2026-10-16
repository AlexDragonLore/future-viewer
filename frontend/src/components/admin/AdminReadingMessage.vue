<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { findSpreadMeta } from '@/data/spreads'
import { findDeckMeta } from '@/data/decks'
import { safeMarkdown } from '@/utils/safeMarkdown'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import { accountSessionVersion, resetOnAccountChange } from '@/utils/accountSession'
import type { AdminReadingSummary } from '@/types/admin'

const props = defineProps<{ reading: AdminReadingSummary & { expiresAt?: string | null } }>()
const questionId = useId()
const answerId = useId()
const questionElement = ref<HTMLElement | null>(null)
const questionExpanded = ref(false)
const questionOverflows = ref(false)
const answerOpen = ref(false)
const answer = ref<string | null>(null)
const answerLoaded = ref(false)
const answerLoading = ref(false)
const answerError = ref<string | null>(null)
let controller: AbortController | undefined
let resizeObserver: ResizeObserver | undefined
const hasAnswer = computed(() => props.reading.hasInterpretation ?? Boolean(props.reading.interpretation))
const interpretationHtml = computed(() => answerOpen.value ? safeMarkdown(answer.value ?? props.reading.interpretation ?? '') : '')
const canExpandQuestion = computed(() => questionOverflows.value || questionExpanded.value || props.reading.question.length > 120 || props.reading.question.includes('\n'))

function measureQuestion() {
  if (questionElement.value && !questionExpanded.value) questionOverflows.value = questionElement.value.scrollHeight > questionElement.value.clientHeight + 1
}
onMounted(() => {
  measureQuestion()
  if (typeof ResizeObserver !== 'undefined' && questionElement.value) {
    resizeObserver = new ResizeObserver(measureQuestion)
    resizeObserver.observe(questionElement.value)
  }
})
onBeforeUnmount(() => { controller?.abort(); resizeObserver?.disconnect() })
function resetMessage() {
  controller?.abort()
  questionExpanded.value = false; questionOverflows.value = false
  answerOpen.value = false; answer.value = null; answerLoaded.value = false
  answerLoading.value = false; answerError.value = null
  void nextTick(measureQuestion)
}
watch(() => props.reading, resetMessage)
resetOnAccountChange({ questionExpanded, questionOverflows, answerOpen, answer, answerLoaded, answerLoading, answerError }, () => controller?.abort())

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
      <slot name="identity" />
      <time :datetime="reading.createdAt">{{ new Date(reading.createdAt).toLocaleString('ru-RU') }}</time>
      <span>{{ findSpreadMeta(reading.spreadType)?.label ?? 'Расклад' }} · {{ findDeckMeta(reading.deckType)?.label }}</span>
      <span v-if="reading.expiresAt" data-testid="admin-reading-retention">Хранится 30 дней · до <time :datetime="reading.expiresAt">{{ new Date(reading.expiresAt).toLocaleString('ru-RU') }}</time></span>
      <span v-if="reading.deletedFromHistoryAt" class="hidden-badge" data-testid="admin-reading-hidden">Скрыт из истории</span>
    </div>
    <slot name="subscription" />
    <p :id="questionId" ref="questionElement" class="message-question" :class="{ collapsed: !questionExpanded }" data-testid="admin-reading-question">{{ reading.question || 'Текст вопроса не сохранён' }}</p>
    <div class="message-actions">
      <button v-if="canExpandQuestion" type="button" :aria-expanded="questionExpanded" :aria-controls="questionId" data-testid="admin-question-expand" @click="questionExpanded = !questionExpanded">{{ questionExpanded ? 'Свернуть вопрос' : 'Весь вопрос' }}</button>
      <button v-if="hasAnswer" type="button" :aria-expanded="answerOpen" :aria-controls="answerId" data-testid="admin-reading-expand" @click="toggleAnswer">{{ answerOpen ? 'Скрыть ответ' : 'Ответ ИИ' }}</button>
      <span v-else class="message-empty">Ответ пока отсутствует</span>
    </div>
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
.message-meta { display: flex; align-items: center; flex-wrap: wrap; gap: 0.15rem 0.65rem; color: rgba(224, 212, 186, 0.55); font-size: 0.68rem; line-height: 1.35; }
.message-question { margin-top: 0.15rem; white-space: pre-wrap; font-size: 0.84rem; line-height: 1.4; color: #eee0cf; }
.message-question.collapsed { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.message-actions { display: flex; align-items: center; gap: 1rem; }
button { min-height: 32px; color: #f5c26b; font-size: 0.73rem; text-align: left; }
button:focus-visible { outline: 2px solid #f5c26b; outline-offset: 2px; border-radius: 3px; }
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
  .message-question.collapsed { -webkit-line-clamp: 3; }
  button { min-height: 44px; }
}
</style>
