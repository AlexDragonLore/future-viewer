<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import { useProfileStore } from '@/stores/useProfileStore'
import { SpreadType } from '@/types'
import SubscriptionBanner from '@/components/SubscriptionBanner.vue'
import { paidProduct } from '@/content/legal'
import { findDeckMeta } from '@/data/decks'
import { SPREADS_META, findSpreadMeta } from '@/data/spreads'
import { extractApiError } from '@/api/httpClient'
import { readingApi } from '@/api/readingApi'
import type { AxiosError } from 'axios'

const router = useRouter()
const auth = useAuthStore()
const deck = useDeckStore()
const profile = useProfileStore()

const question = ref('')
const firstName = ref('')
const lastName = ref('')
const birthDate = ref('')
const introError = ref<string | null>(null)
const validationMessage = ref<string | null>(null)
const validationSuggestion = ref<string | null>(null)
const validatingQuestion = ref(false)
const spreadType = ref<SpreadType>(SpreadType.ThreeCard)

const currentDeckMeta = computed(() => findDeckMeta(deck.current))
const currentSpreadMeta = computed(() => findSpreadMeta(spreadType.value))

onMounted(async () => {
  const validation = sessionStorage.getItem('fv_question_validation')
  if (validation) {
    sessionStorage.removeItem('fv_question_validation')
    try {
      const parsed = JSON.parse(validation) as { message?: string; suggestedQuestion?: string | null }
      validationMessage.value = parsed.message ?? null
      validationSuggestion.value = parsed.suggestedQuestion ?? null
    } catch {
      validationMessage.value = null
      validationSuggestion.value = null
    }
  }

  if (auth.isAuthenticated) {
    await Promise.all([auth.refreshSubscription(), profile.loadPersonalization()])
    if (profile.personalization) {
      firstName.value = profile.personalization.firstName ?? ''
      lastName.value = profile.personalization.lastName ?? ''
      birthDate.value = profile.personalization.birthDate ?? ''
    }
  }
})

const needsIntro = computed(() => auth.isAuthenticated && !(profile.personalization?.isComplete ?? false))

const requiresSubscription = computed(() => {
  if (!auth.isAuthenticated) return false
  if (auth.isSubscribed) return false
  return spreadType.value !== SpreadType.SingleCard
})

const freeQuotaExhausted = computed(() => {
  if (!auth.isAuthenticated) return false
  if (auth.isSubscribed) return false
  if (spreadType.value !== SpreadType.SingleCard) return false
  return !auth.canCreateReading
})

const blocked = computed(() => requiresSubscription.value || freeQuotaExhausted.value)

const blockMessage = computed(() => {
  if (requiresSubscription.value)
    return 'Бесплатный доступ открыт только к раскладу «Карта дня». Оплати доступ, чтобы продолжить.'
  if (freeQuotaExhausted.value)
    return 'Ты использовал бесплатный расклад на сегодня. Возвращайся завтра или оплати доступ.'
  return ''
})

const canBegin = computed(() => {
  if (validatingQuestion.value) return false
  if (!question.value.trim()) return false
  if (needsIntro.value && (!firstName.value.trim() || !lastName.value.trim() || !birthDate.value)) return false
  if (!auth.isAuthenticated) return true
  return !blocked.value
})

const badgeText = computed(() => {
  if (!auth.isAuthenticated) return null
  if (auth.subscriptionLoading) return '…'
  if (auth.isSubscribed) return 'Доступ активен'
  const s = auth.subscription
  if (!s) return null
  const left = Math.max(0, s.freeReadingsDailyLimit - s.freeReadingsUsedToday)
  return `Бесплатно сегодня: ${left}/${s.freeReadingsDailyLimit}`
})

function applySuggestion() {
  if (validationSuggestion.value) {
    question.value = validationSuggestion.value
    validationMessage.value = null
    validationSuggestion.value = null
  }
}

