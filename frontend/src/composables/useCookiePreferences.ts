import { computed, ref } from 'vue'

const configuredPolicyVersion = import.meta.env.VITE_LEGAL_COOKIES_VERSION
  || import.meta.env.VITE_LEGAL_DOCUMENT_VERSION
export const COOKIE_POLICY_VERSION = configuredPolicyVersion || 'browser-storage-2026-10-03'
const STORAGE_KEY = 'fv_cookie_preferences_v1'
const MAX_AGE_MS = 365 * 24 * 60 * 60 * 1000

export type OptionalStorageCategory = 'preferences' | 'analytics' | 'marketing'

export interface CookiePreferences {
  version: string
  necessary: true
  preferences: boolean
  analytics: boolean
  marketing: boolean
  decidedAt: string
  expiresAt: string
}

const preferences = ref<CookiePreferences | null>(null)
const settingsOpen = ref(false)
const initialized = ref(false)
let storageListenerAttached = false

function synchronizeChoice(event: StorageEvent) {
  if (event.key !== STORAGE_KEY && event.key !== null) return
  if (event.storageArea && event.storageArea !== window.localStorage) return
  let value: unknown = null
  try { value = event.newValue ? JSON.parse(event.newValue) : null } catch { /* Treat invalid data as no choice. */ }
  preferences.value = isStoredChoice(value) ? value : null
  initialized.value = true
  settingsOpen.value = preferences.value === null
}

function isStoredChoice(value: unknown): value is CookiePreferences {
  if (!value || typeof value !== 'object') return false
  const item = value as Partial<CookiePreferences>
  return item.version === COOKIE_POLICY_VERSION
    && item.necessary === true
    && typeof item.preferences === 'boolean'
    && typeof item.analytics === 'boolean'
    && typeof item.marketing === 'boolean'
    && typeof item.decidedAt === 'string'
    && typeof item.expiresAt === 'string'
    && Number.isFinite(Date.parse(item.decidedAt))
    && Date.parse(item.decidedAt) <= Date.now()
    && Date.parse(item.expiresAt) - Date.parse(item.decidedAt) <= MAX_AGE_MS
    && Date.parse(item.expiresAt) > Date.now()
}

function loadCookiePreferences() {
  if (initialized.value) return
  initialized.value = true

  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    const parsed: unknown = raw ? JSON.parse(raw) : null
    if (isStoredChoice(parsed)) preferences.value = parsed
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    localStorage.removeItem(STORAGE_KEY)
  }

  if (!preferences.value) settingsOpen.value = true
  if (!preferences.value?.preferences) localStorage.removeItem('fv_deck')
}

function persistChoice(choice: Pick<CookiePreferences, OptionalStorageCategory>) {
  const decidedAt = new Date()
  const value: CookiePreferences = {
    version: COOKIE_POLICY_VERSION,
    necessary: true,
    ...choice,
    decidedAt: decidedAt.toISOString(),
    expiresAt: new Date(decidedAt.getTime() + MAX_AGE_MS).toISOString(),
  }
  localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
  if (!value.preferences) localStorage.removeItem('fv_deck')
  preferences.value = value
  settingsOpen.value = false
}

function acceptNecessary() {
  persistChoice({ preferences: false, analytics: false, marketing: false })
}

function acceptAll() {
  persistChoice({ preferences: true, analytics: true, marketing: true })
}

function saveDetailed(choice: Pick<CookiePreferences, OptionalStorageCategory>) {
  persistChoice(choice)
}

function openCookieSettings() {
  loadCookiePreferences()
  settingsOpen.value = true
}

function closeCookieSettings() {
  if (preferences.value) settingsOpen.value = false
}

function isAllowed(category: 'necessary' | OptionalStorageCategory) {
  if (category === 'necessary') return true
  if (preferences.value && !isStoredChoice(preferences.value)) {
    resetCookiePreferences()
  }
  return preferences.value?.[category] === true
}

function resetCookiePreferences() {
  localStorage.removeItem(STORAGE_KEY)
  localStorage.removeItem('fv_deck')
  preferences.value = null
  initialized.value = false
  settingsOpen.value = true
}

export function useCookiePreferences() {
  if (!storageListenerAttached && typeof window !== 'undefined') {
    window.addEventListener('storage', synchronizeChoice)
    storageListenerAttached = true
  }
  return {
    preferences,
    settingsOpen,
    hasChoice: computed(() => preferences.value !== null),
    loadCookiePreferences,
    acceptNecessary,
    acceptAll,
    saveDetailed,
    openCookieSettings,
    closeCookieSettings,
    isAllowed,
    resetCookiePreferences,
  }
}
