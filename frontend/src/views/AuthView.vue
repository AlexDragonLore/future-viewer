<script setup lang="ts">
import axios from 'axios'
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { extractApiError } from '@/api/httpClient'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const mode = ref<'login' | 'register'>('login')
const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const busy = ref(false)
const info = ref<string | null>(null)
const needsVerification = ref(false)
const resendBusy = ref(false)

async function submit() {
  busy.value = true
  error.value = null
  info.value = null
  needsVerification.value = false
  try {
    if (mode.value === 'login') {
      await auth.login(email.value, password.value)
      const redirect = (route.query.redirect as string) || '/'
      router.replace(redirect)
    } else {
      const result = await auth.register(email.value, password.value)
      if (result.verificationRequired) {
        info.value = `Мы отправили письмо на ${result.email}. Перейдите по ссылке, чтобы подтвердить почту.`
      }
    }
  } catch (e) {
    error.value = extractApiError(e)
    if (axios.isAxiosError(e) && e.response?.status === 403 && e.response?.data?.error === 'email_not_verified') {
      needsVerification.value = true
    }
  } finally {
    busy.value = false
  }
}

async function resendVerification() {
  resendBusy.value = true
  error.value = null
  info.value = null
  try {
    await auth.resendVerification(email.value)
    info.value = 'Письмо отправлено повторно. Проверьте ящик.'
  } catch (e) {
    error.value = extractApiError(e)
  } finally {
    resendBusy.value = false
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
        <div v-if="info" class="text-sm text-mystic-accent">{{ info }}</div>
        <button
          v-if="needsVerification"
          type="button"
          class="w-full text-sm underline text-mystic-accent"
          :disabled="resendBusy"
          @click="resendVerification"
        >
          {{ resendBusy ? '...' : 'Отправить письмо повторно' }}
        </button>
        <button type="submit" class="glow-button w-full" :disabled="busy">
          {{ busy ? '...' : mode === 'login' ? 'Войти' : 'Создать' }}
        </button>
      </form>

      <div class="text-center text-xs text-mystic-silver/60 mt-6 space-y-2">
        <div>
          <button class="underline hover:text-mystic-accent" @click="mode = mode === 'login' ? 'register' : 'login'; info = null; error = null; needsVerification = false">
            {{ mode === 'login' ? 'Создать аккаунт' : 'У меня уже есть аккаунт' }}
          </button>
        </div>
        <div v-if="mode === 'login'">
          <router-link to="/forgot-password" class="underline hover:text-mystic-accent">
            Забыли пароль?
          </router-link>
        </div>
      </div>
    </section>
  </main>
</template>
