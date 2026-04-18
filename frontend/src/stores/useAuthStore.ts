import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api/authApi'
import { subscriptionApi } from '@/api/subscriptionApi'
import type { SubscriptionStatus } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('fv_token'))
  const email = ref<string | null>(localStorage.getItem('fv_email'))
  const subscription = ref<SubscriptionStatus | null>(null)
  const subscriptionLoading = ref(false)

  const isAuthenticated = computed(() => !!token.value)
  const isSubscribed = computed(() => subscription.value?.isActive ?? false)
  const canCreateReading = computed(() => subscription.value?.canCreateFreeReading ?? true)

  function persist(newToken: string | null, newEmail: string | null) {
    token.value = newToken
    email.value = newEmail
    if (newToken) localStorage.setItem('fv_token', newToken)
    else localStorage.removeItem('fv_token')
    if (newEmail) localStorage.setItem('fv_email', newEmail)
    else localStorage.removeItem('fv_email')
  }

  async function login(e: string, password: string) {
    const response = await authApi.login(e, password)
    persist(response.accessToken, response.email)
    void refreshSubscription()
  }

  async function register(e: string, password: string) {
    const response = await authApi.register(e, password)
    persist(response.accessToken, response.email)
    void refreshSubscription()
  }

  function logout() {
    persist(null, null)
    subscription.value = null
  }

  async function refreshSubscription() {
    if (!token.value) {
      subscription.value = null
      return
    }
    subscriptionLoading.value = true
    try {
      subscription.value = await subscriptionApi.status()
    } catch {
      subscription.value = null
    } finally {
      subscriptionLoading.value = false
    }
  }

  return {
    token,
    email,
    subscription,
    subscriptionLoading,
    isAuthenticated,
    isSubscribed,
    canCreateReading,
    login,
    register,
    logout,
    refreshSubscription,
  }
})
