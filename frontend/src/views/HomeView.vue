<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import { useReadingStore } from '@/stores/useReadingStore'
import { SpreadType } from '@/types'
import SubscriptionBanner from '@/components/SubscriptionBanner.vue'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'
import { findDeckMeta } from '@/data/decks'
import { SPREADS_META, findSpreadMeta } from '@/data/spreads'
import { extractApiError } from '@/api/httpClient'
import { readingApi } from '@/api/readingApi'
import { unlockAudio } from '@/composables/useAudio'
import type { AxiosError } from 'axios'
import { assessQuestionLocally } from '@/utils/questionSafety'
import { getGuestContinuation } from '@/utils/guestReading'

const router = useRouter()
const auth = useAuthStore()
const deck = useDeckStore()
const readingStore = useReadingStore()
const publicConfig = usePublicConfigStore()
const tariffSummary = computed(() => publicConfig.paidProducts.map(product => `${product.price} за ${product.period}`).join(' или '))
const showGuestPaidOffer = computed(() => !auth.isAuthenticated
  && publicConfig.paymentsEnabled && publicConfig.paidProducts.length > 0)

const question = ref('')
const validationMessage = ref<string | null>(null)
const validationSuggestion = ref<string | null>(null)
const validatingQuestion = ref(false)
const spreadType = ref<SpreadType>(!auth.isAuthenticated || auth.canCreateIntroReading ? SpreadType.ThreeCard : SpreadType.SingleCard)
let hasExplicitSpreadSelection = false
const hasGuestReading = ref(Boolean(getGuestContinuation()))

const currentDeckMeta = computed(() => findDeckMeta(deck.current))
const currentSpreadMeta = computed(() => findSpreadMeta(spreadType.value))

function buildFallbackSuggestion(source: string) {
  const trimmed = source.trim()
  if (!trimmed) return 'На что мне сейчас стоит обратить внимание?'
  if (trimmed.length <= 40) return `Что мне важно понять про тему «${trimmed}»?`
  return 'Какой следующий шаг мне стоит увидеть в этой ситуации?'
}

function ensureSuggestion(suggestedQuestion: string | null | undefined, source = question.value) {
  return suggestedQuestion?.trim() || buildFallbackSuggestion(source)
}

function restorePendingPayload(payload: { question?: string; spreadType?: SpreadType } | null) {
  if (!payload) return
  question.value = payload.question ?? ''
  if (payload.spreadType) selectSpread(payload.spreadType)
}

function selectSpread(type: SpreadType) {
  hasExplicitSpreadSelection = true
  spreadType.value = type
}

onMounted(async () => {
  const pendingPayload = readingStore.takePending()
  if (pendingPayload && !pendingPayload.validated) restorePendingPayload(pendingPayload)

  const workflowIssue = readingStore.takeWorkflowIssue()
  if (workflowIssue) {
    validationMessage.value = workflowIssue.message
    validationSuggestion.value = ensureSuggestion(workflowIssue.suggestedQuestion)
  }

  if (auth.isAuthenticated) {
    await auth.refreshSubscription()
    if (!hasExplicitSpreadSelection) {
      spreadType.value = auth.canCreateIntroReading ? SpreadType.ThreeCard : SpreadType.SingleCard
    }
  }
})

const requiresSubscription = computed(() => {
  if (!auth.isAuthenticated) return false
  if (auth.isSubscribed) return false
  if (spreadType.value === SpreadType.ThreeCard && auth.canCreateIntroReading) return false
  return spreadType.value !== SpreadType.SingleCard
})

const freeQuotaExhausted = computed(() => {
  if (!auth.isAuthenticated) return false
  if (auth.isSubscribed) return false
  if (requiresSubscription.value) return false
  return !auth.canCreateReading
})

const blocked = computed(() => requiresSubscription.value || freeQuotaExhausted.value)

const canBegin = computed(() => {
  if (validatingQuestion.value) return false
  if (auth.isAuthenticated && auth.subscriptionLoading) return false
  if (auth.isAuthenticated && !question.value.trim()) return false
  if (!auth.isAuthenticated) return true
  return !blocked.value
})

const badgeText = computed(() => {
  if (!auth.isAuthenticated) return null
  if (auth.subscriptionLoading) return '…'
  if (auth.isSubscribed) return 'Доступ активен'
  if (auth.canCreateIntroReading) return 'Первый расклад: 3 карты бесплатно'
  const s = auth.subscription
  if (!s) return null
  const left = Math.max(0, s.freeReadingsDailyLimit - s.freeReadingsUsedToday)
  return `Бесплатно сегодня: ${left}/${s.freeReadingsDailyLimit}`
})

