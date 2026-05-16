export async function preloadImage(src: string): Promise<void> {
  if (typeof window === 'undefined') return

  await new Promise<void>((resolve) => {
    const img = new Image()
    img.decoding = 'async'
    img.onload = () => resolve()
    img.onerror = () => resolve()
    img.src = src

    if (img.complete) resolve()
  })

  const decoded = new Image()
  decoded.decoding = 'async'
  decoded.src = src
  if (decoded.decode) {
    await decoded.decode().catch(() => {})
  }
}

export async function preloadImages(srcs: Array<string | undefined | null>): Promise<void> {
  const unique = Array.from(new Set(srcs.filter((src): src is string => Boolean(src))))
  await Promise.all(unique.map((src) => preloadImage(src)))
}
