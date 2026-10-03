<script setup lang="ts">
import axios from 'axios'
import { computed, ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { extractApiError } from '@/api/httpClient'
import { legalDocumentVersions, loadLegalDocuments } from '@/content/legal'
import { trackGoal } from '@/analytics/metrika'
import type { RegisterPayload } from '@/types'
import { getGuestContinuation } from '@/utils/guestReading'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const mode = ref<'login' | 'register'>(route.query.mode === 'register' ? 'register' : 'login')
const continuesReading = Boolean(getGuestContinuation())
watch(mode, (value) => {
  if (value === 'register') trackGoal('registration_started')
}, { immediate: true })

function destination() {
  if (getGuestContinuation()) return '/result'
  const redirect = route.query.redirect
  return typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/'
}
const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const errorHint = ref<string | null>(null)
const busy = ref(false)
const info = ref<string | null>(route.query.accountDeletion === 'requested'
  ? 'Запрос на удаление аккаунта принят. Вход заблокирован, сеанс завершён. По вопросам выполнения запроса обратитесь к оператору через раздел «Права и обращения».'
  : null)
const needsVerification = ref(false)
const resendBusy = ref(false)
const personalDataConsentAccepted = ref(false)

const registrationReady = computed(() => personalDataConsentAccepted.value)

function getAuthError(e: unknown) {
  if (axios.isAxiosError(e)) {
    const status = e.response?.status
    const data = e.response?.data as { error?: string; message?: string } | undefined
    if (!e.response || e.message === 'Network Error') {
      return {
        message: 'Не удалось связаться с сервисом.',
        hint: 'Проверьте подключение к интернету и попробуйте ещё раз через несколько минут.',
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
      router.replace(destination())
    } else {
      if (!registrationReady.value) {
        error.value = 'Для регистрации нужно дать согласие на обработку персональных данных.'
        return
      }
      await loadLegalDocuments()
      const payload: RegisterPayload = {
        email: email.value,
        password: password.value,
        offerAccepted: true,
        privacyAcknowledged: true,
        personalDataConsentAccepted: true,
        ageConfirmed18: true,
        documentVersions: {
          offer: legalDocumentVersions.offer,
          privacy: legalDocumentVersions.privacy,
          personalDataConsent: legalDocumentVersions.personalDataConsent,
          marketingConsent: legalDocumentVersions.marketingConsent,
          cookies: legalDocumentVersions.cookies,
        },
        optionalConsents: { personalization: false, marketing: false, analytics: false },
        collectionSource: 'registration',
      }
      const result = await auth.register(payload)
      if (result.verificationRequired) {
        info.value = `Мы отправили письмо на ${result.email}. Перейдите по ссылке, чтобы подтвердить почту.${continuesReading ? ' Затем откроется полное толкование вашей карты.' : ''}`
        needsVerification.value = true
      } else {
        await auth.login(email.value, password.value)
        router.replace(destination())
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

      <p v-if="continuesReading" class="text-center text-sm text-mystic-silver/70 mb-6" data-testid="auth-continue-reading">
        Твоя карта уже открыта. {{ mode === 'register' ? 'Зарегистрируйся и подтверди почту' : 'Войди' }}, чтобы дочитать толкование бесплатно.
      </p>

      <form class="space-y-4" @submit.prevent="submit">
        <input
          v-model="email"
          type="email"
          placeholder="Электронная почта"
          autocomplete="email"
          aria-label="Электронная почта"
          required
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <input
          v-model="password"
          type="password"
          placeholder="Пароль"
          :autocomplete="mode === 'register' ? 'new-password' : 'current-password'"
          aria-label="Пароль"
          required
          minlength="8"
          class="w-full bg-black/30 border border-mystic-accent/30 rounded-lg p-3 focus:outline-none focus:border-mystic-accent"
        />
        <label v-if="mode === 'register'" class="registration-consent" data-testid="registration-consents">
          <input v-model="personalDataConsentAccepted" type="checkbox" required data-testid="personal-data-consent-acceptance" />
          <span>Даю <RouterLink to="/legal/personal-data-consent">согласие на обработку персональных данных</RouterLink>.</span>
        </label>
        <p v-if="mode === 'register'" class="registration-terms" data-testid="registration-terms">
          Нажимая «Создать», принимаю <RouterLink to="/legal/offer">оферту</RouterLink>, подтверждаю ознакомление с
          <RouterLink to="/legal/privacy">политикой конфиденциальности</RouterLink> и возраст 18+.
        </p>
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
        <button
          type="submit"
          class="glow-button w-full"
          :disabled="busy || (mode === 'register' && !registrationReady)"
        >
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
.registration-consent {
  display: flex;
  align-items: flex-start;
  gap: 0.65rem;
  color: rgba(224, 212, 186, 0.88);
  font-size: 0.8rem;
  line-height: 1.55;
  cursor: pointer;
}
.registration-consent input {
  flex: 0 0 auto;
  width: 1rem;
  height: 1rem;
  margin-top: 0.2rem;
  accent-color: #f5c26b;
}
.registration-terms {
  color: rgba(224, 212, 186, 0.65);
  font-size: 0.73rem;
  line-height: 1.6;
}
.registration-consent a,
.registration-terms a {
  color: #f5c26b;
  text-decoration: underline;
  text-underline-offset: 0.15em;
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
