import { vi } from 'vitest'

function makeMemoryStorage(): Storage {
  const data = new Map<string, string>()
  return {
    get length() {
      return data.size
    },
    clear() {
      data.clear()
    },
    getItem(key: string) {
      return data.has(key) ? data.get(key)! : null
    },
    key(index: number) {
      return Array.from(data.keys())[index] ?? null
    },
    removeItem(key: string) {
      data.delete(key)
    },
    setItem(key: string, value: string) {
      data.set(key, String(value))
    },
  }
}

Object.defineProperty(globalThis, 'localStorage', {
  configurable: true,
  value: makeMemoryStorage(),
})
Object.defineProperty(globalThis, 'sessionStorage', {
  configurable: true,
  value: makeMemoryStorage(),
})

vi.mock('gsap', () => {
  const timeline = () => {
    const tl = {
      to: () => tl,
      set: () => tl,
      call: () => tl,
      eventCallback: () => tl,
      kill: () => tl,
    }
    return tl
  }
  const gsap = {
    timeline: (opts?: { onComplete?: () => void }) => {
      queueMicrotask(() => opts?.onComplete?.())
      return timeline()
    },
    to: () => timeline(),
    set: () => timeline(),
  }
  return { default: gsap, ...gsap }
})
