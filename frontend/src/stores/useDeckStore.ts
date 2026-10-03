import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { DeckType } from '@/types'
import { useCookiePreferences } from '@/composables/useCookiePreferences'

const STORAGE_KEY = 'fv_deck'

function loadInitial(canUsePreferences: boolean): DeckType {
  if (!canUsePreferences) return DeckType.RWS
  const raw = localStorage.getItem(STORAGE_KEY)
  if (!raw) return DeckType.RWS
  const parsed = Number(raw)
  const allowed: DeckType[] = [
    DeckType.RWS,
    DeckType.Thoth,
    DeckType.Marseille,
    DeckType.ViscontiSforza,
    DeckType.ModernWitch,
  ]
  return allowed.includes(parsed as DeckType) ? (parsed as DeckType) : DeckType.RWS
}

export const useDeckStore = defineStore('deck', () => {
  const cookies = useCookiePreferences()
  cookies.loadCookiePreferences()
  const current = ref<DeckType>(loadInitial(cookies.isAllowed('preferences')))

  watch(
    [current, cookies.preferences],
    ([value]) => {
      if (cookies.isAllowed('preferences')) localStorage.setItem(STORAGE_KEY, String(value))
      else localStorage.removeItem(STORAGE_KEY)
    },
  )

  function select(value: DeckType) {
    current.value = value
  }

  return { current, select }
})
