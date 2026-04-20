<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { useDeckStore } from '@/stores/useDeckStore'
import { DeckType } from '@/types'

const auth = useAuthStore()
const deck = useDeckStore()
const router = useRouter()

const menuOpen = ref(false)
const deckOpen = ref(false)

const deckOptions: { value: DeckType; label: string }[] = [
  { value: DeckType.RWS, label: 'Rider–Waite–Smith' },
  { value: DeckType.Thoth, label: 'Thoth' },
  { value: DeckType.Marseille, label: 'Marseille' },
  { value: DeckType.ViscontiSforza, label: 'Visconti–Sforza' },
  { value: DeckType.ModernWitch, label: 'Modern Witch' },
]

const currentDeckLabel = computed(
  () => deckOptions.find((o) => o.value === deck.current)?.label ?? 'RWS',
)

function selectDeck(value: DeckType) {
  deck.select(value)
  deckOpen.value = false
}

onMounted(async () => {
  if (auth.isAuthenticated && !auth.subscription) {
    await auth.refreshSubscription()
  }
})

function handleLogout() {
  auth.logout()
  menuOpen.value = false
  router.push({ name: 'home' })
}

const quotaLabel = computed(() => {
  if (!auth.isAuthenticated) return null
  if (auth.isSubscribed) return '∞'
  const s = auth.subscription
  if (!s) return null
  const left = Math.max(0, s.freeReadingsDailyLimit - s.freeReadingsUsedToday)
  return `${left}/${s.freeReadingsDailyLimit}`
})
</script>

<template>
  <header class="site-header" data-testid="site-header">
    <div class="header-inner">
      <RouterLink to="/" class="logo" data-testid="site-logo">
        <span class="logo-mark">✦</span>
        <span class="logo-text gold-text">Вуаль Грядущего</span>
      </RouterLink>

      <nav class="links">
        <RouterLink to="/glossary" class="nav-link" data-testid="nav-glossary">Глоссарий</RouterLink>
        <RouterLink to="/leaderboard" class="nav-link" data-testid="nav-leaderboard">Лидерборд</RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/history" class="nav-link" data-testid="nav-history">
          История
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated" to="/achievements" class="nav-link" data-testid="nav-achievements">
          Ачивки
        </RouterLink>
        <RouterLink v-if="auth.isAuthenticated && auth.isAdmin" to="/admin" class="nav-link" data-testid="nav-admin">
          Админ
        </RouterLink>
      </nav>

      <div class="right">
        <div class="deck-picker" data-testid="deck-picker">
          <button
            class="deck-button"
            type="button"
            aria-haspopup="listbox"
            :aria-expanded="deckOpen"
            @click="deckOpen = !deckOpen"
          >
            <span class="deck-icon">◆</span>
            <span class="deck-label">{{ currentDeckLabel }}</span>
            <span class="chev">▾</span>
          </button>
          <ul v-if="deckOpen" class="deck-menu" role="listbox">
            <li v-for="opt in deckOptions" :key="opt.value">
              <button
                class="deck-option"
                :class="{ active: opt.value === deck.current }"
                role="option"
                :aria-selected="opt.value === deck.current"
                type="button"
                @click="selectDeck(opt.value)"
              >
                {{ opt.label }}
              </button>
            </li>
          </ul>
        </div>

        <template v-if="auth.isAuthenticated">
          <div class="quota" v-if="quotaLabel" data-testid="header-quota" :class="{ subscribed: auth.isSubscribed }">
            {{ quotaLabel }}
          </div>
          <div class="user-menu">
            <button class="user-button" type="button" @click="menuOpen = !menuOpen" data-testid="user-button">
              <span class="user-email">{{ auth.email }}</span>
              <span class="chev">▾</span>
            </button>
            <ul v-if="menuOpen" class="user-dropdown">
              <li>
                <RouterLink to="/profile" class="dd-link" @click="menuOpen = false" data-testid="nav-profile">Профиль</RouterLink>
              </li>
              <li>
                <RouterLink to="/history" class="dd-link" @click="menuOpen = false">История раскладов</RouterLink>
              </li>
              <li>
                <RouterLink to="/achievements" class="dd-link" @click="menuOpen = false">Достижения</RouterLink>
              </li>
              <li>
                <button class="dd-link as-btn" type="button" @click="handleLogout" data-testid="logout-button">
                  Выйти
                </button>
              </li>
            </ul>
          </div>
        </template>
        <template v-else>
          <RouterLink to="/auth" class="auth-link" data-testid="nav-auth">Войти</RouterLink>
        </template>
      </div>
    </div>
  </header>
</template>