function applySuggestion() {
  const suggestion = validationSuggestion.value
  if (suggestion) {
    question.value = suggestion
    validationMessage.value = null
    validationSuggestion.value = null
  }
}

async function begin() {
  if (!canBegin.value) return
  unlockAudio()
  if (!auth.isAuthenticated && getGuestContinuation()) {
    router.push({ name: 'result' })
    return
  }
  hasGuestReading.value = false
  validationMessage.value = null
  validationSuggestion.value = null
  const pending = {
    spreadType: auth.isAuthenticated ? spreadType.value : SpreadType.ThreeCard,
    question: question.value.trim() || 'На что мне сейчас стоит обратить внимание?',
    questionWarningAcknowledged: false,
    validated: false,
  }

  const localSafety = assessQuestionLocally(pending.question)
  if (localSafety.blocked) {
    validationMessage.value = localSafety.message ?? 'Вопрос нужно обезличить.'
    validationSuggestion.value = localSafety.suggestedQuestion ?? null
    return
  }

  if (!auth.isAuthenticated) {
    readingStore.setPending({ ...pending, validated: true })
    router.push({ name: 'reading' })
    return
  }

  validatingQuestion.value = true
  try {
    const validation = await readingApi.validateQuestion(spreadType.value, pending.question, deck.current)
    if (validation.status !== 'accepted' || !validation.canContinue) {
      validationMessage.value = validation.message || 'Вопрос нужно обезличить или переформулировать.'
      validationSuggestion.value = ensureSuggestion(validation.suggestedQuestion, pending.question)
      return
    }
  } catch (e) {
    const err = e as AxiosError<{ message?: string; error?: string; suggestedQuestion?: string | null }>
    const code = err.response?.data?.error
    if (code === 'question_needs_rewrite' || code === 'question_rejected' || code === 'question_requires_subscription') {
      validationMessage.value = extractApiError(e, 'Вопрос нужно уточнить')
      validationSuggestion.value = ensureSuggestion(err.response?.data?.suggestedQuestion, pending.question)
      return
    }
    validationMessage.value = extractApiError(e, 'Не удалось проверить вопрос')
    validationSuggestion.value = ensureSuggestion(null, pending.question)
    return
  } finally {
    validatingQuestion.value = false
  }

  readingStore.setPending({ ...pending, validated: true })
  router.push({ name: 'reading' })
}
</script>

