#!/usr/bin/env node

import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')

async function readJson(relativePath) {
  return JSON.parse(await readFile(path.join(repoRoot, relativePath), 'utf8'))
}

function fail(message) {
  console.error(`Legal build environment export blocked: ${message}`)
  process.exit(1)
}

function requirePublicValue(value, label) {
  if (typeof value !== 'string' || !value.trim()) return ''
  if (/[\r\n\0]/.test(value)) fail(`${label} contains a control character`)
  if (/\bTODO\b|указать\s+(?:адрес|провайдер)|\[\s*(?:ФИО|ИНН)\s*\]|example\.com/i.test(value)) {
    fail(`${label} contains a publication placeholder`)
  }
  return value.trim()
}

const [operatorRegistry, processorsRegistry, retentionRegistry, publicationRegistry] = await Promise.all([
  readJson('config/operator.json'),
  readJson('config/processors.json'),
  readJson('config/retention.json'),
  readJson('config/legal-publication.json'),
])

const operator = operatorRegistry.operator
const approvedProcessors = processorsRegistry.providers
  .filter((provider) => provider.enabled && provider.manual_approved)
  .map((provider) => ({
    providerName: requirePublicValue(provider.provider_name, 'processor provider_name'),
    purpose: requirePublicValue(provider.purpose, 'processor purpose'),
    legalEntity: requirePublicValue(provider.legal_entity, 'processor legal_entity'),
    country: requirePublicValue(provider.country, 'processor country'),
    endpoint: requirePublicValue(provider.endpoint, 'processor endpoint'),
    dataCategories: provider.data_categories.map((category) => requirePublicValue(category, 'processor data category')).join(', '),
    retention: requirePublicValue(provider.retention, 'processor retention'),
    usesDataForTraining: provider.uses_data_for_training,
    crossBorderTransfer: provider.cross_border_transfer,
    contractReference: requirePublicValue(provider.contract_reference, 'processor contract_reference'),
    enabled: true,
    manuallyApproved: true,
  }))

const policyById = new Map(retentionRegistry.policies.map((policy) => [policy.policy_id, policy]))
function retention(id) {
  const policy = policyById.get(id)
  if (!policy || policy.status !== 'active' || !policy.manual_approved) return ''
  return requirePublicValue(policy.retention_period, `retention policy ${id}`)
}

const documentByType = new Map(publicationRegistry.documents.map((document) => [document.document_type, document]))
function documentVersion(type) {
  const document = documentByType.get(type)
  if (!document?.is_active || !document?.manual_approved) return ''
  requirePublicValue(document.content_hash, `legal document ${type} content_hash`)
  return requirePublicValue(document.version, `legal document ${type} version`)
}

const effectiveDates = new Set(publicationRegistry.documents.map((document) => document.effective_at))
// Runtime documents supply their own effective dates; export a shared date only when available.
const effectiveAt = effectiveDates.size === 1 ? requirePublicValue([...effectiveDates][0], 'legal effective_at') : ''

const service = publicationRegistry.service
if (service.auto_renewal !== false) fail('published product is inconsistent with the no-auto-renewal offer')

const values = {
  VITE_MERCHANT_SERVICE_NAME: requirePublicValue(service.service_name, 'service name'),
  VITE_MERCHANT_OWNER_NAME: requirePublicValue(operator.full_name, 'operator full_name'),
  VITE_MERCHANT_TAX_STATUS: requirePublicValue(operator.tax_status, 'operator tax_status'),
  VITE_MERCHANT_INN: requirePublicValue(operator.taxpayer_id, 'operator taxpayer_id'),
  VITE_MERCHANT_EMAIL: requirePublicValue(operator.contact_email, 'operator contact_email'),
  VITE_MERCHANT_POSTAL_ADDRESS: requirePublicValue(operator.postal_address, 'operator postal_address'),
  SUPPORT_EMAIL: requirePublicValue(operator.support_email, 'operator support_email'),
  VITE_PAID_PRODUCT_TITLE: requirePublicValue(service.paid_product_title, 'paid product title'),
  VITE_PAID_PRODUCT_PRICE: requirePublicValue(service.price_label, 'paid product price'),
  VITE_PAID_PRODUCT_PERIOD: requirePublicValue(service.access_period, 'paid product access period'),
  VITE_PAID_PRODUCT_DESCRIPTION: requirePublicValue(service.description, 'paid product description'),
  VITE_NPD_RECEIPT_PROCESS_VERIFIED: String(service.npd_receipt_process_verified === true),
  VITE_NPD_RECEIPT_PROCEDURE: requirePublicValue(service.npd_receipt_procedure, 'NPD receipt procedure'),
  VITE_SUPPORT_RESPONSE_TERM: requirePublicValue(service.support_response_term, 'support response term'),
  VITE_REFUND_METHOD: requirePublicValue(service.refund_method, 'refund method'),
  VITE_CLAIM_REVIEW_PROCEDURE: requirePublicValue(service.claim_review_procedure, 'claim review procedure'),
  VITE_LEGAL_OPERATOR_VERIFIED: String(operator?.manual_approved === true && operator?.publication_allowed === true),
  VITE_LEGAL_PROCESSORS_VERIFIED: String(approvedProcessors.length > 0),
  VITE_LEGAL_PROCESSORS_JSON: JSON.stringify(approvedProcessors),
  VITE_LEGAL_EFFECTIVE_AT: effectiveAt,
  VITE_LEGAL_OFFER_VERSION: documentVersion('offer'),
  VITE_LEGAL_PRIVACY_VERSION: documentVersion('privacy'),
  VITE_LEGAL_PERSONAL_DATA_CONSENT_VERSION: documentVersion('personal-data-consent'),
  VITE_LEGAL_MARKETING_CONSENT_VERSION: documentVersion('marketing-consent'),
  VITE_LEGAL_COOKIES_VERSION: documentVersion('cookies'),
  VITE_LEGAL_AI_DISCLAIMER_VERSION: documentVersion('ai-disclaimer'),
  VITE_LEGAL_DATA_REQUEST_VERSION: documentVersion('data-request'),
  VITE_LEGAL_PROCESSORS_VERSION: documentVersion('processors'),
  VITE_LEGAL_READING_RETENTION: retention('reading-history'),
  VITE_LEGAL_ACCOUNT_RETENTION: retention('account-profile'),
  VITE_LEGAL_CONSENT_RETENTION: retention('legal-evidence'),
  VITE_LEGAL_LOG_RETENTION: retention('technical-logs'),
  VITE_LEGAL_BACKUP_RETENTION: retention('backup-rotation'),
}

for (const [key, value] of Object.entries(values)) {
  // Missing registry facts must not overwrite already configured deployment values.
  if (value !== '') process.stdout.write(`${key}=${value}\n`)
}
