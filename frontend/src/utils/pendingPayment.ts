const KEY = 'fv_pending_payment_v1'
const TTL = 24 * 60 * 60 * 1000

export function savePendingPayment(paymentId: string, userId: string) {
  try {
    if (!/^[a-f0-9]{32}$/i.test(paymentId) || !userId) return
    localStorage.setItem(KEY, JSON.stringify({ paymentId, userId, expires: Date.now() + TTL }))
  } catch { /* Checkout works even when storage is unavailable. */ }
}

export function getPendingPayment(userId: string | null): string | null {
  try {
    const value = JSON.parse(localStorage.getItem(KEY) ?? 'null')
    if (value?.userId === userId && typeof value.paymentId === 'string'
      && /^[a-f0-9]{32}$/i.test(value.paymentId) && value.expires > Date.now()) return value.paymentId
    if (!value || value.expires <= Date.now()) clearPendingPayment()
  } catch { /* Treat malformed or unavailable storage as an unknown payment. */ }
  return null
}

export function clearPendingPayment() {
  try { localStorage.removeItem(KEY) } catch { /* Storage may be restricted. */ }
}
