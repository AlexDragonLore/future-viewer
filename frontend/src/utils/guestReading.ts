const key = 'fv_guest_reading_v1'

export interface GuestContinuation {
  ticket: string
  expiresAt: string
}

// Only the server-encrypted ticket is persisted, never the question or interpretation.
// localStorage lets an email verification link opened in a new tab resume the reading.
export function saveGuestContinuation(value: GuestContinuation) {
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
