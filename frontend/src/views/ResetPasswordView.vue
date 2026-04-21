<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { extractApiError } from '@/api/httpClient'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const token = computed(() => (route.query.token as string | undefined) ?? '')
const password = ref('')
const confirm = ref('')
const busy = ref(false)
const error = ref<string | null>(null)

async function submit() {
  if (!token.value) {
    error.value = 'Ссылка недействительна.'
    return
  }
  if (password.value !== confirm.value) {
    error.value = 'Пароли не совпадают.'
    return
  }
  busy.value = true
  error.value = null
  try {
    await auth.resetPassword(token.value, password.value)
    router.replace('/')
  } catch (e) {
    error.value = extractApiError(e)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="min-h-screen flex items-center justify-center px-6">
    <section class="mystic-card p-8 w-full max-w-md">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2 text-center">✦ ВРАТА ✦</div>
      <h1 class="font-display text-3xl gold-text text-center mb-6">Новый пароль</h1>

      <div v-if="!token" class="text-center space-y-4">
        <p class="text-sm text-red-300">Ссылка недействительна или истекла.</p>
        <router-link to="/forgot-password" class="underline text-mystic-accent text-sm">
          Запросить новое письмо
        </router-link>
      </div>

      <form v-else class="space-y-4" @submit.prevent="submit">
        <input
          v-model="password"
          type="password"
          placeholder="новый пароль"
          required
          minlength="8"
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <input
          v-model="confirm"
          type="password"
          placeholder="повторите пароль"
          required
          minlength="8"
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <div v-if="error" class="text-sm text-red-300">{{ error }}</div>
        <button type="submit" class="glow-button w-full" :disabled="busy">
          {{ busy ? '...' : 'Сохранить пароль' }}
        </button>
      </form>
    </section>
  </main>
</template>
