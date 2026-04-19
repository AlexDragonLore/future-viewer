<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { marked } from 'marked'
import { feedbackApi, type FeedbackSubmitResult } from '@/api/feedbackApi'
import { extractApiError } from '@/api/httpClient'
import FeedbackForm from '@/components/FeedbackForm.vue'
import ScoreBadge from '@/components/ScoreBadge.vue'
import { FeedbackStatus, type FeedbackInfo } from '@/types'

const route = useRoute()

const feedback = ref<FeedbackInfo | null>(null)
const loading = ref(true)
const loadError = ref<string | null>(null)
const submitting = ref(false)
const submitError = ref<string | null>(null)
const justScored = ref<FeedbackSubmitResult | null>(null)

const interpretationHtml = computed(() =>
  feedback.value?.interpretation ? (marked.parse(feedback.value.interpretation) as string) : '',
)

const isScored = computed(() => {
  const status = feedback.value?.status
  return status === FeedbackStatus.Scored || status === FeedbackStatus.Answered
})

const displayScore = computed<number | null>(() => {
  if (justScored.value) return justScored.value.score
  return feedback.value?.aiScore ?? null
})

const displayReason = computed<string | null>(() => {
  if (justScored.value) return justScored.value.reason
  return feedback.value?.aiScoreReason ?? null
})

const displaySincere = computed<boolean | null>(() => {
  if (justScored.value) return justScored.value.isSincere
  return feedback.value?.isSincere ?? null
})

async function load(token: string) {
  loading.value = true
  loadError.value = null
  feedback.value = null
  try {
    feedback.value = await feedbackApi.getByToken(token)
  } catch (e) {
    loadError.value = extractApiError(e, 'Ссылка недействительна или истекла')
  } finally {
    loading.value = false
  }
}

async function onSubmit(selfReport: string) {
  if (!feedback.value) return
  submitting.value = true
  submitError.value = null
  try {
    const token = String(route.params.token)
    const result = await feedbackApi.submit(token, selfReport)
    justScored.value = result
    feedback.value = {
      ...feedback.value,
      selfReport,
      aiScore: result.score,
      aiScoreReason: result.reason,
      isSincere: result.isSincere,
      status: FeedbackStatus.Scored,
      answeredAt: new Date().toISOString(),
    }
  } catch (e) {
    submitError.value = extractApiError(e, 'Не удалось отправить отклик')
  } finally {
    submitting.value = false
  }
}

watch(
  () => route.params.token,
  (token) => {
    if (typeof token === 'string' && token) load(token)
  },
  { immediate: true },
)
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-3xl mx-auto">
    <header class="text-center mb-8">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ ОТКЛИК ✦</div>
      <h1 class="font-display text-4xl gold-text">Как прошёл день?</h1>
    </header>

    <div v-if="loading" class="text-center text-mystic-silver/60" data-testid="feedback-loading">
      загружаю…
    </div>
    <div v-else-if="loadError" class="text-center text-red-300" data-testid="feedback-load-error">
      {{ loadError }}
    </div>

    <template v-else-if="feedback">
      <section class="mystic-card p-6 mb-6">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Твой вопрос</div>
        <p class="italic text-mystic-silver/80" data-testid="feedback-question">«{{ feedback.question }}»</p>
      </section>

      <section v-if="feedback.interpretation" class="mystic-card p-6 mb-6" data-testid="feedback-interpretation">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Прошлая интерпретация</div>
        <div class="prose-mystic text-mystic-silver leading-relaxed" v-html="interpretationHtml" />
      </section>

      <section v-if="!isScored" class="mystic-card p-6">
        <FeedbackForm :busy="submitting" :error="submitError" @submit="onSubmit" />
        <p class="mt-4 text-xs text-mystic-silver/50">
          Переответить нельзя. Постарайся быть искренним.
        </p>
      </section>

      <section v-else class="mystic-card p-6" data-testid="feedback-result">
        <div class="flex items-center gap-4 mb-4">
          <ScoreBadge :score="displayScore" size="lg" />
          <div>
            <div class="text-xs uppercase tracking-widest text-mystic-accent/80">Оценка AI</div>
            <div class="text-mystic-silver font-display">
              {{ displayScore ?? '—' }} / 10
              <span v-if="displaySincere === false" class="insincere-tag">не искренне</span>
            </div>
          </div>
        </div>
        <div v-if="displayReason" class="text-sm text-mystic-silver/85 leading-relaxed" data-testid="feedback-reason">
          {{ displayReason }}
        </div>
        <div v-if="feedback.selfReport" class="mt-6">
          <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Твой ответ</div>
          <blockquote class="self-report" data-testid="feedback-self-report">
            {{ feedback.selfReport }}
          </blockquote>
        </div>
      </section>
    </template>
  </main>
</template>

<style scoped>
.prose-mystic :deep(h2) {
  font-size: 1.1rem;
  font-weight: 600;
  color: #f5c26b;
  margin-top: 1.25rem;
  margin-bottom: 0.4rem;
}
.prose-mystic :deep(strong) {
  color: #e8d5a3;
  font-weight: 600;
}
.prose-mystic :deep(ul) {
  list-style: disc;
  padding-left: 1.25rem;
  margin: 0.4rem 0;
}
.prose-mystic :deep(p) {
  margin-bottom: 0.6rem;
}
.insincere-tag {
  display: inline-block;
  margin-left: 0.5rem;
  padding: 0.1rem 0.5rem;
  font-size: 0.7rem;
  letter-spacing: 0.08em;
  border-radius: 999px;
  background: rgba(239, 68, 68, 0.12);
  border: 1px solid rgba(239, 68, 68, 0.45);
  color: #fca5a5;
}
.self-report {
  border-left: 2px solid rgba(245, 194, 107, 0.5);
  padding: 0.5rem 0.9rem;
  color: rgba(232, 213, 242, 0.85);
  font-style: italic;
  line-height: 1.5;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 0 8px 8px 0;
}
</style>
