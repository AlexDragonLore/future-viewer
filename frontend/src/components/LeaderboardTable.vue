<script setup lang="ts">
import type { LeaderboardEntry } from '@/types'

defineProps<{
  entries: LeaderboardEntry[]
  highlightUserId?: string | null
  emptyText?: string
}>()

function medal(rank: number): string {
  if (rank === 1) return '🥇'
  if (rank === 2) return '🥈'
  if (rank === 3) return '🥉'
  return ''
}
</script>

<template>
  <div v-if="entries.length === 0" class="empty" data-testid="leaderboard-empty">
    {{ emptyText ?? 'Пока никого. Будь первым.' }}
  </div>
  <table v-else class="leaderboard" data-testid="leaderboard-table">
    <thead>
      <tr>
        <th class="rank-col">#</th>
        <th class="name-col">Пользователь</th>
        <th class="num-col">Сумма</th>
        <th class="num-col">Откликов</th>
        <th class="num-col">Средний</th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="entry in entries"
        :key="entry.userId"
        :class="{ highlight: highlightUserId && entry.userId === highlightUserId }"
        data-testid="leaderboard-row"
      >
        <td class="rank-col">
          <span class="medal" v-if="medal(entry.rank)">{{ medal(entry.rank) }}</span>
          <span v-else>{{ entry.rank }}</span>
        </td>
        <td class="name-col">{{ entry.displayName }}</td>
        <td class="num-col">{{ entry.totalScore }}</td>
        <td class="num-col">{{ entry.feedbackCount }}</td>
        <td class="num-col">{{ entry.averageScore.toFixed(1) }}</td>
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.empty {
  text-align: center;
  padding: 2rem;
  color: rgba(224, 212, 186, 0.6);
  font-style: italic;
}
.leaderboard {
  width: 100%;
  border-collapse: collapse;
  font-family: 'Inter', system-ui, sans-serif;
}
.leaderboard thead th {
  padding: 0.65rem 0.75rem;
  text-align: left;
  font-family: 'Cinzel', serif;
  font-size: 0.7rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(245, 194, 107, 0.8);
  border-bottom: 1px solid rgba(245, 194, 107, 0.25);
}
.leaderboard tbody tr {
  border-bottom: 1px solid rgba(245, 194, 107, 0.08);
  transition: background 0.2s ease;
}
.leaderboard tbody tr:hover {
  background: rgba(245, 194, 107, 0.04);
}
.leaderboard tbody tr.highlight {
  background: rgba(245, 194, 107, 0.12);
  box-shadow: inset 2px 0 0 #f5c26b;
}
.leaderboard td {
  padding: 0.65rem 0.75rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.9rem;
}
.rank-col {
  width: 3.5rem;
  text-align: center;
}
.num-col {
  text-align: right;
  width: 6rem;
}
.medal {
  font-size: 1.1rem;
}
</style>
