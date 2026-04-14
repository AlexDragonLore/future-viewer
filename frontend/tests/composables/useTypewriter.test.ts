import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { ref } from 'vue'
import { useTypewriter } from '@/composables/useTypewriter'

describe('useTypewriter', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('starts empty and reveals characters over time', async () => {
    const source = ref<string>('hi')
    const { output } = useTypewriter(source, 10)
    expect(output.value).toBe('')

    await vi.advanceTimersByTimeAsync(10)
    expect(output.value).toBe('h')

    await vi.advanceTimersByTimeAsync(10)
    expect(output.value).toBe('hi')
  })

  it('resets and retypes when source changes', async () => {
    const source = ref<string>('one')
    const { output } = useTypewriter(source, 5)

    await vi.advanceTimersByTimeAsync(30)
    expect(output.value).toBe('one')

    source.value = 'two'
    await vi.advanceTimersByTimeAsync(0)
    expect(output.value).toBe('')
    await vi.advanceTimersByTimeAsync(30)
    expect(output.value).toBe('two')
  })

  it('treats null source as empty output', () => {
    const source = ref<string | null>(null)
    const { output } = useTypewriter(source, 10)
    expect(output.value).toBe('')
  })

  it('stop halts further character reveals', async () => {
    const source = ref<string>('abcdef')
    const { output, stop } = useTypewriter(source, 10)

    await vi.advanceTimersByTimeAsync(20)
    const snapshot = output.value
    stop()
    await vi.advanceTimersByTimeAsync(100)
    expect(output.value).toBe(snapshot)
  })
})
