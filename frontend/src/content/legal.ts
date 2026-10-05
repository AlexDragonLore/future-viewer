import { reactive, shallowRef } from 'vue'
import { httpClient } from '@/api/httpClient'

export type LegalDocumentType =
  | 'privacy'
  | 'personal-data-consent'
  | 'cookies'
  | 'offer'
  | 'marketing-consent'
  | 'ai-disclaimer'
  | 'data-request'
  | 'processors'

export interface ProcessorFact {
  providerName: string
  purpose: string
  legalEntity: string
  country: string
  endpoint: string
  dataCategories: string
  retention: string
  usesDataForTraining: boolean
  crossBorderTransfer: boolean
  contractReference: string
  enabled: boolean
  manuallyApproved: boolean
}

export interface LegalSection {
  title: string
  paragraphs: string[]
  items?: string[]
}

export interface LegalDocumentContent {
  title: string
  version: string
  effectiveAt: string
  intro: string
  sections: LegalSection[]
}

export interface BrowserStorageFact {
  name: string
  owner: string
  purpose: string
  retention: string
  category: 'Необходимые' | 'Настройки' | 'Аналитические'
  storage: string
  country: string
}

function envString(key: string) {
  const value = import.meta.env[key]
  return typeof value === 'string' && value.trim() ? value.trim() : ''
}

let processorRegistryValid = false

function parseProcessors(): ProcessorFact[] {
  const source = envString('VITE_LEGAL_PROCESSORS_JSON')
  if (!source) return []

  try {
    const value: unknown = JSON.parse(source)
    if (!Array.isArray(value)) return []
    const processors = value.filter((entry): entry is ProcessorFact => {
      if (!entry || typeof entry !== 'object') return false
      const item = entry as Partial<ProcessorFact>
      return [
        item.providerName,
        item.purpose,
        item.legalEntity,
        item.country,
        item.endpoint,
        item.dataCategories,
        item.retention,
        item.contractReference,
      ].every((field) => typeof field === 'string' && field.trim().length > 0)
        && typeof item.usesDataForTraining === 'boolean'
        && typeof item.crossBorderTransfer === 'boolean'
        && typeof item.enabled === 'boolean'
        && item.manuallyApproved === true
    })
    processorRegistryValid = processors.length > 0 && processors.length === value.length
    return processors
  } catch {
    return []
  }
}

const sharedDocumentVersion = envString('VITE_LEGAL_DOCUMENT_VERSION')
const sharedEffectiveAt = envString('VITE_LEGAL_EFFECTIVE_AT')

export const legalDocumentVersions = reactive({
  offer: envString('VITE_LEGAL_OFFER_VERSION') || sharedDocumentVersion,
  privacy: envString('VITE_LEGAL_PRIVACY_VERSION') || sharedDocumentVersion,
  personalDataConsent: envString('VITE_LEGAL_PERSONAL_DATA_CONSENT_VERSION') || sharedDocumentVersion,
  marketingConsent: envString('VITE_LEGAL_MARKETING_CONSENT_VERSION') || sharedDocumentVersion,
  cookies: envString('VITE_LEGAL_COOKIES_VERSION') || sharedDocumentVersion,
  aiDisclaimer: envString('VITE_LEGAL_AI_DISCLAIMER_VERSION') || sharedDocumentVersion,
  dataRequest: envString('VITE_LEGAL_DATA_REQUEST_VERSION') || sharedDocumentVersion,
  processors: envString('VITE_LEGAL_PROCESSORS_VERSION') || sharedDocumentVersion,
})

export const merchant = {
  serviceName: envString('VITE_MERCHANT_SERVICE_NAME'),
  ownerName: envString('VITE_MERCHANT_OWNER_NAME'),
  taxStatus: envString('VITE_MERCHANT_TAX_STATUS'),
  inn: envString('VITE_MERCHANT_INN'),
  email: envString('VITE_MERCHANT_EMAIL'),
  postalAddress: envString('VITE_MERCHANT_POSTAL_ADDRESS'),
} as const

