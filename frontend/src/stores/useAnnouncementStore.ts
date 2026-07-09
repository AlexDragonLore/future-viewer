import { defineStore } from 'pinia'
import { ref } from 'vue'
import { announcementApi } from '@/api/announcementApi'
import { extractApiError } from '@/api/httpClient'
import type { AnnouncementInfo } from '@/types'

export const useAnnouncementStore = defineStore('announcements', () => {
  const unread = ref<AnnouncementInfo[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function load() {
    loading.value = true
    error.value = null
    try {
      unread.value = await announcementApi.unread()
    } catch (e) {
      error.value = extractApiError(e, 'Не удалось загрузить анонсы')
    } finally {
      loading.value = false
    }
  }

  async function markRead(id: string) {
    await announcementApi.markRead(id)
    unread.value = unread.value.filter((item) => item.id !== id)
  }

  function clear() {
    unread.value = []
    error.value = null
    loading.value = false
  }

  return {
    unread,
    loading,
    error,
    load,
    markRead,
    clear,
  }
})
