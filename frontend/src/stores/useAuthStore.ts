import { clearAccountSession, accountSessionVersion } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api/authApi'
import { subscriptionApi } from '@/api/subscriptionApi'
import { trackGoal, trackGoalOnce } from '@/analytics/metrika'
import type { RegisterPayload, RegisterResponse, SubscriptionStatus } from '@/types'

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
  const canCreateIntroReading = computed(() => subscription.value?.canCreateIntroReading ?? false)

  function persist(newToken: string | null, newEmail: string | null, newUserId: string | null, newIsAdmin: boolean) {
    clearAccountSession()
    subscription.value = null
    subscriptionLoading.value = false
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

  async function register(payload: RegisterPayload): Promise<RegisterResponse> {
    const response = await authApi.register(payload)
    trackGoal('registration_submitted')
    return response
  }

  async function verifyEmail(token: string) {
    const response = await authApi.verifyEmail(token)
    persist(response.accessToken, response.email, response.userId, response.isAdmin)
    trackGoalOnce('email_verified', response.userId)
    void refreshSubscription()
  }

  async function resendVerification(e: string) {
    await authApi.resendVerification(e)
  }

  async function forgotPassword(e: string) {
    await authApi.forgotPassword(e)
  }

  async function resetPassword(resetToken: string, newPassword: string) {
    const response = await authApi.resetPassword(resetToken, newPassword)
    persist(response.accessToken, response.email, response.userId, response.isAdmin)
    void refreshSubscription()
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
    const session = accountSessionVersion()
    subscriptionLoading.value = true
    try {
      const value = await subscriptionApi.status()
      if (session === accountSessionVersion()) subscription.value = value
    } catch {
      if (session === accountSessionVersion()) subscription.value = null
    } finally {
      if (session === accountSessionVersion()) subscriptionLoading.value = false
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
    canCreateIntroReading,
    login,
    register,
    verifyEmail,
    resendVerification,
    forgotPassword,
    resetPassword,
    logout,
    refreshSubscription,
  }
})