export const paidProduct = {
  title: envString('VITE_PAID_PRODUCT_TITLE'),
  price: envString('VITE_PAID_PRODUCT_PRICE'),
  period: envString('VITE_PAID_PRODUCT_PERIOD'),
  description: envString('VITE_PAID_PRODUCT_DESCRIPTION'),
  npdReceiptProcedure: envString('VITE_NPD_RECEIPT_PROCEDURE'),
  supportResponseTerm: envString('VITE_SUPPORT_RESPONSE_TERM'),
  refundMethod: envString('VITE_REFUND_METHOD'),
  claimReviewProcedure: envString('VITE_CLAIM_REVIEW_PROCEDURE'),
  included: [
    'доступ к доступным в интерфейсе раскладам на оплаченный период',
    'развлекательная и информационная интерпретация выбранных карт с помощью ИИ',
    'сохранение раскладов по умолчанию с возможностью отключить историю в настройках или для отдельного расклада',
  ],
} as const

export const retentionFacts = {
  readings: envString('VITE_LEGAL_READING_RETENTION'),
  accounts: envString('VITE_LEGAL_ACCOUNT_RETENTION'),
  consents: envString('VITE_LEGAL_CONSENT_RETENTION'),
  logs: envString('VITE_LEGAL_LOG_RETENTION'),
  backups: envString('VITE_LEGAL_BACKUP_RETENTION'),
} as const

export const processorFacts = parseProcessors()

export const browserStorageFacts: BrowserStorageFact[] = [
  {
    name: 'fv_pending_payment_v1',
    owner: merchant.ownerName,
    purpose: 'Сохраняет идентификатор ожидаемого заказа и текущего аккаунта для проверки оплаты после возвращения от провайдера.',
    retention: 'До подтверждения/отмены оплаты; не более 24 часов действия. Просроченная запись удаляется при следующем обращении.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; проверка заказа выполняется API сервиса.',
  },
  {
    name: 'fv_analytics_goals_v1',
    owner: merchant.ownerName,
    purpose: 'Исключает повторный подсчёт целей по локальным хешам завершённых операций. Хеши и исходные идентификаторы в Метрику не передаются.',
    retention: 'До 48 часов действия, не более 128 записей; удаляется при отзыве аналитики.',
    category: 'Аналитические',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; внешняя передача отсутствует.',
  },
  {
    name: 'fv_guest_reading_v1',
    owner: merchant.ownerName,
    purpose: 'Сохраняет зашифрованный билет гостевого расклада, чтобы продолжить после регистрации и подтверждения почты. Открытый текст вопроса и толкования не записывается.',
    retention: 'Билет действует 24 часа; удаляется после открытия полного результата или при следующем обращении после истечения срока.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; при продолжении билет передаётся оператору сервиса.',
  },
  {
    name: 'fv_cookie_preferences_v1',
    owner: merchant.ownerName,
    purpose: 'Хранит версию и выбор категорий хранилища браузера.',
    retention: 'До 365 дней либо до удаления пользователем.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; внешняя передача отсутствует.',
  },
  {
    name: 'fv_token',
    owner: merchant.ownerName,
    purpose: 'Поддерживает аутентифицированную сессию.',
    retention: 'До выхода, отзыва/истечения токена либо очистки хранилища браузера.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; в API передаётся оператору сервиса.',
  },
  {
    name: 'fv_email',
    owner: merchant.ownerName,
    purpose: 'Показывает email текущего аккаунта в интерфейсе.',
    retention: 'До выхода либо очистки хранилища браузера.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; внешняя передача отсутствует.',
  },
  {
    name: 'fv_user_id',
    owner: merchant.ownerName,
    purpose: 'Связывает состояние интерфейса с текущим аккаунтом.',
    retention: 'До выхода либо очистки хранилища браузера.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; внешняя передача отсутствует.',
  },
  {
    name: 'fv_is_admin',
    owner: merchant.ownerName,
    purpose: 'Управляет видимостью административной навигации; права проверяет сервер.',
    retention: 'До выхода либо очистки хранилища браузера.',
    category: 'Необходимые',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; внешняя передача отсутствует.',
  },
  {
    name: 'fv_deck',
    owner: merchant.ownerName,
    purpose: 'Запоминает выбранную пользователем колоду.',
    retention: 'До отзыва категории «Настройки» либо очистки хранилища браузера.',
    category: 'Настройки',
    storage: 'localStorage браузера',
    country: 'Устройство пользователя; внешняя передача отсутствует.',
  },
]

