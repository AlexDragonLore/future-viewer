<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { useTarotPlusStore } from '@/stores/useTarotPlusStore'

const router = useRouter()
const store = useTarotPlusStore()
const auth = useAuthStore()

const coreRequest = ref('')
const mainSphere = ref('')
const desiredOutcome = ref('')
const previewWaitLong = ref(false)
const previewLastSecond = ref(false)
const previewProgress = ref(0)
let previewWaitTimer: ReturnType<typeof setTimeout> | null = null
let previewProgressTimer: ReturnType<typeof setInterval> | null = null

const canSubmit = computed(
  () =>
    coreRequest.value.trim().length >= 8 &&
    mainSphere.value.trim().length >= 3 &&
    desiredOutcome.value.trim().length >= 5 &&
    !store.loading,
)
const tarotPlusCredits = computed(() => auth.subscription?.tarotPlusCredits ?? 0)

onMounted(() => {
  store.reset()
  void auth.refreshSubscription()
})

watch(
  () => store.loading,
  (loading) => {
    if (previewWaitTimer) {
      clearTimeout(previewWaitTimer)
      previewWaitTimer = null
    }
    if (previewProgressTimer) {
      clearInterval(previewProgressTimer)
      previewProgressTimer = null
    }
    previewWaitLong.value = false
    previewLastSecond.value = false
    previewProgress.value = 0

    if (loading) {
      const startedAt = Date.now()
      previewProgress.value = 4
      previewProgressTimer = setInterval(() => {
        const elapsed = Date.now() - startedAt
        previewProgress.value = Math.min(99, Math.round((elapsed / 10000) * 100))
        previewLastSecond.value = previewProgress.value >= 90
      }, 120)
      previewWaitTimer = setTimeout(() => {
        previewWaitLong.value = true
      }, 4000)
    }
  },
)

onBeforeUnmount(() => {
  if (previewWaitTimer) clearTimeout(previewWaitTimer)
  if (previewProgressTimer) clearInterval(previewProgressTimer)
})

async function createPreview() {
  if (!canSubmit.value) return
  await store.createPreview({
    coreRequest: coreRequest.value,
    mainSphere: mainSphere.value,
    desiredOutcome: desiredOutcome.value,
  })
}

async function pay() {
  if (!store.current) return
  await store.createPayment(store.current.id)
}

function openSession() {
  if (!store.current) return
  router.push({ name: 'tarot-plus-session', params: { id: store.current.id } })
}
</script>

