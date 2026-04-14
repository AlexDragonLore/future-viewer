<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const mode = ref<'login' | 'register'>('login')
const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const busy = ref(false)

async function submit() {
  busy.value = true
  error.value = null
  try {
    if (mode.value === 'login') await auth.login(email.value, password.value)
    else await auth.register(email.value, password.value)
    const redirect = (route.query.redirect as string) || '/'
    router.replace(redirect)
  } catch (e: any) {
    error.value = e.response?.data?.message ?? e.message ?? 'Что-то пошло не так'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="min-h-screen flex items-center justify-center px-6">
    <section class="mystic-card p-8 w-full max-w-md">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2 text-center">✦ ВРАТА ✦</div>
      <h1 class="font-display text-3xl gold-text text-center mb-6">
        {{ mode === 'login' ? 'Войти' : 'Регистрация' }}
      </h1>

      <form class="space-y-4" @submit.prevent="submit">
        <input
          v-model="email"
          type="email"
          placeholder="email"
          required
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <input
          v-model="password"
          type="password"
          placeholder="пароль"
          required
          minlength="8"
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <div v-if="error" class="text-sm text-red-300">{{ error }}</div>
        <button type="submit" class="glow-button w-full" :disabled="busy">
          {{ busy ? '...' : mode === 'login' ? 'Войти' : 'Создать' }}
        </button>
      </form>

      <div class="text-center text-xs text-mystic-silver/60 mt-6">
        <button class="underline hover:text-mystic-accent" @click="mode = mode === 'login' ? 'register' : 'login'">
          {{ mode === 'login' ? 'Создать аккаунт' : 'У меня уже есть аккаунт' }}
        </button>
      </div>
    </section>
  </main>
</template>