const requiredOperatorValues = Object.values(merchant)
const requiredProductValues = [
  paidProduct.title,
  paidProduct.price,
  paidProduct.period,
  paidProduct.description,
  paidProduct.npdReceiptProcedure,
  paidProduct.supportResponseTerm,
  paidProduct.refundMethod,
  paidProduct.claimReviewProcedure,
]
const requiredRetentionValues = Object.values(retentionFacts)
const requiredVersions = Object.values(legalDocumentVersions)

export const legalPublication = {
  isVerified:
    envString('VITE_LEGAL_OPERATOR_VERIFIED') === 'true'
    && envString('VITE_LEGAL_PROCESSORS_VERIFIED') === 'true'
    && envString('VITE_NPD_RECEIPT_PROCESS_VERIFIED') === 'true'
    && requiredOperatorValues.every(Boolean)
    && requiredProductValues.every(Boolean)
    && requiredRetentionValues.every(Boolean)
    && requiredVersions.every(Boolean)
    && Boolean(sharedEffectiveAt)
    && processorRegistryValid
    && processorFacts.some((processor) => processor.enabled),
  effectiveAt: sharedEffectiveAt,
} as const

const publishedDocuments = shallowRef<Partial<Record<LegalDocumentType, LegalDocumentContent>>>({})
let loadingDocuments: Promise<void> | undefined

const versionKeys = {
  offer: 'offer',
  privacy: 'privacy',
  'personal-data-consent': 'personalDataConsent',
  'marketing-consent': 'marketingConsent',
  cookies: 'cookies',
  'ai-disclaimer': 'aiDisclaimer',
  'data-request': 'dataRequest',
  processors: 'processors',
} as const

interface PublishedLegalDocument extends LegalDocumentContent {
  documentType: LegalDocumentType
  contentHash: string
}

export async function loadLegalDocuments() {
  if (Object.keys(publishedDocuments.value).length === Object.keys(versionKeys).length) return
  if (loadingDocuments) return loadingDocuments
  loadingDocuments = (async () => {
    const { data } = await httpClient.get<{ documents: PublishedLegalDocument[] }>('/api/public/legal-documents')
    const entries = data.documents
    if (!Array.isArray(entries) || entries.length !== Object.keys(versionKeys).length
      || new Set(entries.map(entry => entry.documentType)).size !== Object.keys(versionKeys).length
      || entries.some(entry => !Object.hasOwn(versionKeys, entry.documentType) || !entry.version || entry.version.length > 64
        || !/^[a-f0-9]{64}$/i.test(entry.contentHash) || !entry.title || !entry.intro || !entry.effectiveAt
        || !Array.isArray(entry.sections))) {
      throw new Error('Не удалось загрузить условия сервиса. Обновите страницу и попробуйте ещё раз.')
    }
    const documents: Partial<Record<LegalDocumentType, LegalDocumentContent>> = {}
    for (const entry of entries) {
      documents[entry.documentType] = entry
      legalDocumentVersions[versionKeys[entry.documentType]] = entry.version
    }
    publishedDocuments.value = documents
  })()
  try {
    await loadingDocuments
  } finally {
    loadingDocuments = undefined
  }
}

export function getLegalDocument(type: LegalDocumentType): LegalDocumentContent | null {
  return publishedDocuments.value[type] ?? null
}

export function isApprovedProcessorUrl(value: string, purpose: 'payment') {
  try {
    const target = new URL(value)
    if (target.protocol !== 'https:' || target.username || target.password || target.hash
      || (target.port && target.port !== '443')) return false
    // Checkout hosts used by the configured YooKassa/YooMoney integrations.
    if (['yoomoney.ru', 'yookassa.ru'].includes(target.hostname.toLowerCase())) return true
    const purposeTokens = [purpose, 'оплат']
    return processorFacts.some((processor) => {
      if (!processor.enabled || !purposeTokens.some((token) => processor.purpose.toLowerCase().includes(token))) return false
      try {
        return new URL(processor.endpoint).origin === target.origin
      } catch {
        return false
      }
    })
  } catch {
    return false
  }
}
