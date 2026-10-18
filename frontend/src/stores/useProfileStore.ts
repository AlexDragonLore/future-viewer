import { resetOnAccountChange } from '@/utils/accountSession'
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { profileApi } from '@/api/profileApi'
import { extractApiError } from '@/api/httpClient'
import type { Personalization, UpdatePersonalizationPayload } from '@/types'

export const useProfileStore = defineStore('profile', () => {
  const personalization = ref<Personalization | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  resetOnAccountChange({ personalization, loading, error })

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
      await loadPersonalization()
    } finally {
      loading.value = false
    }
  }

  return {
    personalization,
    loading,
    error,
    loadPersonalization,
    savePersonalization,
    deleteMemoryRule,
    clearMemory,
    loadAll,
  }
})