<template>
  <main class="tarot-plus-page min-h-screen px-4 sm:px-6 py-12 sm:py-16">
    <section class="tarot-plus-shell">
      <div class="hero">
        <div class="kicker">TAROT+</div>
        <h1 class="font-display gold-text">Жизненный компас</h1>
        <p>
          Глубокая разовая сессия: сначала короткий preview, затем уточняющее интервью,
          несколько раскладов и персональный отчёт с рекомендациями на 7, 30 и 90 дней.
        </p>
        <div class="price" data-testid="tarot-plus-price">100 ₽ за одну сессию</div>
        <div v-if="tarotPlusCredits > 0" class="bonus" data-testid="tarot-plus-bonus">
          Бонусных сессий: {{ tarotPlusCredits }}
        </div>
      </div>

      <div class="feature-grid" aria-label="Что внутри">
        <div>8–12 уточняющих вопросов</div>
        <div>Несколько раскладов</div>
        <div>Анализ текущей жизни</div>
        <div>2 follow-up вопроса</div>
      </div>

      <section class="mystic-card form-panel" data-testid="tarot-plus-view">
        <div v-if="!store.current" class="preview-block">
          <h2 class="font-display gold-text">Бесплатный preview</h2>
          <form class="preview-form" @submit.prevent="createPreview">
            <label>
              <span>Что сейчас больше всего хочется разобрать?</span>
              <textarea v-model="coreRequest" rows="3" maxlength="800" data-testid="tarot-plus-core" />
            </label>
            <label>
              <span>Какая сфера главная?</span>
              <input v-model="mainSphere" maxlength="200" data-testid="tarot-plus-sphere" />
            </label>
            <label>
              <span>Что хочешь получить после разбора?</span>
              <textarea v-model="desiredOutcome" rows="2" maxlength="500" data-testid="tarot-plus-outcome" />
            </label>
            <button class="glow-button w-full" :disabled="!canSubmit" data-testid="tarot-plus-preview-submit">
              {{ store.loading ? 'Собираю preview…' : 'Получить preview' }}
            </button>
            <div v-if="store.loading" class="preview-wait" role="status" aria-live="polite" data-testid="tarot-plus-preview-loader">
              <div class="mini-card-fan" aria-hidden="true">
                <span />
                <span />
                <span />
              </div>
              <div>
                <strong>
                  {{
                    previewLastSecond
                      ? 'Сейчас включу запасной preview'
                      : previewWaitLong ? 'AI отвечает дольше обычного' : 'Собираю первый контур'
                  }}
                </strong>
                <p>
                  {{
                    previewLastSecond
                      ? 'Если модель не успеет прямо сейчас, продолжим без зависания.'
                      : previewWaitLong
                      ? 'Жду ещё немного. Если модель не ответит быстро, сессия продолжится с базовым preview.'
                      : 'Обычно это занимает несколько секунд: определяю ветку и готовлю короткий preview.'
                  }}
                </p>
                <div class="preview-progress-label">{{ previewProgress }}%</div>
                <div class="preview-progress" aria-hidden="true">
                  <span :style="{ width: `${previewProgress}%` }" />
                </div>
              </div>
            </div>
          </form>
        </div>

        <div v-else class="preview-result" data-testid="tarot-plus-preview-result">
          <div class="route-label">{{ store.current.routeLabel }}</div>
          <h2 class="font-display gold-text">Preview готов</h2>
          <p>{{ store.current.previewText }}</p>
          <div class="result-actions">
            <button
              class="glow-button"
              :disabled="store.paymentLoading"
              data-testid="tarot-plus-pay"
              @click="pay"
            >
              {{ store.paymentLoading ? 'Открываю…' : (tarotPlusCredits > 0 ? 'Использовать бонусную сессию' : 'Продолжить за 100 ₽') }}
            </button>
            <button class="secondary-button" type="button" data-testid="tarot-plus-open-session" @click="openSession">
              Открыть сессию
            </button>
          </div>
        </div>

        <p v-if="store.error" class="error" data-testid="tarot-plus-error">{{ store.error }}</p>
      </section>
    </section>
  </main>
</template>

