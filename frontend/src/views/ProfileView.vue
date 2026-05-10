<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useProfileStore } from '@/stores/useProfileStore'
import { useAuthStore } from '@/stores/useAuthStore'
import ScoreBadge from '@/components/ScoreBadge.vue'
import TelegramLinkButton from '@/components/TelegramLinkButton.vue'
import { extractApiError } from '@/api/httpClient'
import { FeedbackStatus, type FeedbackInfo } from '@/types'

const store = useProfileStore()
const auth = useAuthStore()

const recentFeedbacks = computed(() => store.feedbacks.slice(0, 5))
const isLinked = computed(() => store.telegram?.isLinked ?? false)
const firstName = ref('')
const lastName = ref('')
const birthDate = ref('')
const personalizationError = ref<string | null>(null)
const personalizationSaved = ref(false)

watch(
  () => store.personalization,
  (value) => {
    firstName.value = value?.firstName ?? ''
    lastName.value = value?.lastName ?? ''
    birthDate.value = value?.birthDate ?? ''
  },
  { immediate: true },
)

onMounted(() => {
  store.loadAll()
})

function statusLabel(status: FeedbackStatus): string {
  switch (status) {
    case FeedbackStatus.Pending:
      return 'Ожидает'
    case FeedbackStatus.Notified:
      return 'Уведомлён'
    case FeedbackStatus.Answered:
      return 'Отвечено'
    case FeedbackStatus.Scored:
      return 'Оценено'
    default:
      return ''
  }
}

function averageScore(feedback: FeedbackInfo): number | null {
  return feedback.aiScore ?? null
}

async function savePersonalization() {
  personalizationError.value = null
  personalizationSaved.value = false
  try {
    await store.savePersonalization({
      firstName: firstName.value,
      lastName: lastName.value,
      birthDate: birthDate.value,
    })
    personalizationSaved.value = true
  } catch (e) {
    personalizationError.value = extractApiError(e, 'Не удалось сохранить знакомство')
  }
}

async function deleteMemory(id: string) {
  personalizationError.value = null
  try {
    await store.deleteMemoryRule(id)
  } catch (e) {
    personalizationError.value = extractApiError(e, 'Не удалось удалить память')
  }
}

async function clearMemory() {
  personalizationError.value = null
  try {
    await store.clearMemory()
  } catch (e) {
    personalizationError.value = extractApiError(e, 'Не удалось очистить память')
  }
}
</script>

