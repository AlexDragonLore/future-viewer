import { onBeforeUnmount, onMounted, ref } from 'vue'

export function useMotionPreferences() {
  const prefersReducedMotion = ref(false)
  let media: MediaQueryList | null = null

  const update = () => {
    prefersReducedMotion.value = media?.matches ?? false
  }

  onMounted(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return
    media = window.matchMedia('(prefers-reduced-motion: reduce)')
    update()

    if (media.addEventListener) media.addEventListener('change', update)
    else media.addListener(update)
  })

  onBeforeUnmount(() => {
    if (!media) return
    if (media.removeEventListener) media.removeEventListener('change', update)
    else media.removeListener(update)
    media = null
  })

  return { prefersReducedMotion }
}
