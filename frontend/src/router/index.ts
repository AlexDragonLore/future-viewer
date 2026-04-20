import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'

export const router = createRouter({
  history: createWebHistory(),
  scrollBehavior(to, _from, savedPosition) {
    if (savedPosition) return savedPosition
    if (to.hash) return { el: to.hash, behavior: 'smooth', top: 80 }
    return { top: 0 }
  },
  routes: [
    { path: '/', name: 'home', component: () => import('@/views/HomeView.vue') },
    { path: '/reading', name: 'reading', component: () => import('@/views/ReadingView.vue') },
    { path: '/result', name: 'result', component: () => import('@/views/ResultView.vue') },
    { path: '/history', name: 'history', component: () => import('@/views/HistoryView.vue'), meta: { requiresAuth: true } },
    { path: '/reading/:id', name: 'reading-detail', component: () => import('@/views/ReadingDetailView.vue'), meta: { requiresAuth: true } },
    { path: '/auth', name: 'auth', component: () => import('@/views/AuthView.vue') },
    { path: '/verify-email', name: 'verify-email', component: () => import('@/views/VerifyEmailView.vue') },
    { path: '/glossary', name: 'glossary', component: () => import('@/views/GlossaryView.vue') },
    { path: '/glossary/:id', name: 'glossary-card', component: () => import('@/views/CardDetailView.vue') },
    { path: '/payment/success', name: 'payment-success', component: () => import('@/views/PaymentSuccessView.vue') },
    { path: '/feedback/:token', name: 'feedback', component: () => import('@/views/FeedbackView.vue') },
    { path: '/leaderboard', name: 'leaderboard', component: () => import('@/views/LeaderboardView.vue') },
    { path: '/achievements', name: 'achievements', component: () => import('@/views/AchievementsView.vue'), meta: { requiresAuth: true } },
    { path: '/profile', name: 'profile', component: () => import('@/views/ProfileView.vue'), meta: { requiresAuth: true } },
    {
      path: '/admin',
      component: () => import('@/views/AdminView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true },
      children: [
        { path: '', name: 'admin', redirect: { name: 'admin-users' } },
        { path: 'feedbacks', name: 'admin-feedbacks', component: () => import('@/views/admin/AdminFeedbacksView.vue') },
        { path: 'users', name: 'admin-users', component: () => import('@/views/admin/AdminUsersView.vue') },
        { path: 'users/:id', name: 'admin-user-detail', component: () => import('@/views/admin/AdminUsersView.vue'), props: true },
        { path: 'stats', name: 'admin-stats', component: () => import('@/views/admin/AdminStatsView.vue') },
      ],
    },
  ],
})

router.beforeEach((to) => {
  if (to.meta.requiresAuth) {
    const auth = useAuthStore()
    if (!auth.isAuthenticated) return { name: 'auth', query: { redirect: to.fullPath } }
    if (to.meta.requiresAdmin && !auth.isAdmin) return { name: 'home' }
  }
})
