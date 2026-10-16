// Keep at most three pages of private results in memory; reuse them for 30 seconds.
// Callers clear this cache on identity, filters, refresh and mutations.
export function createAdminPageCache<T>() {
  const pages = new Map<string, { value: T; expiresAt: number }>()
  return {
    get(key: string): T | undefined {
      const item = pages.get(key)
      if (!item || item.expiresAt <= Date.now()) {
        pages.delete(key)
        return undefined
      }
      return item.value
    },
    set(key: string, value: T) {
      pages.delete(key)
      pages.set(key, { value, expiresAt: Date.now() + 30_000 })
      if (pages.size > 3) pages.delete(pages.keys().next().value!)
    },
    clear() { pages.clear() },
  }
}
