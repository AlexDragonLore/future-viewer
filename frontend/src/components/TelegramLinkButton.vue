<script setup lang="ts">
import { computed, ref } from 'vue'
import { telegramApi } from '@/api/telegramApi'
import { extractApiError } from '@/api/httpClient'

const props = defineProps<{
  isLinked: boolean
}>()

const emit = defineEmits<{
  (e: 'update'): void
}>()

const busy = ref(false)
const error = ref<string | null>(null)
const deepLink = ref<string | null>(null)

const label = computed(() => {
  if (busy.value) return '...'
  return props.isLinked ? 'Отвязать Telegram' : 'Привязать Telegram'
})

async function onClick() {
  busy.value = true
  error.value = null
  try {
    if (props.isLinked) {
      await telegramApi.unlink()
      deepLink.value = null
      emit('update')
    } else {
      const response = await telegramApi.link()
      deepLink.value = response.deepLinkUrl
    }
  } catch (e) {
    error.value = extractApiError(e, 'Не удалось выполнить действие')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="tg-link" data-testid="telegram-link-block">
    <button class="glow-button" :disabled="busy" @click="onClick" data-testid="telegram-link-button">
      {{ label }}
    </button>

    <div v-if="error" class="tg-error" data-testid="telegram-link-error">{{ error }}</div>

    <div v-if="deepLink" class="tg-hint" data-testid="telegram-deeplink">
      <p class="tg-hint-text">Открой ссылку ниже и нажми «Start», чтобы завершить привязку.</p>
      <a :href="deepLink" target="_blank" rel="noopener noreferrer" class="tg-link-anchor">
        {{ deepLink }}
      </a>
    </div>
  </div>
</template>

<style scoped>
.tg-link {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.75rem;
}
.tg-error {
  color: #fca5a5;
  font-size: 0.8rem;
}
.tg-hint {
  padding: 0.75rem 1rem;
  border-radius: 10px;
  border: 1px solid rgba(245, 194, 107, 0.3);
  background: rgba(0, 0, 0, 0.25);
  font-size: 0.8rem;
}
.tg-hint-text {
  margin: 0 0 0.5rem;
  color: rgba(224, 212, 186, 0.85);
}
.tg-link-anchor {
  color: #f5c26b;
  word-break: break-all;
  text-decoration: underline;
}
</style>
