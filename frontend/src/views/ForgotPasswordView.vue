<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '@/stores/useAuthStore'
import { extractApiError } from '@/api/httpClient'

const auth = useAuthStore()

const email = ref('')
const busy = ref(false)
const error = ref<string | null>(null)
const sent = ref(false)

async function submit() {
  busy.value = true
  error.value = null
  try {
    await auth.forgotPassword(email.value)
    sent.value = true
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
      <h1 class="font-display text-3xl gold-text text-center mb-6">Забыли пароль?</h1>

      <div v-if="sent" class="text-center space-y-4">
        <p class="text-sm text-mystic-accent">
          Если такой email существует, мы отправили письмо с инструкциями. Проверьте почтовый ящик.
        </p>
        <router-link to="/auth" class="underline text-mystic-accent text-sm">
          Вернуться ко входу
        </router-link>
      </div>

      <form v-else class="space-y-4" @submit.prevent="submit">
        <p class="text-sm text-mystic-silver/70">
          Введите email, с которым регистрировались — мы пришлём ссылку для восстановления пароля.
        </p>
        <input
          v-model="email"
          type="email"
          placeholder="email"
          required
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <div v-if="error" class="text-sm text-red-300">{{ error }}</div>
        <button type="submit" class="glow-button w-full" :disabled="busy">
          {{ busy ? '...' : 'Отправить письмо' }}
        </button>
        <div class="text-center text-xs text-mystic-silver/60">
          <router-link to="/auth" class="underline hover:text-mystic-accent">
            Вернуться ко входу
          </router-link>
        </div>
      </form>
    </section>
  </main>
</template>
