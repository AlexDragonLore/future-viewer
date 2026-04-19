<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useLeaderboardStore } from '@/stores/useLeaderboardStore'
import { useAuthStore } from '@/stores/useAuthStore'
import LeaderboardTable from '@/components/LeaderboardTable.vue'

type Tab = 'monthly' | 'alltime'

const store = useLeaderboardStore()
const auth = useAuthStore()

const tab = ref<Tab>('monthly')

const currentUserId = computed(() => auth.token ? parseSubFromToken(auth.token) : null)
const entries = computed(() => (tab.value === 'monthly' ? store.monthly : store.allTime))

onMounted(async () => {
  await store.loadMonthly()
})

async function select(next: Tab) {
  tab.value = next
  if (next === 'monthly' && store.monthly.length === 0) await store.loadMonthly()
  if (next === 'alltime' && store.allTime.length === 0) await store.loadAllTime()
}

function parseSubFromToken(token: string): string | null {
  try {
    const payload = token.split('.')[1]
    if (!payload) return null
    const padded = payload.replace(/-/g, '+').replace(/_/g, '/')
    const json = atob(padded + '==='.slice((padded.length + 3) % 4))
    const data = JSON.parse(json) as { sub?: string; nameid?: string }
    return data.sub ?? data.nameid ?? null
  } catch {
    return null
  }
}
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-3xl mx-auto">
    <header class="mb-8 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ РЕЙТИНГ ✦</div>
      <h1 class="font-display text-4xl gold-text">Лидерборд</h1>
    </header>

    <div class="tabs mb-6" role="tablist">
      <button
        class="tab"
        :class="{ active: tab === 'monthly' }"
        role="tab"
        :aria-selected="tab === 'monthly'"
        data-testid="tab-monthly"
        @click="select('monthly')"
      >
        Этот месяц
      </button>
      <button
        class="tab"
        :class="{ active: tab === 'alltime' }"
        role="tab"
        :aria-selected="tab === 'alltime'"
        data-testid="tab-alltime"
        @click="select('alltime')"
      >
        Всё время
      </button>
    </div>

    <div v-if="store.loading" class="text-center text-mystic-silver/60" data-testid="leaderboard-loading">
      загружаю…
    </div>
    <div v-else-if="store.error" class="text-center text-red-300" data-testid="leaderboard-error">
      {{ store.error }}
    </div>
    <section v-else class="mystic-card overflow-hidden">
      <LeaderboardTable :entries="entries" :highlight-user-id="currentUserId" />
    </section>
  </main>
</template>

<style scoped>
.tabs {
  display: flex;
  gap: 0.5rem;
  justify-content: center;
}
.tab {
  padding: 0.5rem 1.25rem;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(0, 0, 0, 0.25);
  color: rgba(224, 212, 186, 0.85);
  font-family: 'Cinzel', serif;
  font-size: 0.75rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  border-radius: 999px;
  cursor: pointer;
  transition: all 0.2s ease;
}
.tab:hover {
  border-color: rgba(245, 194, 107, 0.6);
  color: #f5c26b;
}
.tab.active {
  background: rgba(245, 194, 107, 0.12);
  border-color: #f5c26b;
  color: #f5c26b;
  box-shadow: 0 0 16px rgba(245, 194, 107, 0.35);
}
</style>
