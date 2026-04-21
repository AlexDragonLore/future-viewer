<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import { SpreadType } from '@/types'
import SubscriptionBanner from '@/components/SubscriptionBanner.vue'
import { findDeckMeta } from '@/data/decks'
import { SPREADS_META, findSpreadMeta } from '@/data/spreads'

const router = useRouter()
const auth = useAuthStore()
const deck = useDeckStore()

const question = ref('')
const spreadType = ref<SpreadType>(SpreadType.ThreeCard)

const currentDeckMeta = computed(() => findDeckMeta(deck.current))
const currentSpreadMeta = computed(() => findSpreadMeta(spreadType.value))

onMounted(async () => {
  if (auth.isAuthenticated) await auth.refreshSubscription()
})

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
    return 'Бесплатный доступ открыт только к раскладу «Карта дня». Оформи подписку, чтобы продолжить.'
  if (freeQuotaExhausted.value)
    return 'Ты использовал бесплатный расклад на сегодня. Возвращайся завтра или оформи подписку.'
  return ''
})

const canBegin = computed(() => {
  if (!question.value.trim()) return false
  if (!auth.isAuthenticated) return true
  return !blocked.value
})

const badgeText = computed(() => {
  if (!auth.isAuthenticated) return null
  if (auth.subscriptionLoading) return '…'
  if (auth.isSubscribed) return 'Подписка активна'
  const s = auth.subscription
  if (!s) return null
  const left = Math.max(0, s.freeReadingsDailyLimit - s.freeReadingsUsedToday)
  return `Бесплатно сегодня: ${left}/${s.freeReadingsDailyLimit}`
})

function begin() {
  if (!canBegin.value) return
  sessionStorage.setItem('fv_pending', JSON.stringify({ spreadType: spreadType.value, question: question.value }))
  if (!auth.isAuthenticated) {
    router.push({ name: 'auth', query: { redirect: '/reading' } })
    return
  }
  router.push({ name: 'reading' })
}
</script>

<template>
  <main class="min-h-screen flex flex-col items-center justify-center px-4 sm:px-6 py-16">
    <header class="text-center mb-10">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-3">✦ ВУАЛЬ ГРЯДУЩЕГО ✦</div>
      <h1 class="font-display text-4xl sm:text-5xl md:text-7xl gold-text mb-4">Загляни за Вуаль</h1>
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

      <div v-if="blocked" class="block-warning" data-testid="block-warning">
        {{ blockMessage }}
      </div>

      <button class="glow-button w-full" :disabled="!canBegin" @click="begin">
        {{ auth.isAuthenticated ? 'Начать расклад' : 'Войти и начать' }}
      </button>

      <SubscriptionBanner
        v-if="auth.isAuthenticated && !auth.isSubscribed && blocked"
        :message="requiresSubscription ? 'Расклад требует подписки' : 'Лимит бесплатных раскладов исчерпан'"
      />

      <div class="flex justify-between text-xs text-mystic-silver/50">
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
.spread-blurb {
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
</style>
