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
  <template v-else>
    <table class="leaderboard" data-testid="leaderboard-table">
      <thead>
        <tr>
          <th class="rank-col">#</th>
          <th class="name-col">Пользователь</th>
          <th class="num-col">Итог</th>
          <th class="num-col mobile-hide">Откликов</th>
          <th class="num-col mobile-hide">Средний</th>
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
          <td class="num-col">
            <div class="total-score">{{ entry.totalScore }}</div>
            <div class="score-breakdown" data-testid="score-breakdown">
              ★ {{ entry.feedbackScore }} / ✦ {{ entry.achievementScore }}
            </div>
          </td>
          <td class="num-col mobile-hide">{{ entry.feedbackCount }}</td>
          <td class="num-col mobile-hide">{{ entry.feedbackCount > 0 ? entry.averageScore.toFixed(1) : '—' }}</td>
        </tr>
      </tbody>
    </table>

    <ul class="leaderboard-cards" data-testid="leaderboard-mobile-list">
      <li
        v-for="entry in entries"
        :key="`mobile-${entry.userId}`"
        class="leaderboard-card"
        :class="{ highlight: highlightUserId && entry.userId === highlightUserId }"
      >
        <div class="mobile-rank">
          <span class="medal" v-if="medal(entry.rank)">{{ medal(entry.rank) }}</span>
          <span v-else>#{{ entry.rank }}</span>
        </div>
        <div class="mobile-main">
          <div class="mobile-name">{{ entry.displayName }}</div>
          <div class="score-breakdown">★ {{ entry.feedbackScore }} / ✦ {{ entry.achievementScore }}</div>
          <div class="mobile-meta">
            <span>Откликов: {{ entry.feedbackCount }}</span>
            <span>Средний: {{ entry.feedbackCount > 0 ? entry.averageScore.toFixed(1) : '—' }}</span>
          </div>
        </div>
        <div class="mobile-total">
          <span>{{ entry.totalScore }}</span>
          <small>Итог</small>
        </div>
      </li>
    </ul>
  </template>
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
.leaderboard-cards {
  display: none;
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
.total-score {
  font-weight: 600;
}
.score-breakdown {
  font-size: 0.7rem;
  color: rgba(224, 212, 186, 0.55);
  margin-top: 0.15rem;
  font-family: 'Inter', system-ui, sans-serif;
  letter-spacing: 0.02em;
}
@media (max-width: 640px) {
  .leaderboard {
    display: none;
  }
  .leaderboard-cards {
    display: flex;
    flex-direction: column;
    gap: 0.55rem;
    padding: 0.55rem;
    margin: 0;
    list-style: none;
  }
  .leaderboard-card {
    display: grid;
    grid-template-columns: 2.3rem minmax(0, 1fr) auto;
    gap: 0.65rem;
    align-items: center;
    padding: 0.75rem;
    border: 1px solid rgba(245, 194, 107, 0.14);
    border-radius: 10px;
    background: rgba(0, 0, 0, 0.18);
  }
  .leaderboard-card.highlight {
    border-color: rgba(245, 194, 107, 0.55);
    background: rgba(245, 194, 107, 0.12);
  }
  .mobile-rank {
    text-align: center;
    color: #f5c26b;
    font-family: 'Cinzel', serif;
  }
  .mobile-main {
    min-width: 0;
  }
  .mobile-name {
    color: rgba(224, 212, 186, 0.95);
    font-weight: 600;
    overflow-wrap: anywhere;
  }
  .mobile-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem 0.65rem;
    margin-top: 0.25rem;
    color: rgba(224, 212, 186, 0.6);
    font-size: 0.72rem;
  }
  .mobile-total {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    color: #f5c26b;
    font-family: 'Cinzel', serif;
    line-height: 1.1;
  }
  .mobile-total small {
    margin-top: 0.15rem;
    color: rgba(224, 212, 186, 0.55);
    font-family: 'Inter', system-ui, sans-serif;
    font-size: 0.62rem;
    letter-spacing: 0;
  }
  .leaderboard td,
  .leaderboard thead th {
    padding: 0.55rem 0.4rem;
    font-size: 0.85rem;
  }
  .mobile-hide {
    display: none;
  }
  .num-col {
    width: auto;
  }
}
</style>
