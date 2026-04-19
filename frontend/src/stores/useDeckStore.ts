import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { DeckType } from '@/types'

const STORAGE_KEY = 'fv_deck'

function loadInitial(): DeckType {
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
  const current = ref<DeckType>(loadInitial())

  watch(current, (value) => {
    localStorage.setItem(STORAGE_KEY, String(value))
  })

  function select(value: DeckType) {
    current.value = value
  }

  return { current, select }
})
