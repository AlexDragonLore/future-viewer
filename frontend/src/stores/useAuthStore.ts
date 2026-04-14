import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api/authApi'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('fv_token'))
  const email = ref<string | null>(localStorage.getItem('fv_email'))

  const isAuthenticated = computed(() => !!token.value)

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
  }

  async function register(e: string, password: string) {
    const response = await authApi.register(e, password)
    persist(response.accessToken, response.email)
  }

  function logout() {
    persist(null, null)
  }

  return { token, email, isAuthenticated, login, register, logout }
})
