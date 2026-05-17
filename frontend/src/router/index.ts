import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { applyRouteSeo } from '@/seo/applySeo'

export const router = createRouter({
  history: createWebHistory(),
  scrollBehavior(to, _from, savedPosition) {
    if (savedPosition) return savedPosition
    if (to.hash) return { el: to.hash, behavior: 'smooth', top: 80 }
    return { top: 0 }
  },
  routes: [
    { path: '/', name: 'home', component: () => import('@/views/HomeView.vue') },
    { path: '/reading', name: 'reading', component: () => import('@/views/ReadingView.vue'), meta: { noindex: true } },
    { path: '/result', name: 'result', component: () => import('@/views/ResultView.vue'), meta: { noindex: true } },
    { path: '/history', name: 'history', component: () => import('@/views/HistoryView.vue'), meta: { requiresAuth: true, noindex: true } },
    { path: '/reading/:id', name: 'reading-detail', component: () => import('@/views/ReadingDetailView.vue'), meta: { requiresAuth: true, noindex: true } },
    { path: '/auth', name: 'auth', component: () => import('@/views/AuthView.vue'), meta: { noindex: true } },
    { path: '/verify-email', name: 'verify-email', component: () => import('@/views/VerifyEmailView.vue'), meta: { noindex: true } },
    { path: '/forgot-password', name: 'forgot-password', component: () => import('@/views/ForgotPasswordView.vue'), meta: { noindex: true } },
    { path: '/reset-password', name: 'reset-password', component: () => import('@/views/ResetPasswordView.vue'), meta: { noindex: true } },
    { path: '/glossary', name: 'glossary', component: () => import('@/views/GlossaryView.vue') },
    { path: '/glossary/:id', name: 'glossary-card', component: () => import('@/views/CardDetailView.vue'), meta: { noindex: true } },
    { path: '/tarot/cards/:slug', name: 'tarot-card-seo', component: () => import('@/views/TarotCardSeoView.vue') },
    { path: '/tarot/spreads/:slug', name: 'tarot-spread-seo', component: () => import('@/views/TarotSpreadSeoView.vue') },
    { path: '/tarot/decks/:slug', name: 'tarot-deck-seo', component: () => import('@/views/TarotDeckSeoView.vue') },
    { path: '/faq', name: 'faq', component: () => import('@/views/FaqView.vue') },
    { path: '/payment/success', name: 'payment-success', component: () => import('@/views/PaymentSuccessView.vue'), meta: { noindex: true } },
    {
      path: '/tarot-plus',
      name: 'tarot-plus',
      component: () => import('@/views/TarotPlusView.vue'),
      meta: { requiresAuth: true, noindex: true },
    },
    {
      path: '/tarot-plus/:id',
      name: 'tarot-plus-session',
      component: () => import('@/views/TarotPlusSessionView.vue'),
      meta: { requiresAuth: true, noindex: true },
    },
    { path: '/about', name: 'about', component: () => import('@/views/AboutView.vue') },
    { path: '/privacy', name: 'privacy', component: () => import('@/views/PrivacyView.vue') },
    { path: '/legal', name: 'legal', component: () => import('@/views/LegalView.vue') },
    { path: '/feedback/:token', name: 'feedback', component: () => import('@/views/FeedbackView.vue'), meta: { noindex: true } },
    { path: '/leaderboard', name: 'leaderboard', component: () => import('@/views/LeaderboardView.vue'), meta: { noindex: true } },
    { path: '/achievements', name: 'achievements', component: () => import('@/views/AchievementsView.vue'), meta: { requiresAuth: true, noindex: true } },
    { path: '/profile', name: 'profile', component: () => import('@/views/ProfileView.vue'), meta: { requiresAuth: true, noindex: true } },
    {
      path: '/admin',
      component: () => import('@/views/AdminView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true, noindex: true },
      children: [
        { path: '', name: 'admin', redirect: { name: 'admin-users' } },
        { path: 'feedbacks', name: 'admin-feedbacks', component: () => import('@/views/admin/AdminFeedbacksView.vue'), meta: { noindex: true } },
        { path: 'users', name: 'admin-users', component: () => import('@/views/admin/AdminUsersView.vue'), meta: { noindex: true } },
        { path: 'users/:id', name: 'admin-user-detail', component: () => import('@/views/admin/AdminUsersView.vue'), props: true, meta: { noindex: true } },
        { path: 'stats', name: 'admin-stats', component: () => import('@/views/admin/AdminStatsView.vue'), meta: { noindex: true } },
        { path: 'tarot-plus', name: 'admin-tarot-plus', component: () => import('@/views/admin/AdminTarotPlusView.vue'), meta: { noindex: true } },
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

router.afterEach((to) => {
  applyRouteSeo(to)
})
