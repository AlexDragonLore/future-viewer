<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useProfileStore } from '@/stores/useProfileStore'
import { useAuthStore } from '@/stores/useAuthStore'
import ScoreBadge from '@/components/ScoreBadge.vue'
import TelegramLinkButton from '@/components/TelegramLinkButton.vue'
import { FeedbackStatus, type FeedbackInfo } from '@/types'

const store = useProfileStore()
const auth = useAuthStore()

const recentFeedbacks = computed(() => store.feedbacks.slice(0, 5))
const isLinked = computed(() => store.telegram?.isLinked ?? false)

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
    case FeedbackStatus.Expired:
      return 'Истекло'
    default:
      return ''
  }
}

function averageScore(feedback: FeedbackInfo): number | null {
  return feedback.aiScore ?? null
}
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-3xl mx-auto">
    <header class="mb-8 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ ПРОФИЛЬ ✦</div>
      <h1 class="font-display text-4xl gold-text">{{ auth.email ?? 'Мой профиль' }}</h1>
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
</style>