async function begin() {
  if (!canBegin.value) return
  introError.value = null
  validationMessage.value = null
  validationSuggestion.value = null
  const pending = { spreadType: spreadType.value, question: question.value.trim() }
  sessionStorage.setItem('fv_pending', JSON.stringify(pending))
  if (!auth.isAuthenticated) {
    router.push({ name: 'auth', query: { redirect: '/reading' } })
    return
  }
  if (needsIntro.value) {
    try {
      await profile.savePersonalization({
        firstName: firstName.value,
        lastName: lastName.value,
        birthDate: birthDate.value,
      })
    } catch (e) {
      introError.value = extractApiError(e, 'Не удалось сохранить знакомство')
      return
    }
  }

  validatingQuestion.value = true
  try {
    await readingApi.validateQuestion(spreadType.value, pending.question, deck.current)
  } catch (e) {
    const err = e as AxiosError<{ message?: string; error?: string; suggestedQuestion?: string | null }>
    const code = err.response?.data?.error
    if (code === 'question_needs_rewrite' || code === 'question_rejected') {
      validationMessage.value = extractApiError(e, 'Вопрос нужно уточнить')
      validationSuggestion.value = err.response?.data?.suggestedQuestion ?? null
      sessionStorage.removeItem('fv_pending')
      return
    }
    validationMessage.value = extractApiError(e, 'Не удалось проверить вопрос')
    sessionStorage.removeItem('fv_pending')
    return
  } finally {
    validatingQuestion.value = false
  }

  router.push({ name: 'reading' })
}
</script>

