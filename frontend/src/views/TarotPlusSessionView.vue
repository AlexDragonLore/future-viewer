<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { marked } from 'marked'
import { useRoute } from 'vue-router'
import { useTarotPlusStore } from '@/stores/useTarotPlusStore'
import { TarotPlusSessionStatus } from '@/types'

const route = useRoute()
const store = useTarotPlusStore()
const answer = ref('')
const followUpQuestion = ref('')
const ritualCards = ['I', 'II', 'III', 'IV', 'V']
const dealReplayKey = ref(0)
type RitualState = {
  title: string
  description: string
  steps: string[]
  durationMs: number
  minVisibleMs: number
  fallbackHint?: string
}
type AnswerRitualMode = 'default-question' | 'ai-question'
const visibleRitual = ref<RitualState | null>(null)
const ritualProgress = ref(0)
const answerRitualMode = ref<AnswerRitualMode | null>(null)
let ritualTimer: ReturnType<typeof setTimeout> | null = null
let ritualProgressTimer: ReturnType<typeof setInterval> | null = null
let ritualShownAt = 0

const sessionId = computed(() => String(route.params.id))
const session = computed(() => store.current)
const reportHtml = computed(() =>
  session.value?.reportMarkdown ? (marked.parse(session.value.reportMarkdown) as string) : '',
)

function renderMarkdown(markdown: string) {
  return marked.parse(markdown) as string
}

const isPaymentState = computed(() =>
  session.value?.status === TarotPlusSessionStatus.PreviewReady ||
  session.value?.status === TarotPlusSessionStatus.PaymentPending,
)
const isIntakeState = computed(() =>
  session.value?.status === TarotPlusSessionStatus.Paid ||
  session.value?.status === TarotPlusSessionStatus.Intake,
)
const isGeneratingState = computed(() =>
  session.value?.status === TarotPlusSessionStatus.CardsDrawn ||
  session.value?.status === TarotPlusSessionStatus.ReportGenerating,
)
const isReportState = computed(() =>
  session.value?.status === TarotPlusSessionStatus.ReportReady ||
  session.value?.status === TarotPlusSessionStatus.Completed,
)
const canSubmitAnswer = computed(() => Boolean(store.nextQuestion && answer.value.trim().length >= 3 && !store.loading))
const canGenerate = computed(() => Boolean(store.nextStep?.canGenerateReport && !store.reportLoading))
const activeRitual = computed<RitualState | null>(() => {
  if (store.reportLoading) {
    return {
      title: 'Собираю отчёт',
      description: 'AI получает до 30 секунд. Если не успеет, завершу отчёт по уже выпавшим картам.',
      steps: ['Карты', 'AI', 'Отчёт'],
      durationMs: 30000,
      minVisibleMs: 1200,
      fallbackHint: 'Если AI не успеет сейчас, отчёт будет собран без долгого ожидания.',
    }
  }
  if (store.followUpLoading) {
    return {
      title: 'Смотрю уточнение',
      description: 'Сопоставляю вопрос с готовым отчётом и уже выпавшими картами.',
      steps: ['Вопрос', 'Контекст', 'Ответ'],
      durationMs: 45000,
      minVisibleMs: 1200,
    }
  }
  if (store.paymentLoading) {
    return {
      title: 'Готовлю переход к оплате',
      description: 'Создаю безопасную ссылку именно для этой Tarot+ сессии.',
      steps: ['Сессия', 'Платёж', 'Возврат'],
      durationMs: 3000,
      minVisibleMs: 1200,
    }
  }
  if (store.loading && session.value) {
    if (answerRitualMode.value === 'ai-question') {
      return {
        title: 'AI подбирает вопрос',
        description: 'Даём модели до 10 секунд. Если она не успеет, продолжим без зависания.',
        steps: ['Ответ', 'AI', 'Fallback'],
        durationMs: 10000,
        minVisibleMs: 1200,
        fallbackHint: 'Если ответа нет, сейчас включится запасной сценарий.',
      }
    }

    return {
      title: 'Ответ принят',
      description: 'Фиксирую контекст и открываю следующий обычный вопрос.',
      steps: ['Ответ', 'Контекст', 'Вопрос'],
      durationMs: 3000,
      minVisibleMs: 3000,
    }
  }
  return null
})

