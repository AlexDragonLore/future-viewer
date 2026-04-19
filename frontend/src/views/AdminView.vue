<script setup lang="ts">
import { useAuthStore } from '@/stores/useAuthStore'

const auth = useAuthStore()
</script>

<template>
  <main class="min-h-screen px-6 py-16 max-w-4xl mx-auto" data-testid="admin-view">
    <header class="mb-8 text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ АДМИН ✦</div>
      <h1 class="font-display text-4xl gold-text">Панель администратора</h1>
      <p class="text-mystic-muted mt-3">Привет, {{ auth.email }}. Здесь появятся инструменты управления.</p>
    </header>

    <section class="mystic-card p-6" data-testid="admin-placeholder">
      <p class="text-mystic-muted">
        В разработке. Скоро: пользователи, фидбеки, подписки, ачивки, Telegram, статистика.
      </p>
    </section>
  </main>
</template>