<template>
  <main class="home-page min-h-screen flex flex-col items-center justify-center px-4 sm:px-6 py-16">
    <header class="text-center mb-10">
      <div class="home-kicker text-mystic-accent text-xs tracking-[0.4em] mb-3">✦ ВУАЛЬ ГРЯДУЩЕГО ✦</div>
      <h1 class="home-title font-display text-4xl sm:text-5xl md:text-7xl gold-text mb-4">Загляни за Вуаль</h1>
      <p class="text-mystic-silver/70 max-w-xl mx-auto">
        Задай вопрос Вселенной. Карты Таро раскроют тайные нити судьбы и покажут путь сквозь туман грядущего.
      </p>
    </header>

    <section class="mystic-card w-full max-w-xl p-5 sm:p-8 space-y-6">
      <div v-if="badgeText" class="subscription-badge" :class="{ active: auth.isSubscribed }">
        <span>{{ badgeText }}</span>
        <RouterLink v-if="!auth.isSubscribed" to="/history" class="ml-auto text-mystic-accent text-xs hover:underline">
          История
        </RouterLink>
      </div>

      <div v-if="currentDeckMeta" class="deck-blurb" data-testid="home-deck-blurb">
        <div class="deck-blurb-head">
          <span class="deck-blurb-label">Колода:</span>
          <strong>{{ currentDeckMeta.label }}</strong>
        </div>
        <p>{{ currentDeckMeta.shortDescription }}</p>
        <RouterLink :to="`/glossary#deck-${currentDeckMeta.anchorId}`" class="blurb-link">
          Подробнее в глоссарии →
        </RouterLink>
      </div>

      <div>
        <label class="block text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Расклад</label>
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <button
            v-for="s in SPREADS_META"
            :key="s.type"
            class="spread-option"
            :class="{ active: spreadType === s.type }"
            @click="spreadType = s.type"
          >
            <div class="text-sm font-display">{{ s.label }}</div>
            <div class="text-xs text-mystic-silver/60 mt-1">{{ s.cardCount }} карт(ы)</div>
          </button>
        </div>
        <div v-if="currentSpreadMeta" class="spread-blurb" data-testid="home-spread-blurb">
          <p>{{ currentSpreadMeta.shortDescription }}</p>
          <RouterLink
            :to="`/glossary#spread-${currentSpreadMeta.anchorId}`"
            class="blurb-link"
          >
            Подробнее в глоссарии →
          </RouterLink>
        </div>
      </div>

      <div>
        <label class="block text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Вопрос</label>
        <textarea
          v-model="question"
          rows="3"
          placeholder="Что меня ждёт впереди?"
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 text-mystic-silver placeholder:text-mystic-silver/30 focus:outline-none focus:border-mystic-accent transition"
          maxlength="500"
        />
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

      <section v-if="needsIntro" class="intro-block" data-testid="personalization-intro">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Знакомство</div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <input
            v-model="firstName"
            type="text"
            placeholder="Имя"
            maxlength="80"
            class="intro-input"
            data-testid="first-name-input"
          />
          <input
            v-model="lastName"
            type="text"
            placeholder="Фамилия"
            maxlength="80"
            class="intro-input"
            data-testid="last-name-input"
          />
          <input
            v-model="birthDate"
            type="date"
            class="intro-input sm:col-span-2"
            data-testid="birth-date-input"
          />
        </div>
        <p v-if="introError" class="text-red-300 text-xs mt-2">{{ introError }}</p>
      </section>

      <div v-if="blocked" class="block-warning" data-testid="block-warning">
        {{ blockMessage }}
      </div>

      <button class="glow-button w-full" :disabled="!canBegin" @click="begin">
        {{ validatingQuestion ? 'Сверяю вопрос…' : auth.isAuthenticated ? 'Начать расклад' : 'Войти и начать' }}
      </button>

      <SubscriptionBanner
        v-if="auth.isAuthenticated && !auth.isSubscribed && blocked"
        :message="requiresSubscription ? 'Расклад требует платного доступа' : 'Лимит бесплатных раскладов исчерпан'"
      />

      <div class="payment-info">
        <div>
          <div class="payment-title">{{ paidProduct.title }}</div>
          <p>
            {{ paidProduct.price }} за {{ paidProduct.period }}. Цифровой доступ к безлимитным раскладам
            активируется после успешной онлайн-оплаты. Автосписаний нет — для продления нужно оплатить доступ заново.
          </p>
        </div>
        <RouterLink to="/legal" class="payment-link">Условия оплаты</RouterLink>
      </div>

      <div class="home-links flex justify-between text-xs text-mystic-silver/50">
        <RouterLink v-if="!auth.isAuthenticated" to="/auth" class="hover:text-mystic-accent transition">Войти / Регистрация</RouterLink>
        <RouterLink v-else to="/history" class="hover:text-mystic-accent transition">История раскладов</RouterLink>
        <span v-if="auth.email">{{ auth.email }}</span>
      </div>
    </section>
  </main>
</template>

<style scoped>
.spread-option {
  padding: 0.75rem 0.5rem;
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 10px;
  background: rgba(0, 0, 0, 0.2);
  color: inherit;
  cursor: pointer;
  transition: all 0.3s ease;
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
.block-warning {
  padding: 0.75rem 1rem;
  border-radius: 8px;
  border: 1px solid rgba(239, 68, 68, 0.35);
  background: rgba(239, 68, 68, 0.08);
  color: #fca5a5;
  font-size: 0.8rem;
  line-height: 1.4;
}
.glow-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.payment-info {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.75rem 0;
  border-top: 1px solid rgba(245, 194, 107, 0.18);
  border-bottom: 1px solid rgba(245, 194, 107, 0.12);
}
.payment-info p {
  margin: 0.2rem 0 0;
  color: rgba(224, 212, 186, 0.62);
  font-size: 0.75rem;
  line-height: 1.45;
}
.payment-title {
  color: rgba(245, 194, 107, 0.9);
  font-family: 'Cinzel', serif;
  font-size: 0.78rem;
  letter-spacing: 0.08em;
}
.payment-link {
  flex: 0 0 auto;
  color: #f5c26b;
  font-size: 0.75rem;
  text-decoration: none;
}
.payment-link:hover {
  text-decoration: underline;
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
  .payment-info {
    align-items: flex-start;
    flex-direction: column;
    gap: 0.5rem;
  }
}
</style>
