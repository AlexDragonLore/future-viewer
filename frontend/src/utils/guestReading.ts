const key = 'fv_guest_reading_v1'
const unlockedKey = 'fv_guest_reading_unlocked_v1'

export interface GuestContinuation {
  ticket: string
  expiresAt: string
}

export interface UnlockedGuestReading {
  readingId: string
  ownerUserId: string
  expiresAt: string
}

// Only the server-encrypted ticket is persisted, never the question or interpretation.
// localStorage lets an email verification link opened in a new tab resume the reading.
export function saveGuestContinuation(value: GuestContinuation) {
  clearUnlockedGuestReading()
  inMemory = value
  try {
    localStorage.setItem(key, JSON.stringify(value))
    inMemory = null
  } catch { /* Keep an in-memory fallback if browser storage is unavailable. */ }
}

let inMemory: GuestContinuation | null = null

export function getGuestContinuation(): GuestContinuation | null {
  try {
    const stored = localStorage.getItem(key)
    const value = stored ? JSON.parse(stored) as GuestContinuation : inMemory
    if (value && typeof value.ticket === 'string' && Date.parse(value.expiresAt) > Date.now()) return value
  } catch {
    if (inMemory && Date.parse(inMemory.expiresAt) > Date.now()) return inMemory
  }
  clearGuestContinuation()
  return null
}

export function clearGuestContinuation() {
  inMemory = null
  try { localStorage.removeItem(key) } catch { /* Storage may be unavailable. */ }
}

let unlockedInMemory: UnlockedGuestReading | null = null

// Keep only an owner-scoped reference so both verification tabs can reopen the
// authenticated result after the guest ticket is removed.
export function saveUnlockedGuestReading(value: UnlockedGuestReading) {
  unlockedInMemory = value
  try {
    localStorage.setItem(unlockedKey, JSON.stringify(value))
    unlockedInMemory = null
  } catch { /* Keep an in-memory fallback if browser storage is unavailable. */ }
}

export function getUnlockedGuestReading(ownerUserId: string | null): UnlockedGuestReading | null {
  let value: UnlockedGuestReading | null = unlockedInMemory
  try {
    const stored = localStorage.getItem(unlockedKey)
    if (stored) value = JSON.parse(stored) as UnlockedGuestReading
  } catch { /* Use the in-memory fallback if browser storage is unavailable. */ }
  if (!value || typeof value.readingId !== 'string' || !value.readingId
    || typeof value.ownerUserId !== 'string' || !value.ownerUserId
    || Date.parse(value.expiresAt) <= Date.now() || !Number.isFinite(Date.parse(value.expiresAt))) {
    clearUnlockedGuestReading()
    return null
  }
  return ownerUserId && value.ownerUserId === ownerUserId ? value : null
}

export function clearUnlockedGuestReading() {
  unlockedInMemory = null
  try { localStorage.removeItem(unlockedKey) } catch { /* Storage may be unavailable. */ }
}
