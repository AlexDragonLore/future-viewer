<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/useAuthStore'
import { extractApiError } from '@/api/httpClient'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const status = ref<'pending' | 'ok' | 'error'>('pending')
const message = ref<string | null>(null)

onMounted(async () => {
  const token = route.query.token as string | undefined
  if (!token) {
    status.value = 'error'
    message.value = 'Ссылка недействительна.'
    return
  }
  try {
    await auth.verifyEmail(token)
    status.value = 'ok'
    message.value = 'Email подтверждён. Перенаправляем вас…'
    setTimeout(() => router.replace('/'), 1200)
  } catch (e) {
    status.value = 'error'
    message.value = extractApiError(e)
  }
})
</script>

<template>
  <main class="min-h-screen flex items-center justify-center px-6">
    <section class="mystic-card p-8 w-full max-w-md text-center">
      <div class="text-mystic-accent text-xs tracking-[0.4em] mb-2">✦ ВРАТА ✦</div>
      <h1 class="font-display text-3xl gold-text mb-6">Подтверждение email</h1>
      <p v-if="status === 'pending'" class="text-sm text-mystic-silver/70">Проверяем ссылку…</p>
      <p v-else-if="status === 'ok'" class="text-sm text-mystic-accent">{{ message }}</p>
      <div v-else class="space-y-4">
        <p class="text-sm text-red-300">{{ message }}</p>
        <router-link to="/auth" class="underline text-mystic-accent text-sm">
          Вернуться ко входу
        </router-link>
      </div>
    </section>
  </main>
</template>
