import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api/authApi'
import { subscriptionApi } from '@/api/subscriptionApi'
import type { RegisterResponse, SubscriptionStatus } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('fv_token'))
  const email = ref<string | null>(localStorage.getItem('fv_email'))
  const userId = ref<string | null>(localStorage.getItem('fv_user_id'))
  const isAdmin = ref<boolean>(localStorage.getItem('fv_is_admin') === 'true')
  const subscription = ref<SubscriptionStatus | null>(null)
  const subscriptionLoading = ref(false)

  const isAuthenticated = computed(() => !!token.value)
  const isSubscribed = computed(() => subscription.value?.isActive ?? false)
  const canCreateReading = computed(() => subscription.value?.canCreateFreeReading ?? true)

  function persist(newToken: string | null, newEmail: string | null, newUserId: string | null, newIsAdmin: boolean) {
    token.value = newToken
    email.value = newEmail
    userId.value = newUserId
    isAdmin.value = newIsAdmin
    if (newToken) localStorage.setItem('fv_token', newToken)
    else localStorage.removeItem('fv_token')
    if (newEmail) localStorage.setItem('fv_email', newEmail)
    else localStorage.removeItem('fv_email')
    if (newUserId) localStorage.setItem('fv_user_id', newUserId)
    else localStorage.removeItem('fv_user_id')
    if (newIsAdmin) localStorage.setItem('fv_is_admin', 'true')
    else localStorage.removeItem('fv_is_admin')
  }

  async function login(e: string, password: string) {
    const response = await authApi.login(e, password)
    persist(response.accessToken, response.email, response.userId, response.isAdmin)
    void refreshSubscription()
  }

  async function register(e: string, password: string): Promise<RegisterResponse> {
    return await authApi.register(e, password)
  }

  async function verifyEmail(token: string) {
    const response = await authApi.verifyEmail(token)
    persist(response.accessToken, response.email, response.userId, response.isAdmin)
    void refreshSubscription()
  }

  async function resendVerification(e: string) {
    await authApi.resendVerification(e)
  }

  function logout() {
    persist(null, null, null, false)
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
    userId,
    isAdmin,
    subscription,
    subscriptionLoading,
    isAuthenticated,
    isSubscribed,
    canCreateReading,
    login,
    register,
    verifyEmail,
    resendVerification,
    logout,
    refreshSubscription,
  }
})