watch(activeRitual, (ritual) => {
  if (ritualTimer) {
    clearTimeout(ritualTimer)
    ritualTimer = null
  }

  if (ritual) {
    visibleRitual.value = ritual
    ritualShownAt = Date.now()
    startRitualProgress(ritual.durationMs)
    return
  }

  if (!visibleRitual.value) return

  const remainingMs = Math.max(0, visibleRitual.value.minVisibleMs - (Date.now() - ritualShownAt))
  if (remainingMs === 0) {
    ritualProgress.value = 100
    stopRitualProgress()
  }
  ritualTimer = setTimeout(() => {
    ritualProgress.value = 100
    stopRitualProgress()
    visibleRitual.value = null
    ritualTimer = null
    ritualProgress.value = 0
  }, remainingMs)
})

onBeforeUnmount(() => {
  if (ritualTimer) clearTimeout(ritualTimer)
  stopRitualProgress()
})

function startRitualProgress(durationMs: number) {
  stopRitualProgress()
  const startedAt = Date.now()
  ritualProgress.value = 4
  ritualProgressTimer = setInterval(() => {
    const elapsed = Date.now() - startedAt
    ritualProgress.value = Math.min(99, Math.round((elapsed / durationMs) * 100))
  }, 120)
}

function stopRitualProgress() {
  if (!ritualProgressTimer) return
  clearInterval(ritualProgressTimer)
  ritualProgressTimer = null
}

onMounted(async () => {
  await store.load(sessionId.value)
  if (isIntakeState.value) await store.loadNextStep(sessionId.value)
})

watch(
  () => session.value?.status,
  async (status) => {
    if (status === TarotPlusSessionStatus.Paid || status === TarotPlusSessionStatus.Intake) {
      await store.loadNextStep(sessionId.value)
    }
  },
)

async function refresh() {
  await store.load(sessionId.value)
}

async function pay() {
  await store.createPayment(sessionId.value)
}

async function submitAnswer() {
  if (!store.nextQuestion || !canSubmitAnswer.value) return
  const nextIntakeCount = (session.value?.intakeAnswerCount ?? 0) + 1
  answerRitualMode.value = nextIntakeCount < 5 ? 'default-question' : 'ai-question'
  try {
    await store.addAnswer(sessionId.value, store.nextQuestion, answer.value)
    answer.value = ''
  } finally {
    answerRitualMode.value = null
  }
}

async function generateReport() {
  await store.generateReport(sessionId.value)
}

async function askFollowUp() {
  const question = followUpQuestion.value.trim()
  if (!question) return
  await store.askFollowUp(sessionId.value, question)
  followUpQuestion.value = ''
}

function cardDealIndex(spreadIndex: number, cardIndex: number) {
  return spreadIndex * 12 + cardIndex
}

function replaySpreadAnimation() {
  dealReplayKey.value += 1
}
</script>

