<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { privacyApi, type OptionalConsentType } from '@/api/privacyApi'
import { usePrivacyStore } from '@/stores/usePrivacyStore'
import { useProfileStore } from '@/stores/useProfileStore'
import { extractApiError } from '@/api/httpClient'
import { useAuthStore } from '@/stores/useAuthStore'
import { useRouter } from 'vue-router'
import { useReadingStore } from '@/stores/useReadingStore'

const privacy = usePrivacyStore()
const profile = useProfileStore()
const auth = useAuthStore()
const router = useRouter()

const password = ref('')
const busyAction = ref<string | null>(null)
const message = ref<string | null>(null)
const actionError = ref<string | null>(null)

const optionalTypes: OptionalConsentType[] = ['personalization', 'marketing', 'analytics']
const consentLabels: Record<OptionalConsentType, string> = {
  personalization: 'Персонализация',
  marketing: 'Рекламные сообщения',
  analytics: 'Аналитика',
}

const activeConsentByType = computed(() => Object.fromEntries(optionalTypes.map((type) => {
  const record = privacy.consents.find((item) => item.consentType.toLowerCase() === type && !item.revokedAt)
  return [type, record ?? null]
})))

onMounted(() => privacy.loadAll())

function requirePassword() {
  actionError.value = null
  message.value = null
  if (password.value.length < 8) {
    actionError.value = 'Введите текущий пароль для повторной аутентификации.'
    return false
  }
  return true
}

async function run(action: string, callback: () => Promise<void>) {
  if (busyAction.value) return
  busyAction.value = action
  actionError.value = null
  message.value = null
  try {
    await callback()
  } catch (error) {
    actionError.value = extractApiError(error, 'Операцию не удалось выполнить')
  } finally {
    busyAction.value = null
  }
}

async function toggleHistory(event: Event) {
  const enabled = (event.target as HTMLInputElement).checked
  await run('history', async () => {
    await privacy.updateHistory(enabled)
    message.value = enabled ? 'Сохранение новых раскладов включено.' : 'Сохранение новых раскладов отключено.'
  })
}

async function revoke(type: OptionalConsentType) {
  await run(`revoke:${type}`, async () => {
    await privacy.revoke(type)
    if (type === 'personalization') await profile.loadPersonalization()
    message.value = `Согласие «${consentLabels[type]}» отозвано.`
  })
}

async function exportData() {
  if (!requirePassword()) return
  await run('export', async () => {
    const blob = await privacyApi.exportData(password.value)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `future-viewer-data-${new Date().toISOString().slice(0, 10)}.json`
    link.click()
    URL.revokeObjectURL(url)
    password.value = ''
    message.value = 'Экспорт сформирован и передан браузеру для скачивания.'
  })
}

async function deleteAllReadings() {
  if (!requirePassword()) return
  if (!confirm('Удалить всю историю раскладов? Действие нельзя отменить.')) return
  await run('delete-readings', async () => {
    await privacyApi.deleteAllReadings(password.value)
    useReadingStore().reset()
    profile.feedbacks = []
    password.value = ''
    message.value = 'История удалена.'
  })
}

async function requestAccountDeletion() {
  if (!requirePassword()) return
  if (!confirm('Запросить удаление аккаунта и связанных рабочих данных? Аккаунт будет заблокирован.')) return
  await run('delete-account', async () => {
    await privacyApi.requestAccountDeletion(password.value)
    password.value = ''
    auth.logout()
    await router.replace({ path: '/auth', query: { accountDeletion: 'requested' } })
  })
}
</script>

