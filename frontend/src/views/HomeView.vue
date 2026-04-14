<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'
import { useAuthStore } from '@/stores/useAuthStore'
import { SpreadType } from '@/types'

const router = useRouter()
const store = useReadingStore()
const auth = useAuthStore()

const question = ref('')
const spreadType = ref<SpreadType>(SpreadType.ThreeCard)

onMounted(() => store.loadSpreads())

function begin() {
  if (!question.value.trim()) return
  sessionStorage.setItem('fv_pending', JSON.stringify({ spreadType: spreadType.value, question: question.value }))
  router.push({ name: 'reading' })
}
</script>

<template>
  <main class="min-h-screen flex flex-col items-center justify-center px-6 py-16">
    <header class="text-center mb-10">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-3">✦ FUTURE VIEWER ✦</div>
      <h1 class="font-display text-5xl md:text-7xl gold-text mb-4">Загляни за Вуаль</h1>
      <p class="text-mystic-silver/70 max-w-xl mx-auto">
        Задай вопрос Вселенной. Карты Таро раскроют тайные нити судьбы и покажут путь сквозь туман грядущего.
      </p>
    </header>

    <section class="mystic-card w-full max-w-xl p-8 space-y-6">
      <div>
        <label class="block text-xs uppercase tracking-widest text-mystic-accent/80 mb-2">Расклад</label>
        <div class="grid grid-cols-3 gap-3">
          <button
            v-for="s in store.spreads.length ? store.spreads : [
              { type: 1, name: 'Карта дня', cardCount: 1 },
              { type: 3, name: 'Три карты', cardCount: 3 },
              { type: 10, name: 'Кельтский крест', cardCount: 10 }
            ]"
            :key="s.type"
            class="spread-option"
            :class="{ active: spreadType === s.type }"
            @click="spreadType = s.type as SpreadType"
          >
            <div class="text-sm font-display">{{ s.name }}</div>
            <div class="text-xs text-mystic-silver/60 mt-1">{{ s.cardCount }} карт(ы)</div>
          </button>
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

      <button class="glow-button w-full" :disabled="!question.trim()" @click="begin">
        Начать расклад
      </button>

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
</style>