<template>
  <main class="tarot-plus-session min-h-screen px-4 sm:px-6 py-12 sm:py-16" data-testid="tarot-plus-session-view">
    <section class="session-shell">
      <div v-if="store.loading && !session" class="mystic-card state-card">Загрузка…</div>

      <template v-else-if="session">
        <header class="session-header">
          <div class="kicker">TAROT+ · {{ session.routeLabel }}</div>
          <h1 class="font-display gold-text">Жизненный компас</h1>
          <p>{{ session.coreRequest }}</p>
        </header>

        <section v-if="isPaymentState" class="mystic-card state-card" data-testid="tarot-plus-payment-state">
          <h2 class="font-display gold-text">Preview</h2>
          <p>{{ session.previewText }}</p>
          <div class="payment-line">{{ session.priceRub }} ₽ за одну сессию</div>
          <div class="actions">
            <button class="glow-button" :disabled="store.paymentLoading" data-testid="tarot-plus-session-pay" @click="pay">
              {{ store.paymentLoading ? 'Открываю оплату…' : 'Оплатить и продолжить' }}
            </button>
            <button class="secondary-button" type="button" @click="refresh">Обновить статус</button>
          </div>
        </section>

        <section v-else-if="isIntakeState" class="mystic-card state-card" data-testid="tarot-plus-intake-state">
          <div class="progress">
            Ответов: {{ session.intakeAnswerCount }} / {{ store.nextStep?.maxAnswers ?? 9 }}
          </div>
          <template v-if="store.nextQuestion">
            <h2 class="font-display gold-text">Следующий вопрос</h2>
            <p class="question" data-testid="tarot-plus-next-question">{{ store.nextQuestion }}</p>
            <textarea v-model="answer" rows="4" data-testid="tarot-plus-answer" />
            <button class="glow-button w-full" :disabled="!canSubmitAnswer" data-testid="tarot-plus-answer-submit" @click="submitAnswer">
              {{ store.loading ? 'Сохраняю…' : 'Ответить' }}
            </button>
          </template>
          <div v-if="canGenerate" class="generate-block">
            <p>Контекста достаточно, чтобы вытянуть карты и собрать отчёт.</p>
            <button class="glow-button w-full" :disabled="store.reportLoading" data-testid="tarot-plus-generate" @click="generateReport">
              {{ store.reportLoading ? 'Собираю отчёт…' : 'Сгенерировать отчёт' }}
            </button>
          </div>
        </section>

        <section v-else-if="isGeneratingState" class="mystic-card state-card" data-testid="tarot-plus-generating-state">
          <div class="ritual-inline" aria-hidden="true">
            <div class="ritual-visual compact">
              <span v-for="card in ritualCards" :key="card" class="ritual-card">{{ card }}</span>
            </div>
          </div>
          <h2 class="font-display gold-text">Карты уже легли</h2>
          <p>Собираю отчёт из раскладов, ответов и текущей ветки анализа.</p>
          <button
            v-if="session.status === TarotPlusSessionStatus.CardsDrawn"
            class="glow-button w-full"
            :disabled="store.reportLoading"
            data-testid="tarot-plus-retry-generate"
            @click="generateReport"
          >
            {{ store.reportLoading ? 'Собираю отчёт…' : 'Повторить генерацию отчёта' }}
          </button>
          <button class="secondary-button" type="button" @click="refresh">Обновить</button>
        </section>

        <section v-else-if="isReportState" class="report-layout" data-testid="tarot-plus-report-state">
          <article class="mystic-card report-card">
            <div class="prose-mystic tarot-plus-report text-mystic-silver leading-relaxed" v-html="reportHtml" />
            <section
              v-if="session.drawnSpreads.length > 0"
              :key="dealReplayKey"
              class="spreads-section"
              data-testid="tarot-plus-drawn-spreads"
            >
              <div class="spreads-heading">
                <h2 class="font-display gold-text">Выпавшие расклады</h2>
                <button
                  class="replay-button"
                  type="button"
                  data-testid="tarot-plus-replay-spreads"
                  @click="replaySpreadAnimation"
                >
                  Повторить
                </button>
              </div>
              <div v-for="(spread, spreadIndex) in session.drawnSpreads" :key="spread.spreadId" class="spread-block">
                <h3>{{ spread.spreadName }}</h3>
                <div class="cards-grid">
                  <article
                    v-for="(card, cardIndex) in spread.cards"
                    :key="`${spread.spreadId}-${card.position}`"
                    class="drawn-card"
                    :class="{ reversed: card.isReversed }"
                    :style="{ '--deal-index': String(cardDealIndex(spreadIndex, cardIndex)) }"
                  >
                    <img v-if="card.imagePath" :src="card.imagePath" :alt="card.cardName" loading="lazy" />
                    <div class="card-copy">
                      <span class="position">{{ card.positionName }}</span>
                      <strong>{{ card.cardName }}<span v-if="card.isReversed"> · перевёрнутая</span></strong>
                      <p>{{ card.meaning }}</p>
                    </div>
                  </article>
                </div>
              </div>
            </section>
          </article>

          <aside class="mystic-card follow-up-card">
            <h2 class="font-display gold-text">Follow-up</h2>
            <p>Осталось вопросов: {{ session.followUpsLeft }}</p>
            <textarea
              v-if="session.followUpsLeft > 0"
              v-model="followUpQuestion"
              rows="3"
              data-testid="tarot-plus-follow-up-question"
            />
            <button
              v-if="session.followUpsLeft > 0"
              class="glow-button w-full"
              :disabled="store.followUpLoading || followUpQuestion.trim().length < 3"
              data-testid="tarot-plus-follow-up-submit"
              @click="askFollowUp"
            >
              {{ store.followUpLoading ? 'Отвечаю…' : 'Задать вопрос' }}
            </button>
            <div v-for="item in session.followUps" :key="item.createdAt" class="stored-follow-up">
              <strong>{{ item.question }}</strong>
              <div class="prose-mystic" v-html="renderMarkdown(item.answerMarkdown)" />
            </div>
          </aside>
        </section>

        <section v-else class="mystic-card state-card">
          <h2 class="font-display gold-text">Сессия недоступна</h2>
          <p>Текущий статус: {{ session.status }}</p>
        </section>
      </template>

      <p v-if="store.error" class="error" data-testid="tarot-plus-session-error">{{ store.error }}</p>
    </section>

    <Transition name="ritual-fade">
      <div
        v-if="visibleRitual"
        class="ritual-overlay"
        role="status"
        aria-live="polite"
        data-testid="tarot-plus-ritual-loader"
      >
        <div class="ritual-panel">
          <div class="ritual-visual" aria-hidden="true">
            <span v-for="card in ritualCards" :key="card" class="ritual-card">{{ card }}</span>
          </div>
          <div class="ritual-copy">
            <p class="ritual-kicker">Tarot+ в работе</p>
            <h2 class="font-display gold-text">{{ visibleRitual.title }}</h2>
            <p>{{ visibleRitual.description }}</p>
            <p v-if="visibleRitual.fallbackHint && ritualProgress >= 85" class="ritual-fallback">
              {{ visibleRitual.fallbackHint }}
            </p>
            <div class="ritual-steps">
              <span v-for="step in visibleRitual.steps" :key="step">{{ step }}</span>
            </div>
            <div class="ritual-progress-label">{{ ritualProgress }}%</div>
            <div class="ritual-progress" aria-hidden="true">
              <span :style="{ width: `${ritualProgress}%` }" />
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </main>
</template>

