const CARD_IMAGE_VERSION = 'cards-20260510'

export function versionedCardImage(path?: string | null): string | undefined {
  if (!path) return undefined
  if (!path.startsWith('/cards/')) return path

  const separator = path.includes('?') ? '&' : '?'
  return `${path}${separator}v=${CARD_IMAGE_VERSION}`
}
