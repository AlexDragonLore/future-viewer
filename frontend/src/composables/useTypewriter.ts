import { ref, watch, type Ref } from 'vue'

export function useTypewriter(source: Ref<string | null | undefined>, speed = 28) {
  const output = ref('')
  let timer: number | null = null

  function stop() {
    if (timer !== null) {
      window.clearInterval(timer)
      timer = null
    }
  }

  watch(
    source,
    (val) => {
      stop()
      output.value = ''
      if (!val) return
      let i = 0
      timer = window.setInterval(() => {
        output.value += val.charAt(i)
        i++
        if (i >= val.length) stop()
      }, speed)
    },
    { immediate: true },
  )

  return { output, stop }
}