<style scoped>
.session-shell {
  max-width: 68rem;
  margin: 0 auto;
  display: grid;
  gap: 1.25rem;
}
.session-header {
  text-align: center;
}
.kicker {
  color: #f5c26b;
  letter-spacing: 0.24em;
  font-size: 0.75rem;
  text-transform: uppercase;
}
h1 {
  margin: 0.5rem 0 0.7rem;
  font-size: 3.4rem;
  line-height: 1.08;
}
.session-header p {
  max-width: 42rem;
  margin: 0 auto;
  color: rgba(224, 212, 186, 0.72);
}
.state-card,
.report-card,
.follow-up-card {
  padding: 1.5rem;
}
.report-card {
  overflow: hidden;
}
.tarot-plus-report {
  max-width: 46rem;
  font-size: 1rem;
  line-height: 1.78;
  color: rgba(224, 212, 186, 0.84);
}
.tarot-plus-report :deep(h1) {
  margin: 0 0 1.25rem;
  color: #f5c26b;
  font-size: clamp(1.9rem, 4vw, 2.65rem);
  line-height: 1.12;
  letter-spacing: 0.02em;
}
.tarot-plus-report :deep(h2) {
  margin: 1.6rem 0 0.85rem;
  padding: 0.8rem 0.95rem;
  border-left: 3px solid rgba(245, 194, 107, 0.72);
  border-radius: 8px;
  color: #f5c26b;
  background: rgba(245, 194, 107, 0.08);
  font-size: 1.2rem;
  line-height: 1.3;
  letter-spacing: 0.04em;
}
.tarot-plus-report :deep(h3) {
  margin: 1.2rem 0 0.5rem;
  color: rgba(245, 194, 107, 0.92);
  font-size: 1rem;
  line-height: 1.35;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.tarot-plus-report :deep(p) {
  margin: 0 0 0.9rem;
  overflow-wrap: anywhere;
}
.tarot-plus-report :deep(p + p) {
  margin-top: 0.2rem;
}
.tarot-plus-report :deep(strong) {
  color: #f5c26b;
  font-weight: 700;
}
.tarot-plus-report :deep(em) {
  color: rgba(245, 194, 107, 0.9);
}
.tarot-plus-report :deep(ul),
.tarot-plus-report :deep(ol) {
  display: grid;
  gap: 0.45rem;
  margin: 0.85rem 0 1rem;
  padding-left: 1.15rem;
}
.tarot-plus-report :deep(li) {
  padding-left: 0.2rem;
  color: rgba(224, 212, 186, 0.86);
}
.tarot-plus-report :deep(li::marker) {
  color: #f5c26b;
}
.tarot-plus-report :deep(blockquote) {
  margin: 1rem 0;
  padding: 0.85rem 1rem;
  border-left: 3px solid rgba(245, 194, 107, 0.55);
  border-radius: 8px;
  background: rgba(245, 194, 107, 0.07);
  color: rgba(224, 212, 186, 0.88);
}
.tarot-plus-report :deep(hr) {
  margin: 1.35rem 0;
  border: 0;
  border-top: 1px solid rgba(245, 194, 107, 0.18);
}
.state-card {
  max-width: 42rem;
  width: 100%;
  justify-self: center;
  display: grid;
  gap: 1rem;
}
.state-card h2,
.follow-up-card h2 {
  font-size: 1.5rem;
  margin: 0;
}
.state-card p,
.follow-up-card p {
  color: rgba(224, 212, 186, 0.78);
  line-height: 1.65;
}
.payment-line,
.progress {
  color: #f5c26b;
  font-size: 0.9rem;
}
.actions {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}
.secondary-button {
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 999px;
  padding: 0.7rem 1rem;
  color: #f5c26b;
  background: transparent;
}
.question {
  font-size: 1.05rem;
  color: #f5c26b;
}
textarea {
  width: 100%;
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.28);
  color: #e0d4ba;
  padding: 0.8rem;
  outline: none;
}
textarea:focus {
  border-color: #f5c26b;
}
.generate-block {
  display: grid;
  gap: 0.8rem;
  border-top: 1px solid rgba(245, 194, 107, 0.18);
  padding-top: 1rem;
}
.ritual-overlay {
  position: fixed;
  inset: 0;
  z-index: 80;
  display: grid;
  place-items: center;
  padding: 1rem;
  background:
    radial-gradient(circle at 50% 24%, rgba(245, 194, 107, 0.14), transparent 26rem),
    rgba(6, 2, 16, 0.78);
  backdrop-filter: blur(10px);
}
.ritual-panel {
  width: min(35rem, 100%);
  display: grid;
  justify-items: center;
  gap: 1.4rem;
  padding: 1.6rem;
  border: 1px solid rgba(245, 194, 107, 0.32);
  border-radius: 8px;
  background: linear-gradient(145deg, rgba(32, 13, 54, 0.96), rgba(9, 4, 25, 0.96));
  box-shadow: 0 1.2rem 3rem rgba(0, 0, 0, 0.48), 0 0 3rem rgba(245, 194, 107, 0.12);
}
.ritual-copy {
  width: 100%;
  display: grid;
  gap: 0.75rem;
  text-align: center;
}
.ritual-copy h2 {
  margin: 0;
  font-size: 1.65rem;
}
.ritual-copy p {
  margin: 0;
  color: rgba(224, 212, 186, 0.78);
  line-height: 1.6;
}
.ritual-fallback {
  color: #f5c26b;
  font-size: 0.86rem;
}
.ritual-kicker {
  color: #f5c26b;
  font-size: 0.72rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
}
.ritual-visual {
  position: relative;
  width: 14rem;
  height: 9rem;
}
.ritual-visual.compact {
  width: 12rem;
  height: 7.5rem;
  margin: 0 auto;
}
.ritual-card {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 4.3rem;
  height: 6.35rem;
  display: grid;
  place-items: center;
  border: 1px solid rgba(245, 194, 107, 0.45);
  border-radius: 7px;
  background:
    linear-gradient(135deg, rgba(245, 194, 107, 0.22), transparent 42%),
    radial-gradient(circle at 50% 28%, rgba(255, 255, 255, 0.18), transparent 1.7rem),
    linear-gradient(160deg, rgba(47, 20, 77, 0.98), rgba(10, 4, 28, 0.98));
  color: rgba(245, 194, 107, 0.88);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  box-shadow: 0 0.9rem 1.7rem rgba(0, 0, 0, 0.35);
  transform-origin: 50% 115%;
  animation: card-breathe 1.8s ease-in-out infinite;
}
.ritual-card:nth-child(1) {
  transform: translate(-50%, -50%) rotate(-26deg) translateY(0.4rem);
  animation-delay: 0s;
}
.ritual-card:nth-child(2) {
  transform: translate(-50%, -50%) rotate(-13deg) translateY(-0.15rem);
  animation-delay: 0.12s;
}
.ritual-card:nth-child(3) {
  transform: translate(-50%, -50%) rotate(0deg) translateY(-0.45rem);
  animation-delay: 0.24s;
}
.ritual-card:nth-child(4) {
  transform: translate(-50%, -50%) rotate(13deg) translateY(-0.15rem);
  animation-delay: 0.36s;
}
.ritual-card:nth-child(5) {
  transform: translate(-50%, -50%) rotate(26deg) translateY(0.4rem);
  animation-delay: 0.48s;
}
.ritual-steps {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.45rem;
}
.ritual-steps span {
  min-width: 0;
  padding: 0.42rem 0.55rem;
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 999px;
  color: rgba(245, 194, 107, 0.86);
  background: rgba(245, 194, 107, 0.08);
  font-size: 0.72rem;
  overflow-wrap: anywhere;
}
.ritual-progress {
  height: 3px;
  overflow: hidden;
  border-radius: 999px;
  background: rgba(245, 194, 107, 0.14);
}
.ritual-progress-label {
  color: rgba(245, 194, 107, 0.84);
  font-size: 0.78rem;
  letter-spacing: 0.12em;
}
.ritual-progress span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, rgba(245, 194, 107, 0.45), #f5c26b);
  transition: width 0.16s ease;
}
.ritual-inline {
  padding: 0.35rem 0 0.2rem;
}
.ritual-fade-enter-active,
.ritual-fade-leave-active {
  transition: opacity 0.18s ease;
}
.ritual-fade-enter-from,
.ritual-fade-leave-to {
  opacity: 0;
}
.report-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 22rem;
  gap: 1rem;
  align-items: start;
}
.spreads-section {
  margin-top: 1.25rem;
  padding-top: 1.25rem;
  border-top: 1px solid rgba(245, 194, 107, 0.18);
}
.spreads-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
}
.spreads-section h2 {
  margin: 0;
  font-size: 1.6rem;
}
.replay-button {
  flex: 0 0 auto;
  border: 1px solid rgba(245, 194, 107, 0.28);
  border-radius: 999px;
  padding: 0.5rem 0.85rem;
  color: #f5c26b;
  background: rgba(245, 194, 107, 0.07);
  font-size: 0.78rem;
}
.replay-button:hover {
  border-color: rgba(245, 194, 107, 0.55);
  background: rgba(245, 194, 107, 0.12);
}
.spread-block {
  display: grid;
  gap: 0.8rem;
}
.spread-block + .spread-block {
  margin-top: 1.25rem;
}
.spread-block h3 {
  margin: 0;
  color: #f5c26b;
  font-size: 1rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
  gap: 0.8rem;
}
.drawn-card {
  display: grid;
  grid-template-columns: 4.2rem minmax(0, 1fr);
  gap: 0.75rem;
  align-items: start;
  padding: 0.75rem;
  border: 1px solid rgba(245, 194, 107, 0.16);
  border-radius: 8px;
  background: rgba(11, 6, 24, 0.34);
  opacity: 0;
  transform-origin: 50% 0;
  animation: tarot-card-deal 0.58s cubic-bezier(0.2, 0.78, 0.22, 1) forwards;
  animation-delay: calc(var(--deal-index) * 82ms);
  will-change: opacity, transform, filter;
}
.drawn-card img {
  width: 100%;
  aspect-ratio: 2 / 3;
  object-fit: cover;
  border-radius: 6px;
  border: 1px solid rgba(245, 194, 107, 0.22);
  transform-origin: center;
  animation: tarot-card-face 0.5s ease-out both;
  animation-delay: calc((var(--deal-index) * 82ms) + 120ms);
}
.drawn-card.reversed img {
  transform: rotate(180deg);
  animation-name: tarot-card-face-reversed;
}
.card-copy {
  min-width: 0;
  display: grid;
  gap: 0.35rem;
}
.card-copy .position {
  color: rgba(245, 194, 107, 0.78);
  font-size: 0.72rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}
