import { resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { feedbackApi } from '@/api/feedbackApi'
import { profileApi } from '@/api/profileApi'
import { extractApiError } from '@/api/httpClient'
import type { FeedbackInfo, Personalization, UpdatePersonalizationPayload } from '@/types'

export const useProfileStore = defineStore('profile', () => {
  const feedbacks = ref<FeedbackInfo[]>([])
  const personalization = ref<Personalization | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  resetOnAccountChange({ feedbacks, personalization, loading, error })

  async function loadFeedbacks() {
    try {
      feedbacks.value = await feedbackApi.getMy()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить историю откликов')
    }
  }

  async function loadPersonalization() {
    try {
      personalization.value = await profileApi.personalization()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить знакомство')
    }
  }

  async function savePersonalization(payload: UpdatePersonalizationPayload) {
    personalization.value = await profileApi.updatePersonalization(payload)
  }

  async function deleteMemoryRule(id: string) {
    await profileApi.deleteMemoryRule(id)
    if (personalization.value) {
      personalization.value = {
        ...personalization.value,
        memoryRules: personalization.value.memoryRules.filter((r) => r.id !== id),
      }
    }
  }

  async function clearMemory() {
    await profileApi.clearMemory()
    if (personalization.value) {
      personalization.value = { ...personalization.value, memoryRules: [] }
    }
  }

  async function loadAll() {
    loading.value = true
    error.value = null
    try {
      await Promise.all([loadFeedbacks(), loadPersonalization()])
    } finally {
      loading.value = false
    }
  }

  return {
    feedbacks,
    personalization,
    loading,
    error,
    loadFeedbacks,
    loadPersonalization,
    savePersonalization,
    deleteMemoryRule,
    clearMemory,
    loadAll,
  }
})
