import { defineStore } from 'pinia'
import { ref } from 'vue'
import { publicApi } from '@/api/publicApi'
import { isLocalStaticPreview } from '@/utils/runtime'

export const usePublicConfigStore = defineStore('publicConfig', () => {
  const supportEmail = ref<string>('')
  const loaded = ref(false)

  async function load() {
    if (loaded.value) return
    if (isLocalStaticPreview()) {
      loaded.value = true
      return
    }
    try {
      const config = await publicApi.getConfig()
      supportEmail.value = config.supportEmail ?? ''
    } catch {
      supportEmail.value = ''
    } finally {
      loaded.value = true
    }
  }

  return { supportEmail, loaded, load }
})