.card-copy strong {
  color: #f5c26b;
}
.card-copy p {
  margin: 0;
  color: rgba(224, 212, 186, 0.72);
  font-size: 0.9rem;
  line-height: 1.45;
}
.follow-up-card {
  display: grid;
  gap: 0.9rem;
  position: sticky;
  top: 5rem;
}
.stored-follow-up {
  border-top: 1px solid rgba(245, 194, 107, 0.16);
  padding-top: 0.8rem;
  color: rgba(224, 212, 186, 0.82);
}
.stored-follow-up strong {
  color: #f5c26b;
}
.error {
  color: #fca5a5;
  text-align: center;
}
@media (max-width: 900px) {
  .report-layout {
    grid-template-columns: 1fr;
  }
  .follow-up-card {
    position: static;
  }
}
@media (max-width: 640px) {
  h1 {
    font-size: 2.35rem;
  }
  .state-card,
  .report-card,
  .follow-up-card {
    padding: 1.1rem;
  }
  .tarot-plus-report {
    max-width: none;
    font-size: 0.96rem;
    line-height: 1.68;
  }
  .tarot-plus-report :deep(h2) {
    padding: 0.7rem 0.8rem;
    font-size: 1.05rem;
  }
  .spreads-heading {
    align-items: flex-start;
    flex-direction: column;
  }
  .ritual-panel {
    padding: 1.2rem;
  }
  .ritual-visual {
    width: 12rem;
    height: 8rem;
  }
  .ritual-card {
    width: 3.8rem;
    height: 5.7rem;
  }
  .drawn-card {
    grid-template-columns: 3.7rem minmax(0, 1fr);
  }
  .actions .glow-button,
  .actions .secondary-button {
    width: 100%;
  }
}
@media (prefers-reduced-motion: reduce) {
  .ritual-card,
  .ritual-progress span,
  .drawn-card,
  .drawn-card img {
    animation: none;
    transition: none;
    opacity: 1;
    transform: none;
  }
  .drawn-card.reversed img {
    transform: rotate(180deg);
  }
  .ritual-fade-enter-active,
  .ritual-fade-leave-active {
    transition: none;
  }
}
@keyframes card-breathe {
  0%, 100% {
    filter: brightness(1);
    margin-top: 0;
  }
  50% {
    filter: brightness(1.22);
    margin-top: -0.45rem;
  }
}
@keyframes tarot-card-deal {
  0% {
    opacity: 0;
    transform: translate3d(0, -2.4rem, 0) rotate(-5deg) scale(0.94);
    filter: blur(2px) brightness(1.15);
  }
  62% {
    opacity: 1;
    transform: translate3d(0, 0.28rem, 0) rotate(1.4deg) scale(1.015);
    filter: blur(0) brightness(1.08);
  }
  100% {
    opacity: 1;
    transform: translate3d(0, 0, 0) rotate(0) scale(1);
    filter: blur(0) brightness(1);
  }
}
@keyframes tarot-card-face {
  0% {
    opacity: 0.35;
    transform: rotateY(84deg);
  }
  100% {
    opacity: 1;
    transform: rotateY(0deg);
  }
}
@keyframes tarot-card-face-reversed {
  0% {
    opacity: 0.35;
    transform: rotateY(84deg) rotate(180deg);
  }
  100% {
    opacity: 1;
    transform: rotateY(0deg) rotate(180deg);
  }
}
</style>