<template>
  <main class="profile-page min-h-screen px-4 sm:px-6 py-12 sm:py-16 max-w-3xl mx-auto">
    <header class="mb-8 text-center">
      <div class="profile-kicker text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ ПРОФИЛЬ ✦</div>
      <h1 class="profile-title font-display text-4xl gold-text">{{ auth.email ?? 'Мой профиль' }}</h1>
    </header>

    <div v-if="store.loading" class="text-center text-mystic-silver/60" data-testid="profile-loading">
      загружаю…
    </div>
    <div v-else-if="store.error" class="text-center text-red-300" data-testid="profile-error">
      {{ store.error }}
    </div>

    <template v-else>
      <section class="mystic-card p-6 mb-6" data-testid="profile-summary">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Рейтинг</div>
        <div v-if="store.summary" class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div class="stat">
            <div class="stat-label">Сумма</div>
            <div class="stat-value">{{ store.summary.totalScore }}</div>
          </div>
          <div class="stat">
            <div class="stat-label">В этом месяце</div>
            <div class="stat-value">{{ store.summary.monthlyScore }}</div>
          </div>
          <div class="stat">
            <div class="stat-label">Место (всё время)</div>
            <div class="stat-value">{{ store.summary.rank ?? '—' }}</div>
          </div>
          <div class="stat">
            <div class="stat-label">Место (месяц)</div>
            <div class="stat-value">{{ store.summary.monthlyRank ?? '—' }}</div>
          </div>
          <div class="stat">
            <div class="stat-label">Откликов</div>
            <div class="stat-value">{{ store.summary.feedbackCount }}</div>
          </div>
          <div class="stat">
            <div class="stat-label">Средний балл</div>
            <div class="stat-value">{{ store.summary.averageScore.toFixed(1) }}</div>
          </div>
        </div>
        <div v-else class="text-mystic-silver/60 text-sm">
          Пока нет оценок — сделай расклад и поделись отзывом.
        </div>
      </section>

      <section class="mystic-card p-6 mb-6" data-testid="profile-telegram">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Telegram</div>
        <p class="text-sm text-mystic-silver/80 mb-3">
          <template v-if="isLinked">
            Уведомления привязаны. Отвяжи, чтобы перестать получать напоминания.
          </template>
          <template v-else>
            Привяжи аккаунт, чтобы получать уведомления и ссылку на отклик через день после расклада.
          </template>
        </p>
        <TelegramLinkButton :is-linked="isLinked" @update="store.loadTelegram()" />
      </section>

      <section class="mystic-card p-6 mb-6" data-testid="profile-personalization">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Знакомство и память</div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-4">
          <input v-model="firstName" class="profile-input" maxlength="80" placeholder="Имя" data-testid="profile-first-name" />
          <input v-model="lastName" class="profile-input" maxlength="80" placeholder="Фамилия" data-testid="profile-last-name" />
          <input v-model="birthDate" class="profile-input sm:col-span-2" type="date" data-testid="profile-birth-date" />
        </div>
        <button
          class="memory-action primary"
          :disabled="!firstName.trim() || !lastName.trim() || !birthDate"
          @click="savePersonalization"
          data-testid="save-personalization"
        >
          Сохранить
        </button>
        <p v-if="personalizationSaved" class="text-xs text-mystic-accent mt-2">Сохранено</p>
        <p v-if="personalizationError" class="text-xs text-red-300 mt-2">{{ personalizationError }}</p>

        <div class="memory-head">
          <span>Память AI</span>
          <button
            v-if="store.personalization?.memoryRules.length"
            class="memory-action"
            @click="clearMemory"
            data-testid="clear-memory"
          >
            Очистить
          </button>
        </div>
        <div v-if="!store.personalization?.memoryRules.length" class="text-sm text-mystic-silver/60">
          AI пока ничего не сохранил для будущих раскладов.
        </div>
        <ul v-else class="memory-list">
          <li v-for="rule in store.personalization.memoryRules" :key="rule.id" class="memory-row">
            <span>{{ rule.text }}</span>
            <button class="memory-action" @click="deleteMemory(rule.id)" data-testid="delete-memory-rule">Удалить</button>
          </li>
        </ul>
      </section>

      <section class="mystic-card p-6" data-testid="profile-feedbacks">
        <div class="text-xs uppercase tracking-widest text-mystic-accent/80 mb-3">Последние отклики</div>
        <div v-if="recentFeedbacks.length === 0" class="text-sm text-mystic-silver/60">
          Откликов пока нет.
        </div>
        <ul v-else class="space-y-3">
          <li v-for="f in recentFeedbacks" :key="f.id" class="feedback-row">
            <ScoreBadge :score="averageScore(f)" size="sm" />
            <div class="feedback-meta">
              <div class="feedback-question">«{{ f.question }}»</div>
              <div class="feedback-sub">
                <span>{{ statusLabel(f.status) }}</span>
                <span>·</span>
                <span>{{ new Date(f.createdAt).toLocaleDateString() }}</span>
              </div>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </main>
</template>

<style scoped>
.stat {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding: 0.6rem 0.75rem;
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid rgba(245, 194, 107, 0.15);
  border-radius: 10px;
}
.stat-label {
  font-size: 0.65rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: rgba(224, 212, 186, 0.65);
}
.stat-value {
  font-family: 'Cinzel', serif;
  font-size: 1.35rem;
  color: #f5c26b;
}
.feedback-row {
  display: flex;
  align-items: center;
  gap: 0.85rem;
}
.feedback-meta {
  flex: 1;
  min-width: 0;
}
.feedback-question {
  font-size: 0.9rem;
  color: rgba(232, 213, 242, 0.9);
  font-style: italic;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.feedback-sub {
  font-size: 0.72rem;
  color: rgba(224, 212, 186, 0.55);
  display: flex;
  gap: 0.5rem;
  margin-top: 0.15rem;
}
.profile-input {
  width: 100%;
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.25);
  padding: 0.7rem 0.8rem;
  color: #e0d4ba;
  outline: none;
}
.profile-input:focus {
  border-color: #f5c26b;
}
.memory-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin: 1.2rem 0 0.75rem;
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.8);
}
.memory-list {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}
.memory-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.7rem 0.8rem;
  border: 1px solid rgba(245, 194, 107, 0.15);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.22);
  color: rgba(224, 212, 186, 0.86);
  font-size: 0.86rem;
  line-height: 1.45;
}
.memory-action {
  flex: 0 0 auto;
  border: 1px solid rgba(245, 194, 107, 0.28);
  border-radius: 8px;
  padding: 0.45rem 0.7rem;
  color: #f5c26b;
  background: rgba(245, 194, 107, 0.06);
  font-size: 0.75rem;
}
.memory-action.primary {
  width: 100%;
  padding: 0.7rem;
}
.memory-action:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
@media (max-width: 640px) {
  .profile-page {
    padding-top: 2rem;
    padding-bottom: 2.5rem;
  }
  .profile-kicker {
    letter-spacing: 0.14em;
  }
  .profile-title {
    font-size: 2rem;
    line-height: 1.15;
    overflow-wrap: anywhere;
  }
  .feedback-row {
    align-items: flex-start;
  }
  .feedback-sub {
    flex-wrap: wrap;
    gap: 0.25rem 0.45rem;
  }
  .memory-row {
    flex-direction: column;
  }
}
</style>
