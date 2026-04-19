<script setup lang="ts">
import { computed, ref } from 'vue'

const props = defineProps<{
  busy: boolean
  error?: string | null
  minLength?: number
}>()

const emit = defineEmits<{
  (e: 'submit', selfReport: string): void
}>()

const text = ref('')

const min = computed(() => props.minLength ?? 10)

const charsLeft = computed(() => {
  const diff = min.value - text.value.trim().length
  return diff > 0 ? diff : 0
})

const canSubmit = computed(() => text.value.trim().length >= min.value && !props.busy)

function onSubmit() {
  if (!canSubmit.value) return
  emit('submit', text.value.trim())
}
</script>

<template>
  <form class="feedback-form" @submit.prevent="onSubmit" data-testid="feedback-form">
    <label class="feedback-label" for="feedback-self-report">Как ты следовал рекомендациям?</label>
    <textarea
      id="feedback-self-report"
      v-model="text"
      rows="6"
      :disabled="busy"
      placeholder="Расскажи подробно, что получилось, а что нет. Искренность важнее краткости."
      class="feedback-textarea"
      data-testid="feedback-textarea"
    />
    <div class="feedback-foot">
      <span class="feedback-hint" data-testid="feedback-hint">
        <template v-if="charsLeft > 0">Ещё {{ charsLeft }} символ(ов) до отправки</template>
        <template v-else>Можно отправлять</template>
      </span>
      <button
        type="submit"
        class="glow-button"
        :disabled="!canSubmit"
        data-testid="feedback-submit"
      >
        {{ busy ? '...' : 'Отправить' }}
      </button>
    </div>
    <div v-if="error" class="feedback-error" data-testid="feedback-error">{{ error }}</div>
  </form>
</template>

<style scoped>
.feedback-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
.feedback-label {
  font-family: 'Cinzel', serif;
  font-size: 0.8rem;
  letter-spacing: 0.1em;
  color: rgba(245, 194, 107, 0.85);
  text-transform: uppercase;
}
.feedback-textarea {
  width: 100%;
  padding: 0.85rem 1rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 10px;
  color: #e8d5f2;
  font-family: 'Inter', system-ui, sans-serif;
  font-size: 0.95rem;
  line-height: 1.5;
  resize: vertical;
  transition: border-color 0.2s ease;
}
.feedback-textarea:focus {
  outline: none;
  border-color: #f5c26b;
}
.feedback-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}
.feedback-hint {
  font-size: 0.75rem;
  color: rgba(224, 212, 186, 0.7);
}
.feedback-error {
  color: #fca5a5;
  font-size: 0.85rem;
}
</style>