<template>
  <main class="home-page min-h-screen flex flex-col items-center justify-center px-4 sm:px-6 py-16">
    <header class="text-center mb-10">
      <div class="home-kicker text-mystic-accent text-xs tracking-[0.4em] mb-3">✦ ВУАЛЬ ГРЯДУЩЕГО ✦</div>
      <h1 class="home-title font-display text-4xl sm:text-5xl md:text-7xl gold-text mb-4">{{ auth.isAuthenticated ? 'Загляни за Вуаль' : 'Открой свой расклад' }}</h1>
      <p class="text-mystic-silver/70 max-w-xl mx-auto">
        {{ auth.isAuthenticated
          ? 'Задай обезличенный вопрос и получи символическую интерпретацию карт как один из возможных взглядов на ситуацию.'
          : 'Первый расклад на 3 карты и начало толкования — бесплатно, без регистрации. Продолжение откроется после создания аккаунта. Затем — одна карта в день бесплатно.' }}
      </p>
    </header>

    <section class="mystic-card w-full max-w-xl p-5 sm:p-8 space-y-6">
      <div v-if="badgeText" class="subscription-badge" :class="{ active: auth.isSubscribed }">
        <span>{{ badgeText }}</span>
        <RouterLink v-if="!auth.isSubscribed" to="/history" class="ml-auto text-mystic-accent text-xs hover:underline">
          История
        </RouterLink>
      </div>

      <p v-if="auth.isAuthenticated && !auth.isSubscribed" class="text-xs text-mystic-silver/70" data-testid="free-reading-policy">
        {{ auth.canCreateIntroReading
          ? 'Первый расклад на 3 карты — бесплатно. Он считается раскладом на сегодня. Затем — одна карта в день бесплатно.'
          : 'Одна карта в день — бесплатно. Расклады на 3 и 10 карт доступны с платным доступом.' }}
      </p>

      <div v-if="auth.isAuthenticated && currentDeckMeta" class="deck-blurb" data-testid="home-deck-blurb">
        <div class="deck-blurb-head">
          <span class="deck-blurb-label">Колода:</span>
          <strong>{{ currentDeckMeta.label }}</strong>
        </div>
        <p>{{ currentDeckMeta.shortDescription }}</p>
        <RouterLink :to="currentDeckMeta.seoPath" class="blurb-link">
          Подробнее о колоде →
        </RouterLink>
      </div>

      <div v-if="auth.isAuthenticated">
        <label class="block text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Расклад</label>
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <button
            v-for="s in SPREADS_META"
            :key="s.type"
            class="spread-option"
            :class="{ active: spreadType === s.type }"
            @click="selectSpread(s.type)"
          >
            <div class="text-sm font-display">{{ s.label }}</div>
            <div class="text-xs text-mystic-silver/60 mt-1">{{ s.cardCount }} карт(ы)</div>
          </button>
        </div>
        <div v-if="currentSpreadMeta" class="spread-blurb" data-testid="home-spread-blurb">
          <p>{{ currentSpreadMeta.shortDescription }}</p>
          <RouterLink
            :to="currentSpreadMeta.seoPath"
            class="blurb-link"
          >
            Подробнее о раскладе →
          </RouterLink>
        </div>
      </div>

      <p v-if="!auth.isAuthenticated && hasGuestReading" class="text-center text-mystic-silver/80">
        Твой расклад уже открыт. Вернись к нему и продолжи с того же места.
      </p>
      <div v-if="auth.isAuthenticated || !hasGuestReading">
        <label for="reading-question" class="block text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">{{ auth.isAuthenticated ? 'Вопрос' : 'Твой вопрос · необязательно' }}</label>
        <textarea
          id="reading-question"
          v-model="question"
          rows="3"
          placeholder="На что мне сейчас стоит обратить внимание?"
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 text-mystic-silver placeholder:text-mystic-silver/30 focus:outline-none focus:border-mystic-accent transition"
          maxlength="500"
        />
        <p v-if="!auth.isAuthenticated" class="text-xs text-mystic-silver/60 mt-2">
          Можно просто открыть расклад на 3 карты. Если пишешь свой вопрос, не указывай имена и личные данные.
        </p>
      </div>

      <div v-if="validationMessage" class="validation-warning" data-testid="question-validation">
        <p>{{ validationMessage }}</p>
        <button
          v-if="validationSuggestion"
          type="button"
          class="suggestion-button"
          @click="applySuggestion"
          data-testid="apply-suggested-question"
        >
          {{ validationSuggestion }}
        </button>
      </div>

      <div v-if="validatingQuestion" class="validation-pending" data-testid="question-validating">
        <span class="validation-spinner" aria-hidden="true"></span>
        <span>Сверяю вопрос с Вуалью…</span>
      </div>

      <p v-if="auth.isAuthenticated && auth.subscriptionLoading" class="text-sm text-mystic-silver/60" role="status">
        Проверяю доступ…
      </p>
      <SubscriptionBanner
        v-else-if="blocked"
        :message="requiresSubscription ? 'Открой все расклады' : 'Расклады без ограничений'"
      />
      <div v-else>
        <button class="glow-button w-full" :disabled="!canBegin" @click="begin">
          {{ validatingQuestion ? 'Сверяю вопрос…' : auth.isAuthenticated ? 'Начать расклад' : hasGuestReading ? 'Продолжить мой расклад' : 'Открыть 3 карты бесплатно' }}
        </button>
        <p v-if="showGuestPaidOffer" class="guest-paid-offer" data-testid="guest-paid-offer">
          Все 3 расклада безлимитно — {{ tariffSummary }}. Без автосписаний.
        </p>
      </div>

      <div class="home-links flex flex-wrap items-center gap-3 justify-between text-xs text-mystic-silver/50">
        <RouterLink v-if="!auth.isAuthenticated" to="/auth" class="hover:text-mystic-accent transition">Войти / Регистрация</RouterLink>
        <RouterLink v-else to="/history" class="hover:text-mystic-accent transition">История раскладов</RouterLink>
        <RouterLink to="/faq" class="hover:text-mystic-accent transition">Вопросы и ответы</RouterLink>
        <span v-if="auth.email">{{ auth.email }}</span>
      </div>
    </section>
  </main>
</template>

