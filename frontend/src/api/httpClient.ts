import axios, { AxiosError } from 'axios'

export const httpClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  headers: { 'Content-Type': 'application/json' },
})

httpClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('fv_token')
  if (token && config.headers) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

httpClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401 && localStorage.getItem('fv_token')) {
      localStorage.removeItem('fv_token')
      localStorage.removeItem('fv_email')
      if (typeof window !== 'undefined' && window.location.pathname !== '/auth') {
        window.location.assign('/auth')
      }
    }
    return Promise.reject(error)
  },
)

export function extractApiError(e: unknown, fallback = 'Что-то пошло не так'): string {
  const err = e as AxiosError<{ message?: string; error?: string; details?: string[] }>
  const data = err?.response?.data
  if (data) {
    if (Array.isArray(data.details) && data.details.length > 0) return data.details.join('; ')
    if (typeof data.message === 'string' && data.message) return data.message
    if (typeof data.error === 'string' && data.error) return data.error
  }
  if (err?.message) return err.message
  return fallback
}
