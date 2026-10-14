// Independent of the app bundle, including its failure and slow-network paths.
(() => {
  const splash = document.getElementById('startup-splash')
  const app = document.getElementById('app')
  const status = document.getElementById('startup-status')
  const retry = document.getElementById('startup-retry')
  const startedAt = performance.now()
  const motion = matchMedia('(prefers-reduced-motion: reduce)')
  let ready = false
  document.body.classList.add('startup-pending')
  app.setAttribute('inert', '')
  app.setAttribute('aria-hidden', 'true')

  function showRetry(failed) {
    if (ready) return
    if (failed) clearTimeout(watchdog)
    status.textContent = failed ? 'Не удалось открыть сайт' : 'Загрузка занимает чуть больше времени…'
    splash.classList.toggle('startup-failed', failed)
    retry.hidden = false
  }

  const watchdog = setTimeout(() => showRetry(false), 12000)
  const onError = () => showRetry(true)
  document.addEventListener('fv:app-error', onError)
  window.addEventListener('error', onError)
  // Resource errors do not bubble. A failed entry module also needs a retry.
  const onResourceError = (event) => {
    if (event.target instanceof HTMLScriptElement && event.target.type === 'module') showRetry(true)
  }
  window.addEventListener('error', onResourceError, true)

  document.addEventListener('fv:app-ready', () => {
    ready = true
    clearTimeout(watchdog)
    document.removeEventListener('fv:app-error', onError)
    window.removeEventListener('error', onError)
    window.removeEventListener('error', onResourceError, true)
    // A short first shuffle avoids a flash on cached visits; slow loads add no extra wait.
    const remaining = motion.matches ? 0 : Math.max(0, 1100 - (performance.now() - startedAt))
    setTimeout(() => {
      app.removeAttribute('inert')
      app.removeAttribute('aria-hidden')
      document.body.classList.remove('startup-pending')
      splash.classList.add('startup-leaving')
      setTimeout(() => splash.remove(), motion.matches ? 0 : 460)
    }, remaining)
  }, { once: true })
})()