<style scoped>
.site-header {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 30;
  backdrop-filter: blur(14px);
  -webkit-backdrop-filter: blur(14px);
  background: linear-gradient(180deg, rgba(11, 6, 24, 0.75), rgba(11, 6, 24, 0.45));
  border-bottom: 1px solid rgba(245, 194, 107, 0.18);
  box-shadow: 0 2px 24px rgba(0, 0, 0, 0.35);
}
.header-inner {
  max-width: 78rem;
  margin: 0 auto;
  padding: 0.75rem 1.25rem;
  display: flex;
  align-items: center;
  gap: 1.25rem;
}
.logo {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  text-decoration: none;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.14em;
  font-size: 0.95rem;
}
.logo-mark {
  color: #f5c26b;
  font-size: 1rem;
  text-shadow: 0 0 10px rgba(245, 194, 107, 0.7);
}
.links {
  display: flex;
  gap: 1.25rem;
  margin-left: 0.5rem;
}
.nav-link {
  color: rgba(224, 212, 186, 0.8);
  text-decoration: none;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.12em;
  font-size: 0.75rem;
  text-transform: uppercase;
  padding: 0.25rem 0.25rem;
  transition: color 0.2s ease, text-shadow 0.2s ease;
}
.nav-link:hover,
.nav-link.router-link-active {
  color: #f5c26b;
  text-shadow: 0 0 8px rgba(245, 194, 107, 0.5);
}
.right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.deck-picker {
  position: relative;
}
.deck-button,
.user-button {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.4rem 0.75rem;
  border-radius: 999px;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(0, 0, 0, 0.25);
  color: rgba(224, 212, 186, 0.9);
  font-family: 'Cinzel', serif;
  letter-spacing: 0.1em;
  font-size: 0.72rem;
  cursor: pointer;
  transition: border-color 0.2s ease, background 0.2s ease, color 0.2s ease;
}
.deck-button:hover,
.user-button:hover {
  border-color: rgba(245, 194, 107, 0.7);
  background: rgba(245, 194, 107, 0.08);
  color: #f5c26b;
}
.deck-icon {
  color: #f5c26b;
}
.chev {
  font-size: 0.6rem;
  opacity: 0.8;
}
.deck-menu,
.user-dropdown {
  position: absolute;
  top: calc(100% + 0.4rem);
  right: 0;
  min-width: 12rem;
  list-style: none;
  margin: 0;
  padding: 0.35rem;
  border-radius: 12px;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(11, 6, 24, 0.92);
  backdrop-filter: blur(12px);
  box-shadow: 0 16px 40px rgba(0, 0, 0, 0.55);
  z-index: 40;
}
.deck-option,
.dd-link {
  display: block;
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  padding: 0.5rem 0.75rem;
  border-radius: 8px;
  color: rgba(224, 212, 186, 0.85);
  font-family: 'Cinzel', serif;
  font-size: 0.75rem;
  letter-spacing: 0.08em;
  cursor: pointer;
  text-decoration: none;
  transition: background 0.2s ease, color 0.2s ease;
}
.deck-option:hover,
.dd-link:hover {
  background: rgba(245, 194, 107, 0.1);
  color: #f5c26b;
}
.deck-option.active {
  color: #f5c26b;
  background: rgba(245, 194, 107, 0.08);
}
.dd-link.as-btn {
  color: rgba(252, 165, 165, 0.9);
}
.quota {
  padding: 0.3rem 0.6rem;
  border-radius: 999px;
  border: 1px solid rgba(245, 194, 107, 0.35);
  background: rgba(0, 0, 0, 0.25);
  font-family: 'Cinzel', serif;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.72rem;
  letter-spacing: 0.08em;
}
.quota.subscribed {
  color: #f5c26b;
  border-color: rgba(245, 194, 107, 0.7);
  background: rgba(245, 194, 107, 0.1);
}
.user-menu {
  position: relative;
}
.user-email {
  max-width: 10rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.auth-link {
  color: #f5c26b;
  text-decoration: none;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.14em;
  font-size: 0.75rem;
  text-transform: uppercase;
  padding: 0.4rem 0.9rem;
  border-radius: 999px;
  border: 1px solid rgba(245, 194, 107, 0.45);
  transition: background 0.2s ease, box-shadow 0.2s ease;
}
.auth-link:hover {
  background: rgba(245, 194, 107, 0.12);
  box-shadow: 0 0 20px rgba(245, 194, 107, 0.3);
}

@media (max-width: 640px) {
  .links {
    display: none;
  }
  .user-email {
    max-width: 6rem;
  }
  .deck-label {
    display: none;
  }
}
</style>