<style scoped>
.tarot-plus-page {
  width: 100%;
}
.tarot-plus-shell {
  max-width: 64rem;
  margin: 0 auto;
  display: grid;
  gap: 1.5rem;
}
.hero {
  text-align: center;
  padding-top: 1rem;
}
.kicker,
.route-label {
  color: #f5c26b;
  letter-spacing: 0.28em;
  font-size: 0.75rem;
  text-transform: uppercase;
}
h1 {
  font-size: 4.5rem;
  line-height: 1.05;
  margin: 0.6rem 0 1rem;
}
.hero p {
  max-width: 42rem;
  margin: 0 auto;
  color: rgba(224, 212, 186, 0.74);
  line-height: 1.7;
}
.price {
  margin-top: 1.25rem;
  display: inline-flex;
  padding: 0.55rem 1rem;
  border: 1px solid rgba(245, 194, 107, 0.34);
  border-radius: 999px;
  color: #f5c26b;
  background: rgba(245, 194, 107, 0.08);
}
.bonus {
  margin-top: 0.75rem;
  color: #98e09d;
  font-size: 0.9rem;
}
.feature-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.75rem;
}
.feature-grid div {
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 8px;
  padding: 0.85rem;
  color: rgba(224, 212, 186, 0.82);
  background: rgba(0, 0, 0, 0.18);
  text-align: center;
  font-size: 0.85rem;
}
.form-panel {
  padding: 1.5rem;
  max-width: 42rem;
  width: 100%;
  justify-self: center;
}
.form-panel h2 {
  margin: 0 0 1rem;
  font-size: 1.6rem;
  text-align: center;
}
.preview-form {
  display: grid;
  gap: 1rem;
}
.preview-wait {
  display: grid;
  grid-template-columns: 4.5rem minmax(0, 1fr);
  gap: 0.85rem;
  align-items: center;
  padding: 0.85rem;
  border: 1px solid rgba(245, 194, 107, 0.18);
  border-radius: 8px;
  background: rgba(245, 194, 107, 0.07);
  color: rgba(224, 212, 186, 0.82);
}
.preview-wait strong {
  display: block;
  color: #f5c26b;
  font-size: 0.95rem;
}
.preview-wait p {
  margin: 0.25rem 0 0;
  color: rgba(224, 212, 186, 0.7);
  font-size: 0.86rem;
  line-height: 1.45;
}
.preview-progress-label {
  margin-top: 0.55rem;
  color: rgba(245, 194, 107, 0.85);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
}
.preview-progress {
  height: 3px;
  margin-top: 0.3rem;
  overflow: hidden;
  border-radius: 999px;
  background: rgba(245, 194, 107, 0.13);
}
.preview-progress span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, rgba(245, 194, 107, 0.45), #f5c26b);
  transition: width 0.16s ease;
}
.mini-card-fan {
  position: relative;
  height: 4rem;
}
.mini-card-fan span {
  position: absolute;
  left: 50%;
  top: 0.2rem;
  width: 2.1rem;
  height: 3.25rem;
  border: 1px solid rgba(245, 194, 107, 0.42);
  border-radius: 5px;
  background:
    radial-gradient(circle at 50% 30%, rgba(255, 255, 255, 0.16), transparent 1.2rem),
    linear-gradient(155deg, rgba(55, 24, 85, 0.98), rgba(10, 4, 28, 0.98));
  box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.25);
  animation: mini-card-pulse 1.2s ease-in-out infinite;
}
.mini-card-fan span:nth-child(1) {
  transform: translateX(-75%) rotate(-15deg);
}
.mini-card-fan span:nth-child(2) {
  transform: translateX(-50%) translateY(-0.15rem);
  animation-delay: 0.15s;
}
.mini-card-fan span:nth-child(3) {
  transform: translateX(-25%) rotate(15deg);
  animation-delay: 0.3s;
}
label {
  display: grid;
  gap: 0.45rem;
  color: rgba(245, 194, 107, 0.85);
  font-size: 0.78rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
input,
textarea {
  width: 100%;
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.28);
  color: #e0d4ba;
  padding: 0.8rem;
  outline: none;
  text-transform: none;
  letter-spacing: 0;
}
input:focus,
textarea:focus {
  border-color: #f5c26b;
}
.preview-result {
  display: grid;
  gap: 1rem;
  text-align: center;
}
.preview-result p {
  color: rgba(224, 212, 186, 0.82);
  line-height: 1.7;
}
.result-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: center;
  flex-wrap: wrap;
}
.secondary-button {
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 999px;
  padding: 0.7rem 1rem;
  color: #f5c26b;
  background: transparent;
}
.error {
  margin-top: 1rem;
  color: #fca5a5;
  text-align: center;
  font-size: 0.85rem;
}
@media (max-width: 760px) {
  h1 {
    font-size: 2.6rem;
  }
  .feature-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (max-width: 520px) {
  .feature-grid {
    grid-template-columns: 1fr;
  }
  .preview-wait {
    grid-template-columns: 1fr;
    justify-items: center;
    text-align: center;
  }
  .mini-card-fan {
    width: 4.5rem;
  }
  .result-actions .glow-button,
  .result-actions .secondary-button {
    width: 100%;
  }
}
@media (prefers-reduced-motion: reduce) {
  .mini-card-fan span {
    animation: none;
  }
  .preview-progress span {
    transition: none;
  }
}
@keyframes mini-card-pulse {
  0%, 100% {
    filter: brightness(1);
  }
  50% {
    filter: brightness(1.2);
  }
}
</style>