<template>
  <section id="privacy-center" class="mystic-card privacy-center" data-testid="privacy-center">
    <div class="privacy-heading">
      <div>
        <div class="privacy-kicker">ДАННЫЕ И КОНФИДЕНЦИАЛЬНОСТЬ</div>
        <h2>Управление данными</h2>
      </div>
      <RouterLink to="/legal/data-request">Порядок обращений</RouterLink>
    </div>

    <p v-if="privacy.loading" class="privacy-muted">Загружаю настройки…</p>
    <p v-if="privacy.error" class="privacy-error">{{ privacy.error }}</p>
    <p v-if="actionError" class="privacy-error" data-testid="privacy-action-error">{{ actionError }}</p>
    <p v-if="message" class="privacy-success" data-testid="privacy-action-success">{{ message }}</p>

    <div class="privacy-row">
      <div>
        <strong>Сохранение истории</strong>
        <p>Влияет только на новые расклады; для каждого расклада выбор всё равно показывается перед отправкой.</p>
      </div>
      <input
        :checked="privacy.settings.historyEnabled"
        :disabled="Boolean(busyAction) || privacy.loading"
        type="checkbox"
        aria-label="Сохранять историю новых раскладов"
        data-testid="history-setting"
        @change="toggleHistory"
      />
    </div>

    <div class="consent-list">
      <h3>Необязательные согласия</h3>
      <div v-for="type in optionalTypes" :key="type" class="privacy-row">
        <div>
          <strong>{{ consentLabels[type] }}</strong>
          <p v-if="activeConsentByType[type]">
            Активно · версия {{ activeConsentByType[type]?.documentVersion }} ·
            {{ new Date(activeConsentByType[type]!.acceptedAt).toLocaleDateString() }}
          </p>
          <p v-else>Не предоставлено или отозвано.</p>
        </div>
        <button
          v-if="activeConsentByType[type]"
          type="button"
          class="privacy-button"
          :disabled="Boolean(busyAction)"
          :data-testid="`revoke-${type}`"
          @click="revoke(type)"
        >
          Отозвать
        </button>
      </div>
    </div>

    <div class="reauth-block">
      <label for="privacy-password">Текущий пароль для экспорта и удаления</label>
      <input
        id="privacy-password"
        v-model="password"
        type="password"
        minlength="8"
        autocomplete="current-password"
        data-testid="privacy-password"
      />
      <p>Для защиты данных эти действия требуют повторного ввода текущего пароля.</p>
    </div>

    <div class="privacy-actions">
      <button type="button" class="privacy-button" :disabled="Boolean(busyAction)" data-testid="export-data" @click="exportData">
        Скачать мои данные
      </button>
      <RouterLink to="/history" class="privacy-button">Удалить отдельный расклад</RouterLink>
      <button type="button" class="privacy-button danger" :disabled="Boolean(busyAction)" data-testid="delete-all-readings" @click="deleteAllReadings">
        Удалить всю историю
      </button>
      <button type="button" class="privacy-button danger" :disabled="Boolean(busyAction)" data-testid="request-account-deletion" @click="requestAccountDeletion">
        Удалить аккаунт
      </button>
    </div>

    <p v-if="privacy.deletionStatus?.requested" class="deletion-status" data-testid="account-deletion-status">
      Статус удаления аккаунта: {{ privacy.deletionStatus.status ?? 'принят' }}
    </p>
  </section>
</template>

<style scoped>
.privacy-center {
  margin-bottom: 1.5rem;
  padding: 1.5rem;
}
.privacy-heading,
.privacy-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}
.privacy-heading h2 {
  margin-top: 0.25rem;
  color: #f5c26b;
  font-size: 1.3rem;
}
.privacy-heading a {
  color: #f5c26b;
  font-size: 0.75rem;
  text-decoration: underline;
}
.privacy-kicker,
.consent-list h3 {
  color: rgba(245, 194, 107, 0.75);
  font-size: 0.68rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}
.privacy-row {
  margin-top: 0.9rem;
  border-top: 1px solid rgba(245, 194, 107, 0.14);
  padding-top: 0.9rem;
}
.privacy-row p,
.reauth-block p,
.privacy-muted {
  margin-top: 0.25rem;
  color: rgba(224, 212, 186, 0.62);
  font-size: 0.75rem;
  line-height: 1.45;
}
.privacy-row input {
  accent-color: #f5c26b;
}
.consent-list,
.reauth-block {
  margin-top: 1.2rem;
}
.reauth-block label {
  display: block;
  margin-bottom: 0.35rem;
  color: rgba(245, 194, 107, 0.78);
  font-size: 0.75rem;
}
.reauth-block input {
  width: 100%;
  border: 1px solid rgba(245, 194, 107, 0.28);
  border-radius: 0.6rem;
  background: rgba(0, 0, 0, 0.25);
  padding: 0.7rem;
}
.privacy-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  margin-top: 1rem;
}
.privacy-button {
  display: inline-flex;
  min-height: 2.5rem;
  align-items: center;
  border: 1px solid rgba(245, 194, 107, 0.3);
  border-radius: 999px;
  padding: 0.55rem 0.85rem;
  color: #f5c26b;
  font-size: 0.75rem;
}
.privacy-button.danger {
  border-color: rgba(252, 165, 165, 0.35);
  color: #fca5a5;
}
.privacy-button:disabled {
  opacity: 0.5;
}
.privacy-error,
.privacy-success,
.deletion-status {
  margin-top: 0.75rem;
  border-left: 2px solid currentColor;
  padding-left: 0.65rem;
  font-size: 0.78rem;
}
.privacy-error {
  color: #fca5a5;
}
.privacy-success,
.deletion-status {
  color: #f5c26b;
}
@media (max-width: 640px) {
  .privacy-heading,
  .privacy-row {
    flex-direction: column;
  }
  .privacy-actions > * {
    width: 100%;
    justify-content: center;
  }
}
</style>
