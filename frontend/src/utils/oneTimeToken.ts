export function extractOneTimeToken(hash: string, legacyQueryToken: unknown): string {
  const normalizedHash = hash.startsWith('#') ? hash.slice(1) : hash
  const fragmentToken = new URLSearchParams(normalizedHash).get('token')?.trim()
  if (fragmentToken) return fragmentToken
  return typeof legacyQueryToken === 'string' ? legacyQueryToken.trim() : ''
}
