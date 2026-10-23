import { defineStore } from 'pinia'
import { onScopeDispose, ref } from 'vue'
import { adminApi } from '@/api/adminApi'
import { extractApiError } from '@/api/httpClient'
import { resetOnAccountChange } from '@/utils/accountSession'
import type { AdminPurchase, AdminPurchaseFilter } from '@/types/admin'

export const useAdminPurchaseStore = defineStore('admin-purchases', () => {
  const items = ref<AdminPurchase[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = 20
  const search = ref('')
  const status = ref<AdminPurchaseFilter>('paid')
  const userId = ref<string | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let request = 0
  let controller: AbortController | undefined

  function cancel() {
    request++
    controller?.abort()
  }
  resetOnAccountChange({ items, total, page, search, status, userId, loading, error }, cancel)
  onScopeDispose(cancel)

  function setFilters(next: { search: string; status: AdminPurchaseFilter; userId: string | null }) {
    const trimmed = next.search.trim()
    if (trimmed !== search.value || next.status !== status.value || next.userId !== userId.value) {
      page.value = 1
    }
    search.value = trimmed
    status.value = next.status
    userId.value = next.userId
  }

  async function load() {
    cancel()
    const current = request
    controller = new AbortController()
    loading.value = true
    error.value = null
    items.value = []
    total.value = 0
    try {
      const result = await adminApi.listPurchases({
        search: search.value, status: status.value, userId: userId.value,
        page: page.value, pageSize,
      }, controller.signal)
      if (current !== request) return
      items.value = result.items
      total.value = result.total
    } catch (e) {
      if (current !== request) return
      error.value = extractApiError(e, 'Не удалось загрузить историю покупок')
    } finally {
      if (current === request) loading.value = false
    }
  }

  return { items, total, page, pageSize, search, status, userId, loading, error, setFilters, load }
})
