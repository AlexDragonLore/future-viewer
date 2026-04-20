<script setup lang="ts">
import { ref } from 'vue'
import { useAdminStore } from '@/stores/useAdminStore'

const emit = defineEmits<{ done: [] }>()
const store = useAdminStore()

type Mode = 'scheduled' | 'immediate' | 'synthetic'

const mode = ref<Mode>('scheduled')
const readingId = ref('')
const aiScore = ref<number>(8)
const aiScoreReason = ref('')
const isSincere = ref(true)
const selfReport = ref('')
const replace = ref(false)
const submitting = ref(false)

async function submit(): Promise<void> {
  if (!readingId.value.trim()) return
  submitting.value = true
  try {
    if (mode.value === 'synthetic') {
      const created = await store.createSyntheticFeedback({
        readingId: readingId.value.trim(),
        aiScore: aiScore.value,
        aiScoreReason: aiScoreReason.value || null,
        isSincere: isSincere.value,
        selfReport: selfReport.value || null,
      })
      if (created) emit('done')
    } else {
      const created = await store.createFeedback({
        readingId: readingId.value.trim(),
        bypassDelay: mode.value === 'immediate',
        replace: replace.value,
      })
      if (created) emit('done')
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <form class="mystic-card p-4 space-y-3" data-testid="admin-create-feedback-form" @submit.prevent="submit">
    <div class="flex gap-3 flex-wrap text-xs uppercase tracking-widest text-mystic-muted">
      <label class="cursor-pointer">
        <input v-model="mode" type="radio" value="scheduled" /> Через 24ч
      </label>
      <label class="cursor-pointer">
        <input v-model="mode" type="radio" value="immediate" data-testid="admin-create-mode-immediate" />
        Сразу (bypassDelay)
      </label>
      <label class="cursor-pointer">
        <input v-model="mode" type="radio" value="synthetic" data-testid="admin-create-mode-synthetic" />
        Синтетический Scored
      </label>
    </div>

    <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
      <span>Reading ID</span>
      <input
        v-model="readingId"
        type="text"
        placeholder="UUID расклада"
        class="admin-input"
        data-testid="admin-create-reading-id"
        required
      />
    </label>

    <div v-if="mode !== 'synthetic'" class="flex items-center gap-2 text-sm text-mystic-muted">
      <input id="admin-replace" v-model="replace" type="checkbox" data-testid="admin-create-replace" />
      <label for="admin-replace">Заменить существующий</label>
    </div>

    <template v-if="mode === 'synthetic'">
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>AI score (1–10)</span>
        <input
          v-model.number="aiScore"
          type="number"
          min="1"
          max="10"
          class="admin-input"
          data-testid="admin-create-score"
        />
      </label>
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>Причина оценки</span>
        <input
          v-model="aiScoreReason"
          type="text"
          class="admin-input"
        />
      </label>
      <label class="flex flex-col text-xs uppercase tracking-widest text-mystic-muted gap-1">
        <span>Self report</span>
        <textarea v-model="selfReport" class="admin-input" rows="2" />
      </label>
      <label class="flex items-center gap-2 text-sm text-mystic-muted">
        <input v-model="isSincere" type="checkbox" /> Искренний ответ
      </label>
    </template>

    <div class="flex justify-end gap-2 pt-2">
      <button type="button" class="admin-btn" @click="emit('done')">Отмена</button>
      <button
        type="submit"
        class="admin-btn primary"
        :disabled="submitting || !readingId.trim()"
        data-testid="admin-create-submit"
      >
        {{ submitting ? '…' : 'Создать' }}
      </button>
    </div>
  </form>
</template>

<style scoped>
.admin-input {
  background: rgba(20, 16, 32, 0.6);
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 0.4rem;
  padding: 0.4rem 0.75rem;
  color: #f8f4eb;
}
.admin-btn {
  padding: 0.45rem 0.9rem;
  border: 1px solid rgba(245, 194, 107, 0.4);
  border-radius: 0.4rem;
  color: rgba(224, 212, 186, 0.9);
  font-size: 0.85rem;
}
.admin-btn:hover:not(:disabled) {
  background: rgba(245, 194, 107, 0.1);
  color: #f5c26b;
}
.admin-btn:disabled {
  opacity: 0.4;
}
.admin-btn.primary {
  background: rgba(245, 194, 107, 0.2);
  color: #f5c26b;
}
</style>
