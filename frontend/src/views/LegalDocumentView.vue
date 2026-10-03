<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  browserStorageFacts,
  getLegalDocument,
  loadLegalDocuments,
  processorFacts,
  type LegalDocumentType,
} from '@/content/legal'

const props = defineProps<{ documentType: LegalDocumentType }>()
const document = computed(() => getLegalDocument(props.documentType))
const registeredProcessors = computed(() => processorFacts)
const loadError = ref('')
async function load() {
  loadError.value = ''
  try { await loadLegalDocuments() }
  catch { loadError.value = 'Не удалось загрузить документ. Попробуйте ещё раз.' }
}
onMounted(load)
</script>

<template>
  <main class="legal-page px-4 sm:px-6 py-10 sm:py-12">
    <section v-if="!document" class="mystic-card legal-loading" data-testid="legal-document-loading">
      <div class="legal-kicker">ЮРИДИЧЕСКАЯ ИНФОРМАЦИЯ</div>
      <h1>{{ loadError ? 'Документ недоступен' : 'Загружаем документ…' }}</h1>
      <p v-if="loadError" role="alert">{{ loadError }}</p>
      <button v-if="loadError" class="glow-button" @click="load">Повторить</button>
    </section>

    <section v-else class="mystic-card legal-shell" data-testid="legal-document">
      <header>
        <div class="legal-kicker">ВУАЛЬ ГРЯДУЩЕГО</div>
        <h1>{{ document.title }}</h1>
        <p>{{ document.intro }}</p>
        <div class="legal-version">Версия {{ document.version }} · действует с {{ document.effectiveAt }}</div>
      </header>

      <article v-for="section in document.sections" :key="section.title" class="legal-section">
        <h2>{{ section.title }}</h2>
        <p v-for="paragraph in section.paragraphs" :key="paragraph">{{ paragraph }}</p>
        <ul v-if="section.items">
          <li v-for="item in section.items" :key="item">{{ item }}</li>
        </ul>
      </article>

      <section v-if="documentType === 'processors'" class="legal-section processor-section">
        <h2>Разрешённые обработчики</h2>
        <div class="processor-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Провайдер и цель</th>
                <th>Юридическое лицо, страна, адрес сервиса</th>
                <th>Данные и хранение</th>
                <th>Условия</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="processor in registeredProcessors" :key="`${processor.providerName}:${processor.endpoint}`">
                <td><strong>{{ processor.providerName }}</strong><br />{{ processor.purpose }}</td>
                <td>{{ processor.legalEntity }}<br />{{ processor.country }}<br />{{ processor.endpoint }}</td>
                <td>{{ processor.dataCategories }}<br />{{ processor.retention }}</td>
                <td>
                  Обучение: {{ processor.usesDataForTraining ? 'да' : 'нет' }}<br />
                  Трансграничная передача: {{ processor.crossBorderTransfer ? 'да' : 'нет' }}<br />
                  Включён: {{ processor.enabled ? 'да' : 'нет' }}<br />
                  Одобрен вручную: {{ processor.manuallyApproved ? 'да' : 'нет' }}<br />
                  Основание: {{ processor.contractReference }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section v-if="documentType === 'cookies'" class="legal-section processor-section">
        <h2>Реестр cookies и хранилища браузера</h2>
        <div class="processor-table-wrap">
          <table>
            <thead>
              <tr>
                <th>Имя и владелец</th>
                <th>Назначение</th>
                <th>Срок и категория</th>
                <th>Хранилище и страна</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in browserStorageFacts" :key="item.name">
                <td><strong>{{ item.name }}</strong><br />{{ item.owner }}</td>
                <td>{{ item.purpose }}</td>
                <td>{{ item.retention }}<br />{{ item.category }}</td>
                <td>{{ item.storage }}<br />{{ item.country }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <nav class="legal-nav" aria-label="Юридические документы">
        <RouterLink to="/legal/privacy">Политика ПД</RouterLink>
        <RouterLink to="/legal/personal-data-consent">Согласие</RouterLink>
        <RouterLink to="/legal/cookies">Хранение в браузере</RouterLink>
        <RouterLink to="/legal/offer">Оферта</RouterLink>
        <RouterLink to="/legal/marketing-consent">Реклама</RouterLink>
        <RouterLink to="/legal/ai-disclaimer">ИИ и Таро</RouterLink>
        <RouterLink to="/legal/data-request">Права пользователя</RouterLink>
        <RouterLink to="/legal/processors">Обработчики</RouterLink>
      </nav>
    </section>
  </main>
</template>

<style scoped>
.legal-page {
  width: 100%;
  min-height: calc(100vh - 4rem);
}
.legal-shell,
.legal-loading {
  max-width: 68rem;
  margin: 0 auto;
  padding: 1.5rem;
}
.legal-loading {
  max-width: 42rem;
  text-align: center;
}
.legal-loading h1,
.legal-shell h1 {
  margin: 0.6rem 0 1rem;
  color: #f5c26b;
  font-size: clamp(1.75rem, 5vw, 2.6rem);
}
.legal-shell header {
  max-width: 50rem;
  margin: 0 auto 1.25rem;
  text-align: center;
}
.legal-shell header p,
.legal-loading p,
.legal-section {
  color: rgba(224, 212, 186, 0.84);
  line-height: 1.65;
}
.legal-kicker,
.legal-version {
  color: rgba(245, 194, 107, 0.78);
  font-size: 0.7rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
}
.legal-version {
  margin-top: 0.75rem;
  letter-spacing: 0.08em;
}
.legal-section {
  margin-top: 1rem;
  border: 1px solid rgba(245, 194, 107, 0.2);
  border-radius: 0.75rem;
  background: rgba(0, 0, 0, 0.2);
  padding: 1.1rem;
}
.legal-section h2 {
  margin: 0 0 0.7rem;
  color: #f5c26b;
  font-size: 1rem;
}
.legal-section p + p {
  margin-top: 0.7rem;
}
.legal-section ul {
  margin-top: 0.75rem;
  padding-left: 1.2rem;
  list-style: disc;
}
.processor-table-wrap {
  overflow-x: auto;
}
table {
  width: 100%;
  min-width: 52rem;
  border-collapse: collapse;
  font-size: 0.75rem;
}
th,
td {
  border: 1px solid rgba(245, 194, 107, 0.18);
  padding: 0.65rem;
  text-align: left;
  vertical-align: top;
}
.legal-nav {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.55rem;
  margin-top: 1.2rem;
}
.legal-nav a {
  border: 1px solid rgba(245, 194, 107, 0.25);
  border-radius: 999px;
  padding: 0.5rem 0.7rem;
  color: #f5c26b;
  font-size: 0.72rem;
}
</style>
