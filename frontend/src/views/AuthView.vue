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
const errorHint = ref<string | null>(null)
const busy = ref(false)
const info = ref<string | null>(null)
const needsVerification = ref(false)
const resendBusy = ref(false)

function getAuthError(e: unknown) {
  if (axios.isAxiosError(e)) {
    const status = e.response?.status
    const data = e.response?.data as { error?: string; message?: string } | undefined
    if (!e.response || e.message === 'Network Error') {
      return {
        message: 'Не удалось связаться с локальным API.',
        hint: 'Проверь, что backend запущен, подожди несколько секунд и попробуй снова.',
      }
    }
    if (status === 401 || data?.error === 'unauthorized' || data?.message === 'Invalid credentials') {
      return { message: 'Неверный email или пароль', hint: 'Проверь почту и пароль или восстанови доступ.' }
    }
    if (status === 403 && data?.error === 'email_not_verified') {
      return { message: 'Подтвердите почту, чтобы войти.', hint: 'Можно отправить письмо повторно.' }
    }
    if (status === 409 && data?.error === 'conflict') {
      return { message: 'Аккаунт с такой почтой уже существует.', hint: 'Переключись на вход и войди с этим email.' }
    }
  }
  return { message: extractApiError(e), hint: 'Проверь данные и попробуй ещё раз.' }
}

async function submit() {
  error.value = null
  errorHint.value = null
  info.value = null
  needsVerification.value = false
  busy.value = true
  try {
    if (mode.value === 'login') {
      await auth.login(email.value, password.value)
      const redirect = (route.query.redirect as string) || '/'
      router.replace(redirect)
    } else {
      const result = await auth.register(email.value, password.value)
      if (result.verificationRequired) {
        info.value = `Мы отправили письмо на ${result.email}. Перейдите по ссылке, чтобы подтвердить почту.`
      } else {
        await auth.login(email.value, password.value)
        const redirect = (route.query.redirect as string) || '/'
        router.replace(redirect)
      }
    }
  } catch (e) {
    const authError = getAuthError(e)
    error.value = authError.message
    errorHint.value = authError.hint
    if (axios.isAxiosError(e) && e.response?.status === 403 && e.response?.data?.error === 'email_not_verified') {
      needsVerification.value = true
    }
  } finally {
    busy.value = false
  }
}

async function resendVerification() {
  error.value = null
  errorHint.value = null
  info.value = null
  resendBusy.value = true
  try {
    await auth.resendVerification(email.value)
    info.value = 'Письмо отправлено повторно. Проверьте ящик.'
  } catch (e) {
    const authError = getAuthError(e)
    error.value = authError.message
    errorHint.value = authError.hint
  } finally {
    resendBusy.value = false
  }
}
</script>

<template>
  <main class="auth-page min-h-screen flex items-center justify-center px-4 sm:px-6 py-10">
    <section class="mystic-card p-6 sm:p-8 w-full max-w-md">
      <div class="auth-kicker text-mystic-accent text-xs tracking-[0.4em] mb-2 text-center">✦ ВРАТА ✦</div>
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
        <div v-if="error" class="auth-error">
          <p>{{ error }}</p>
          <p v-if="errorHint" class="auth-error-hint">{{ errorHint }}</p>
        </div>
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

      <div class="auth-secondary-actions">
        <div>
          <button class="auth-secondary-action" @click="mode = mode === 'login' ? 'register' : 'login'; info = null; error = null; errorHint = null; needsVerification = false">
            {{ mode === 'login' ? 'Создать аккаунт' : 'У меня уже есть аккаунт' }}
          </button>
        </div>
        <div v-if="mode === 'login'">
          <router-link to="/forgot-password" class="auth-secondary-action">
            Забыли пароль?
          </router-link>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.auth-error {
  border: 1px solid rgba(248, 113, 113, 0.35);
  border-radius: 0.75rem;
  background: rgba(127, 29, 29, 0.16);
  padding: 0.75rem 0.85rem;
  color: #fca5a5;
  font-size: 0.875rem;
  line-height: 1.45;
}
.auth-error-hint {
  margin-top: 0.35rem;
  color: rgba(224, 212, 186, 0.72);
  font-size: 0.78rem;
}
.auth-secondary-actions {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.35rem;
  margin-top: 1.25rem;
  text-align: center;
  font-size: 0.78rem;
  color: rgba(224, 212, 186, 0.72);
}
.auth-secondary-action {
  display: inline-flex;
  min-height: 2.75rem;
  align-items: center;
  justify-content: center;
  min-width: 11rem;
  padding: 0 0.9rem;
  color: inherit;
  text-decoration: underline;
  text-underline-offset: 0.18em;
  touch-action: manipulation;
  transition: color 0.2s ease;
}
.auth-secondary-action:hover {
  color: #f5c26b;
}
@media (max-width: 640px) {
  .auth-page {
    align-items: flex-start;
    padding-top: 2.5rem;
  }
  .auth-kicker {
    letter-spacing: 0.14em;
  }
}
</style>
