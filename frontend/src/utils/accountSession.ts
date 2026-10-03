import { onScopeDispose, type Ref } from 'vue'

let version = 0
const resets = new Set<() => void>()

export function accountSessionVersion() {
  return version
}

// Account data must not survive logout or a change of credentials in the same SPA.
export function clearAccountSession() {
  version += 1
  for (const reset of resets) reset()
}

export function resetOnAccountChange(state: Record<string, Ref>, afterReset?: () => void) {
  const initial = JSON.stringify(Object.fromEntries(
    Object.entries(state).map(([key, value]) => [key, value.value]),
  ))
  const reset = () => {
    const defaults = JSON.parse(initial) as Record<string, unknown>
    for (const [key, value] of Object.entries(state)) value.value = defaults[key]
    afterReset?.()
  }
  resets.add(reset)
  onScopeDispose(() => resets.delete(reset))
}
