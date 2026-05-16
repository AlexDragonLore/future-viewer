const localHosts = new Set(['localhost', '127.0.0.1', '0.0.0.0'])

export function isLocalStaticPreview(): boolean {
  if (import.meta.env.VITE_API_URL) return false
  if (!import.meta.env.PROD || typeof window === 'undefined') return false
  return localHosts.has(window.location.hostname)
}
