import { createApp, nextTick } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { startAnalytics } from './analytics/metrika'
import './assets/styles/main.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)
const stopAnalytics = startAnalytics(router)
if (import.meta.hot) import.meta.hot.dispose(stopAnalytics)

async function mountApp() {
  try {
    // The initial route is lazy: reveal the site only after its view is mounted.
    await router.isReady()
    app.mount('#app')
    await nextTick()
    requestAnimationFrame(() => {
      document.dispatchEvent(new Event('fv:app-ready'))
      // A failed download of the small startup controller must not hide a ready app.
      if (!document.body.classList.contains('startup-pending')) {
        document.getElementById('startup-splash')?.remove()
      }
    })
  } catch (error) {
    document.dispatchEvent(new Event('fv:app-error'))
    console.error('Unable to start the application', error)
  }
}

void mountApp()
