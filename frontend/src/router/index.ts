import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: () => import('@/views/HomeView.vue') },
    { path: '/reading', name: 'reading', component: () => import('@/views/ReadingView.vue') },
    { path: '/result', name: 'result', component: () => import('@/views/ResultView.vue') },
    { path: '/history', name: 'history', component: () => import('@/views/HistoryView.vue'), meta: { requiresAuth: true } },
    { path: '/reading/:id', name: 'reading-detail', component: () => import('@/views/ReadingDetailView.vue'), meta: { requiresAuth: true } },
    { path: '/auth', name: 'auth', component: () => import('@/views/AuthView.vue') },
    { path: '/glossary', name: 'glossary', component: () => import('@/views/GlossaryView.vue') },
    { path: '/glossary/:id', name: 'glossary-card', component: () => import('@/views/CardDetailView.vue') },
    { path: '/payment/success', name: 'payment-success', component: () => import('@/views/PaymentSuccessView.vue') },
  ],
})

router.beforeEach((to) => {
  if (to.meta.requiresAuth) {
    const auth = useAuthStore()
    if (!auth.isAuthenticated) return { name: 'auth', query: { redirect: to.fullPath } }
  }
})