<style scoped>
.guest-paid-offer {
  margin-top: 0.65rem;
  color: rgba(224, 212, 186, 0.62);
  font-size: 0.75rem;
  line-height: 1.6;
  text-align: center;
}
.spread-option {
  padding: 0.75rem 0.5rem;
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 10px;
  background: rgba(0, 0, 0, 0.2);
  color: inherit;
  cursor: pointer;
  transition:
    border-color 0.25s ease,
    background-color 0.25s ease,
    box-shadow 0.25s ease;
}
.spread-option:hover {
  border-color: rgba(245, 194, 107, 0.6);
  background: rgba(245, 194, 107, 0.08);
}
.spread-option.active {
  border-color: #f5c26b;
  background: rgba(245, 194, 107, 0.15);
  box-shadow: 0 0 20px rgba(245, 194, 107, 0.3);
}
.deck-blurb,
.spread-blurb,
.intro-block,
.validation-warning,
.validation-pending {
  padding: 0.75rem 1rem;
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 10px;
  background: rgba(0, 0, 0, 0.2);
  font-size: 0.8rem;
  color: rgba(224, 212, 186, 0.8);
  line-height: 1.5;
}
.spread-blurb {
  margin-top: 0.75rem;
}
.intro-input {
  width: 100%;
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.28);
  padding: 0.7rem 0.8rem;
  color: #e0d4ba;
  outline: none;
}
.intro-input:focus {
  border-color: #f5c26b;
}
.validation-warning {
  color: rgba(252, 165, 165, 0.95);
}
.validation-pending {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  color: rgba(245, 194, 107, 0.95);
}
.validation-spinner {
  width: 1rem;
  height: 1rem;
  flex: 0 0 auto;
  border: 2px solid rgba(245, 194, 107, 0.18);
  border-top-color: #f5c26b;
  border-radius: 999px;
  animation: validation-spin 0.9s linear infinite;
}
@keyframes validation-spin {
  to {
    transform: rotate(360deg);
  }
}
.suggestion-button {
  display: block;
  width: 100%;
  margin-top: 0.65rem;
  border: 1px solid rgba(245, 194, 107, 0.35);
  border-radius: 8px;
  padding: 0.65rem 0.8rem;
  color: #f5c26b;
  text-align: left;
  background: rgba(245, 194, 107, 0.08);
}
.question-warning-panel {
  border-color: rgba(252, 165, 165, 0.38);
}
.warning-title {
  font-family: 'Cinzel', serif;
  letter-spacing: 0.08em;
  color: #f5c26b;
  font-size: 0.95rem;
  margin-bottom: 0.65rem;
}
.warning-text,
.warning-reason {
  font-size: 0.85rem;
  line-height: 1.55;
  overflow-wrap: anywhere;
}
.warning-reason {
  margin-top: 0.5rem;
  color: rgba(252, 165, 165, 0.9);
}
.warning-actions {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
}
.warning-secondary {
  border: 1px solid rgba(224, 212, 186, 0.25);
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.24);
  color: rgba(224, 212, 186, 0.84);
  padding: 0.65rem 1rem;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  cursor: pointer;
}
.warning-primary {
  flex: 1 1 auto;
  min-width: min(100%, 15rem);
}
.deck-blurb-head {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
  margin-bottom: 0.35rem;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.08em;
  color: #f5c26b;
  font-size: 0.8rem;
}
.deck-blurb-label {
  color: rgba(224, 212, 186, 0.6);
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.15em;
}
.blurb-link {
  display: inline-block;
  margin-top: 0.4rem;
  color: #f5c26b;
  text-decoration: none;
  font-size: 0.75rem;
  letter-spacing: 0.06em;
}
.blurb-link:hover {
  text-decoration: underline;
}
.subscription-badge {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border-radius: 8px;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(0, 0, 0, 0.25);
  font-size: 0.75rem;
  color: rgba(224, 212, 186, 0.9);
}
.subscription-badge.active {
  border-color: rgba(245, 194, 107, 0.7);
  background: rgba(245, 194, 107, 0.12);
  color: #f5c26b;
}
.legal-payment-block {
  border-left: 2px solid rgba(252, 165, 165, 0.7);
  padding: 0.65rem 0.8rem;
  color: rgba(252, 165, 165, 0.9);
  font-size: 0.75rem;
  line-height: 1.5;
}
.glow-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
@media (max-width: 620px) {
  .home-page {
    justify-content: flex-start;
    padding-top: 2.5rem;
    padding-bottom: 2.5rem;
  }
  .home-kicker {
    letter-spacing: 0.14em;
  }
  .home-title {
    font-size: clamp(2rem, 13vw, 2.75rem);
    line-height: 1.12;
  }
  .deck-blurb-head,
  .subscription-badge,
  .home-links {
    flex-wrap: wrap;
  }
  .home-links {
    gap: 0.5rem;
    justify-content: center;
    overflow-wrap: anywhere;
    text-align: center;
  }
  .warning-actions {
    align-items: stretch;
    flex-direction: column-reverse;
  }
  .warning-primary,
  .warning-secondary {
    width: 100%;
  }
}
</style>
