#!/usr/bin/env node

import { readFile, readdir } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { validateAssetEvidence, validatePrimaryLocations } from './lib/compliance-evidence.mjs'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const scopeArg = process.argv.find((arg) => arg.startsWith('--scope='))
const scope = scopeArg ? scopeArg.slice('--scope='.length) : 'all'
const validScopes = new Set(['all', 'backend', 'frontend', 'release'])

if (!validScopes.has(scope)) {
  console.error(`Unknown compliance scope: ${scope}`)
  process.exit(2)
}

const errors = []
const documentaryWarnings = []
const addWarning = (message) => documentaryWarnings.push(message)
const addError = (message) => errors.push(message)
const isBlank = (value) => value === null || value === undefined || (typeof value === 'string' && value.trim() === '')

async function readJson(relativePath) {
  try {
    return JSON.parse(await readFile(path.join(repoRoot, relativePath), 'utf8'))
  } catch (error) {
    addError(`${relativePath}: cannot read valid JSON (${error.message})`)
    return null
  }
}

async function readText(relativePath) {
  try {
    return await readFile(path.join(repoRoot, relativePath), 'utf8')
  } catch (error) {
    addError(`${relativePath}: cannot read (${error.message})`)
    return ''
  }
}

async function walk(relativeDir, predicate = () => true) {
  const absoluteDir = path.join(repoRoot, relativeDir)
  const result = []
  let entries
  try {
    entries = await readdir(absoluteDir, { withFileTypes: true })
  } catch {
    return result
  }

  for (const entry of entries) {
    if (['node_modules', 'bin', 'obj', 'dist', 'test-results', 'playwright-report', '.git'].includes(entry.name)) continue
    const child = path.join(relativeDir, entry.name)
    if (entry.isDirectory()) result.push(...await walk(child, predicate))
    else if (entry.isFile() && predicate(child)) result.push(child)
  }
  return result
}

function validateProcessors(registry) {
  if (!registry || registry.schema_version !== 1 || !Array.isArray(registry.providers) || registry.providers.length === 0) {
    addError('config/processors.json: providers registry is empty or has an unsupported schema_version')
    return []
  }

  const requiredFields = [
    'provider_name', 'provider_type', 'purpose', 'legal_entity', 'country', 'endpoint',
    'additional_endpoints', 'data_categories', 'retention', 'uses_data_for_training',
    'cross_border_transfer', 'contract_reference', 'enabled', 'manual_approved',
  ]
  const names = new Set()

  for (const [index, provider] of registry.providers.entries()) {
    const label = `config/processors.json providers[${index}]`
    if (!provider || typeof provider !== 'object' || Array.isArray(provider)) {
      addError(`${label}: must be an object`)
      continue
    }
    for (const field of requiredFields) {
      if (!(field in provider)) addError(`${label}: missing field ${field}`)
    }
    if (isBlank(provider.provider_name)) addError(`${label}: provider_name is required`)
    if (names.has(provider.provider_name)) addError(`${label}: duplicate provider_name ${provider.provider_name}`)
    names.add(provider.provider_name)
    if (!Array.isArray(provider.data_categories) || provider.data_categories.length === 0) {
      addError(`${label}: data_categories must be non-empty`)
    }
    if (!Array.isArray(provider.additional_endpoints)) addError(`${label}: additional_endpoints must be an array`)
    if (typeof provider.enabled !== 'boolean' || typeof provider.manual_approved !== 'boolean') {
      addError(`${label}: enabled and manual_approved must be booleans`)
    }

    const endpointValues = [provider.endpoint, ...(Array.isArray(provider.additional_endpoints) ? provider.additional_endpoints : [])]
      .filter((value) => !isBlank(value))
    for (const endpoint of endpointValues) {
      let parsed
      try {
        parsed = new URL(endpoint)
      } catch {
        addError(`${label}: endpoint is not an absolute URL: ${endpoint}`)
        continue
      }
      const allowedProtocols = provider.provider_type === 'email'
        ? new Set(['smtps:', 'smtp+starttls:', 'https:'])
        : provider.provider_type === 'storage'
          ? new Set(['https:', 'postgresql:'])
          : provider.provider_type === 'backup'
            ? new Set(['https:', 'file:'])
            : new Set(['https:'])
      if (!allowedProtocols.has(parsed.protocol)) {
        addError(`${label}: endpoint does not use an approved encrypted protocol for ${provider.provider_type}: ${endpoint}`)
      }
      if (provider.provider_type === 'email') {
        if (parsed.protocol === 'smtps:' && parsed.port !== '465') {
          addError(`${label}: SMTPS endpoint must use explicit port 465: ${endpoint}`)
        }
        if (parsed.protocol === 'smtp+starttls:' && !new Set(['25', '587']).has(parsed.port)) {
          addError(`${label}: SMTP STARTTLS endpoint must use explicit port 25 or 587: ${endpoint}`)
        }
      }
      if (parsed.username || parsed.password || parsed.search || parsed.hash) {
        addError(`${label}: endpoint must not contain credentials, query or fragment: ${endpoint}`)
      }
    }

    if (provider.provider_type === 'ai' && provider.enabled && endpointValues.length === 0) {
      addError(`${label}: enabled AI provider has an unknown endpoint`)
    }
    if (provider.enabled) {
      for (const field of ['legal_entity', 'country', 'endpoint', 'retention', 'contract_reference']) {
        if (isBlank(provider[field])) addWarning(`${label}: enabled provider has unknown ${field}`)
      }
      for (const field of ['uses_data_for_training', 'cross_border_transfer']) {
        if (typeof provider[field] !== 'boolean') addWarning(`${label}: enabled provider has unknown ${field}`)
      }
      if (!provider.manual_approved) addWarning(`${label}: enabled provider is not manually approved`)
    }
    if (provider.manual_approved && !provider.enabled) {
      addError(`${label}: manual_approved=true while enabled=false; approval state is ambiguous`)
    }
  }
  return registry.providers
}

function validateRequiredProductionProcessors(providers) {
  const required = [
    'Production Docker host',
    'Primary PostgreSQL',
    'Backup storage',
    'SMTP provider',
    'Support mailbox provider',
    'GitHub and GitHub Actions',
    'DNS and domain registrar',
    'TLS certificate authority',
    'NPD receipt channel',
  ]
  for (const name of required) {
    const provider = providers.find((item) => item.provider_name === name)
    if (!provider?.enabled || !provider?.manual_approved) {
      addWarning(`config/processors.json: documentary review remains incomplete (${name})`)
    }
  }
}

async function checkDirectAiCalls() {
  const files = await walk('backend', (name) => name.endsWith('.cs'))
  const factoryPath = 'backend/src/FutureViewer.Infrastructure/AI/AIChatClientFactory.cs'
  const interpreterPath = 'backend/src/FutureViewer.Infrastructure/AI/OpenAIInterpreter.cs'
  const transportPatterns = [
    /\bOpenAIClient\b/,
    /\bChatClient\b/,
    /\.CompleteChat(?:Streaming)?Async\s*\(/,
    /\.GetChatClient\s*\(/,
  ]

  for (const file of files) {
    const normalized = file.replaceAll('\\', '/')
    if (normalized.includes('/tests/')) continue
    const text = await readText(file)
    if (!transportPatterns.some((pattern) => pattern.test(text))) continue

    if (normalized === factoryPath) {
      if (!/ValidateOfficialEndpoint\s*\(/.test(text) ||
          !/AI integration is disabled/.test(text) ||
          !/ApiKey is not configured/.test(text)) {
        addError(`${file}: AI transport requires an enabled feature, credentials and an exact official HTTPS endpoint`)
      }
      continue
    }

    if (normalized === interpreterPath) {
      for (const requirement of [
        { pattern: /IAiPrivacyGateway/, label: 'IAiPrivacyGateway dependency' },
        { pattern: /_privacyGateway\.Prepare\s*\(/, label: 'gateway Prepare call' },
        { pattern: /privacy\.SafeText/, label: 'gateway SafeText in the outgoing prompt' },
      ]) {
        if (!requirement.pattern.test(text)) addError(`${file}: missing ${requirement.label}`)
      }
      if (/promptContext\s*\.\s*(?:FirstName|LastName|Birth\w*|Memory\w*|Email|Telegram\w*|UserId)\b/i.test(text)) {
        addError(`${file}: identity, profile or memory data must not enter the AI prompt`)
      }
      continue
    }

    addError(`${file}: direct AI transport is forbidden outside the exact reviewed factory/interpreter boundary`)
  }

  const frontendFiles = await walk('frontend/src', (name) => /\.(?:ts|vue|js)$/.test(name))
  for (const file of frontendFiles) {
    const text = await readText(file)
    if (/api\.openai\.com|api\.deepseek\.com|\bOpenAIClient\b|\/v1\/chat\/completions/i.test(text)) {
      addError(`${file}: browser-side AI provider transport is forbidden`)
    }
  }
}

async function checkLegalPublication() {
  const candidates = [
    'backend/src/FutureViewer.Infrastructure/Compliance/published-legal-documents.json',
    ...(await walk('frontend/src/content', (name) => /\.(?:ts|js|json|md|html)$/.test(name))),
    ...(await walk('frontend/src/views', (name) => /(?:legal|privacy|cookie|offer|consent|disclaimer|datarequest).+\.(?:vue|ts|md|html)$/i.test(name))),
  ]
  const forbidden = [
    { pattern: /\bTODO\b/i, label: 'TODO' },
    { pattern: /указать\s+адрес/i, label: 'указать адрес' },
    { pattern: /указать\s+провайдер/i, label: 'указать провайдера' },
    { pattern: /\[\s*ФИО\s*\]/i, label: '[ФИО]' },
    { pattern: /\[\s*ИНН\s*\]/i, label: '[ИНН]' },
    { pattern: /example\.com/i, label: 'example.com' },
  ]

  for (const file of new Set(candidates)) {
    const text = await readText(file)
    for (const item of forbidden) {
      if (item.pattern.test(text)) addError(`${file}: public legal content contains forbidden placeholder ${item.label}`)
    }
  }
}

async function checkRegistrationConsentBoundary() {
  const consentType = await readText('backend/src/FutureViewer.Domain/Enums/ConsentType.cs')
  const registerRequest = await readText('backend/src/FutureViewer.DomainServices/DTOs/RegisterRequest.cs')
  const validator = await readText('backend/src/FutureViewer.DomainServices/Validation/RegisterRequestValidator.cs')
  const privacyService = await readText('backend/src/FutureViewer.DomainServices/Services/PrivacyService.cs')
  const authView = await readText('frontend/src/views/AuthView.vue')
  const frontendTypes = await readText('frontend/src/types/index.ts')
  const legalContent = await readText('backend/src/FutureViewer.Infrastructure/Compliance/published-legal-documents.json')

  if (!/PersonalDataProcessingConsent\s*=\s*4\b/.test(consentType)) {
    addError('ConsentType.cs: a distinct PersonalDataProcessingConsent value is required')
  }
  if (!/\bPersonalDataConsentAccepted\s*\{\s*get;\s*init;\s*\}/.test(registerRequest)) {
    addError('RegisterRequest.cs: mandatory PersonalDataConsentAccepted flag is missing')
  }
  if (!/RuleFor\(x\s*=>\s*x\.PersonalDataConsentAccepted\)[\s\S]{0,160}?\.Equal\(true\)/.test(validator)) {
    addError('RegisterRequestValidator.cs: personal-data consent must be explicitly true')
  }
  if (!/ConsentType\.PersonalDataProcessingConsent\s*,\s*LegalDocumentType\.PersonalDataConsent\s*,\s*request\.DocumentVersions\.PersonalDataConsent/.test(privacyService)) {
    addError('PrivacyService.cs: personal-data consent must be stored against the exact active document version/hash')
  }
  if (!/const\s+personalDataConsentAccepted\s*=\s*ref\(false\)/.test(authView)) {
    addError('AuthView.vue: personal-data consent checkbox must start unchecked')
  }
  if (!/registrationReady[\s\S]{0,300}?personalDataConsentAccepted\.value/.test(authView)) {
    addError('AuthView.vue: registration must remain blocked without personal-data consent')
  }
  if (!/data-testid="personal-data-consent-acceptance"/.test(authView) ||
      !/to="\/legal\/personal-data-consent"[^>]*>согласие на обработку (?:моих )?персональных данных</i.test(authView)) {
    addError('AuthView.vue: a distinct mandatory personal-data consent control must link to its document')
  }
  if (!/personalDataConsentAccepted:\s*true/.test(frontendTypes) ||
      !/personalDataConsentAccepted:\s*true/.test(authView)) {
    addError('Frontend registration contract: personalDataConsentAccepted=true is required')
  }
  for (const optionalType of ['personalization', 'marketing', 'analytics']) {
    if (!new RegExp(`${optionalType}:\\s*false`).test(authView)) {
      addError(`AuthView.vue: optional consent ${optionalType} must start false`)
    }
  }
  if (!/Согласие на обработку персональных данных подтверждается отдельным действием/.test(legalContent) ||
      !/Изменение документа не создаёт согласие автоматически/.test(legalContent)) {
    addError('legal.ts: the personal-data consent document must describe the separate action and version boundary')
  }
}

async function checkAnalytics(providers) {
  const analyticsPath = 'frontend/src/analytics/metrika.ts'
  const files = await walk('frontend/src', name => /\.(?:html|vue|ts|js|mjs)$/.test(name))
  for (const file of files) {
    const text = await readText(file)
    if (!/mc\.yandex\.(?:ru|com)|googletagmanager\.com|google-analytics\.com/i.test(text)) continue
    if (file !== analyticsPath) {
      addError(`${file}: external analytics transport must use the central consent-aware loader`)
      continue
    }
    for (const requirement of [
      /VITE_YANDEX_METRICA_ID/,
      /this\.counter !== undefined/,
      /isAllowed\('analytics'\)/,
      /!this\.allowed\(\)/,
      /https:\/\/mc\.yandex\.ru\/metrika\/tag\.js/,
      /webvisor:\s*false/,
      /sanitizePageUrl/,
    ]) {
      if (!requirement.test(text)) addError(`${file}: analytics must remain optional, consent-aware and sanitized`)
    }
  }
  const yandex = providers.find(provider => provider.provider_name === 'Yandex Metrica')
  if (!yandex?.manual_approved) addWarning('Yandex Metrica documentary metadata remains unreviewed; runtime requires a counter ID and separate analytics consent')
}

function validateOperator(registry, requireApproval) {
  const operator = registry?.operator
  if (!registry || registry.schema_version !== 1 || !operator || typeof operator !== 'object') {
    addError('config/operator.json: invalid or empty registry')
    return
  }
  if (typeof operator.manual_approved !== 'boolean' || typeof operator.publication_allowed !== 'boolean') {
    addError('config/operator.json: manual_approved and publication_allowed must be booleans')
  }
  if (operator.publication_allowed && !operator.manual_approved) {
    addError('config/operator.json: publication is allowed without manual approval')
  }
  if (operator.publication_allowed) {
    for (const field of ['full_name', 'tax_status', 'taxpayer_id', 'postal_address', 'contact_email', 'support_email', 'evidence_reference', 'reviewed_at']) {
      if (isBlank(operator[field])) addError(`config/operator.json: published operator has unknown ${field}`)
    }
  }
  if (requireApproval && (!operator.publication_allowed || !operator.manual_approved)) {
    addWarning('config/operator.json: operator evidence is not yet reviewed')
  }
}

function validateRetention(registry, requireApproval) {
  if (!registry || registry.schema_version !== 1 || !Array.isArray(registry.policies) || registry.policies.length === 0) {
    addError('config/retention.json: policies registry is empty or invalid')
    return
  }
  const ids = new Set()
  for (const [index, policy] of registry.policies.entries()) {
    const label = `config/retention.json policies[${index}]`
    if (isBlank(policy.policy_id) || ids.has(policy.policy_id)) addError(`${label}: policy_id is missing or duplicated`)
    ids.add(policy.policy_id)
    if (!Array.isArray(policy.data_categories) || policy.data_categories.length === 0) addError(`${label}: data_categories must be non-empty`)
    if (policy.status === 'active') {
      for (const field of ['retention_period', 'legal_basis_reference', 'deletion_mechanism', 'backup_handling']) {
        if (isBlank(policy[field])) addError(`${label}: active policy has unknown ${field}`)
      }
      if (!policy.manual_approved) addWarning(`${label}: active policy lacks documentary review`)
    }
  }
  if (requireApproval && (!registry.manual_approved || registry.policies.some((policy) => policy.status !== 'active' || !policy.manual_approved))) {
    addWarning('config/retention.json: retention policy review remains incomplete')
  }
}

function validateAssets(registry, requireApproval) {
  if (!registry || registry.schema_version !== 1 || !Array.isArray(registry.assets) || registry.assets.length === 0) {
    addError('config/assets.json: asset registry is empty or invalid')
    return
  }
  const ids = new Set()
  const requiredAssetGroups = new Set([
    'tarot-card-images',
    'tarot-card-text-catalog',
    'backend-tarot-card-text-catalog',
    'deck-and-spread-copy',
    'ai-prompt-text',
    'procedural-audio-source',
    'third-party-dependency-licenses',
    'brand-and-social-images',
    'remote-google-fonts',
  ])
  for (const [index, asset] of registry.assets.entries()) {
    const label = `config/assets.json assets[${index}]`
    if (isBlank(asset.asset_id) || ids.has(asset.asset_id)) addError(`${label}: asset_id is missing or duplicated`)
    ids.add(asset.asset_id)
    if (typeof asset.shipped !== 'boolean' || typeof asset.manual_approved !== 'boolean') {
      addError(`${label}: shipped and manual_approved must be booleans`)
    }
    if (asset.shipped && asset.manual_approved) {
      for (const field of ['author', 'license_spdx_or_public_domain_basis', 'evidence_reference', 'sha256_manifest']) {
        if (isBlank(asset[field])) addError(`${label}: approved shipped asset has unknown ${field}`)
      }
      if (asset.commercial_use !== true) addError(`${label}: approved shipped asset does not permit commercial use`)
    }
    if (requireApproval && asset.shipped && !asset.manual_approved) {
      addWarning(`${label}: asset evidence review remains incomplete (${asset.asset_id})`)
    }
  }
  for (const requiredId of requiredAssetGroups) {
    if (!ids.has(requiredId)) addError(`config/assets.json: required asset group is missing (${requiredId})`)
  }
}

function validateLegalPublication(registry, requireApproval) {
  const requiredTypes = new Set([
    'offer', 'privacy', 'personal-data-consent', 'marketing-consent',
    'cookies', 'ai-disclaimer', 'data-request', 'processors',
  ])
  if (!registry || registry.schema_version !== 1 || !registry.service || !Array.isArray(registry.documents)) {
    addError('config/legal-publication.json: registry is empty or invalid')
    return
  }

  const service = registry.service
  if (typeof registry.manual_approved !== 'boolean' || typeof service.manual_approved !== 'boolean') {
    addError('config/legal-publication.json: approval flags must be booleans')
  }
  if (service.auto_renewal !== false) {
    addError('config/legal-publication.json: frontend offer states no auto-renewal, so auto_renewal must be false')
  }
  if (service.manual_approved && service.npd_receipt_process_verified !== true) {
    addError('config/legal-publication.json: NPD receipt process is not manually verified')
  }
  if (service.manual_approved) {
    for (const field of ['service_name', 'paid_product_title', 'price_label', 'access_period', 'description', 'npd_receipt_procedure', 'support_response_term', 'refund_method', 'claim_review_procedure']) {
      if (isBlank(service[field])) addError(`config/legal-publication.json: approved service has unknown ${field}`)
    }
  }

  const seen = new Set()
  const effectiveDates = new Set()
  for (const [index, document] of registry.documents.entries()) {
    const label = `config/legal-publication.json documents[${index}]`
    if (!requiredTypes.has(document.document_type) || seen.has(document.document_type)) {
      addError(`${label}: document_type is unknown or duplicated`)
    }
    seen.add(document.document_type)
    if (document.effective_at) effectiveDates.add(document.effective_at)
    if (document.is_active || document.manual_approved) {
      for (const field of ['version', 'content_hash', 'published_at', 'effective_at']) {
        if (isBlank(document[field])) addError(`${label}: approved document has unknown ${field}`)
      }
      if (!/^sha256:[a-f0-9]{64}$/i.test(document.content_hash ?? '')) {
        addError(`${label}: content_hash must be a sha256:<64 hex> evidence value`)
      }
      if (!document.is_active || !document.manual_approved) {
        addError(`${label}: production document must be active and manually approved`)
      }
    }
  }
  for (const type of requiredTypes) {
    if (!seen.has(type)) addError(`config/legal-publication.json: missing document type ${type}`)
  }
  if (requireApproval && (!registry.manual_approved || !service.manual_approved || isBlank(registry.reviewed_at))) {
    addWarning('config/legal-publication.json: documentary registry remains unreviewed; live text/version/hash come from the published backend catalog')
  }
}


async function validatePublishedLegalCatalog() {
  const catalogPath = 'backend/src/FutureViewer.Infrastructure/Compliance/published-legal-documents.json'
  const catalog = await readJson(catalogPath)
  const required = new Set(['offer', 'privacy', 'personal-data-consent', 'marketing-consent', 'cookies', 'ai-disclaimer', 'data-request', 'processors'])
  if (!Array.isArray(catalog?.documents) || catalog.documents.length !== required.size) {
    addError(`${catalogPath}: all eight published document types are required`)
    return
  }
  const seen = new Set()
  for (const document of catalog.documents) {
    if (!required.has(document.documentType) || seen.has(document.documentType)) addError(`${catalogPath}: unknown or duplicated document type`)
    seen.add(document.documentType)
    const content = document.content
    if (isBlank(content?.title) || isBlank(content?.intro) || !Array.isArray(content?.sections) || !content.sections.length
      || content.sections.some(section => isBlank(section.title) || !Array.isArray(section.paragraphs) || !section.paragraphs.length || section.paragraphs.some(isBlank))) {
      addError(`${catalogPath}: ${document.documentType} has incomplete public text`)
    }
  }
  const publisher = await readText('backend/src/FutureViewer.Infrastructure/Compliance/PublishedLegalDocuments.cs')
  const initializer = await readText('backend/src/FutureViewer.Infrastructure/Persistence/DatabaseInitializer.cs')
  const frontend = await readText('frontend/src/content/legal.ts')
  if (!/SHA256\.HashData[\s\S]{0,100}Content\.GetRawText\(\)/.test(publisher)
    || !/PublishedLegalDocuments\.All/.test(initializer)
    || !/ContentHash = content\.ContentHash/.test(initializer)
    || !/\/api\/public\/legal-documents/.test(frontend)) {
    addError('Published document content, SHA-256, active backend seed and browser versions must share one runtime catalog')
  }
}

async function checkInfrastructure() {
  const deployWorkflow = await readText('.github/workflows/deploy-production.yml')
  const runtimeProcessorGate = await readText('scripts/check-runtime-processors.mjs')
  const compose = await readText('docker-compose.prod.yml')
  const nginx = await readText('frontend/nginx.conf')
  const caddy = await readText('deploy/Caddyfile')
  const prohibitedDefaults = /\$\{VITE_MERCHANT_(?:OWNER_NAME|INN|PHONE|EMAIL|POSTAL_ADDRESS):-[^}]+\}|^(?:ARG )?VITE_MERCHANT_(?:OWNER_NAME|INN|PHONE|EMAIL|POSTAL_ADDRESS)=[^\r\n]+$/m

  if (/docker\s+(?:system|image)\s+prune\b/.test(deployWorkflow)) {
    addError('.github/workflows/deploy-production.yml: host-wide Docker prune is forbidden on a shared host')
  }
  if (/provider_name[^\n]*\.includes\s*\(/.test(runtimeProcessorGate)) {
    addError('scripts/check-runtime-processors.mjs: substring processor-name approval is forbidden')
  }
  if (/(?:^|["'])80:80|(?:^|["'])443:443/m.test(compose)) {
    addError('docker-compose.prod.yml: application stack must not own shared host ports 80/443')
  }
  if (!/server_name\s+alex-taro\.ru\s*;/.test(nginx)) {
    addError('frontend/nginx.conf: exact alex-taro.ru server_name is required')
  }
  if (!/Content-Security-Policy/i.test(caddy) || !/Strict-Transport-Security/i.test(caddy)) {
    addError('deploy/Caddyfile: required CSP/HSTS edge headers are missing')
  }
  for (const file of ['docker-compose.prod.yml', 'docker-compose.yml', 'frontend/Dockerfile', '.env.production.example', 'frontend/.env.production', 'frontend/.env.staging', '.github/workflows/deploy-production.yml']) {
    const text = await readText(file)
    if (prohibitedDefaults.test(text)) addError(`${file}: contains unverified hard-coded operator identity/defaults`)
  }
}

const processorsRegistry = await readJson('config/processors.json')
const operatorRegistry = await readJson('config/operator.json')
const retentionRegistry = await readJson('config/retention.json')
const assetsRegistry = await readJson('config/assets.json')
const legalPublicationRegistry = await readJson('config/legal-publication.json')
const providers = validateProcessors(processorsRegistry)
errors.push(...validatePrimaryLocations(providers))
errors.push(...await validateAssetEvidence(repoRoot, assetsRegistry?.assets))

await checkRegistrationConsentBoundary()
await validatePublishedLegalCatalog()
if (scope === 'all' || scope === 'backend' || scope === 'release') await checkDirectAiCalls()
if (scope === 'all' || scope === 'frontend' || scope === 'release') {
  await checkLegalPublication()
  await checkAnalytics(providers)
}

const release = scope === 'all' || scope === 'release' || process.argv.includes('--require-approval')
validateOperator(operatorRegistry, release)
validateRetention(retentionRegistry, release)
validateAssets(assetsRegistry, release || scope === 'frontend')
validateLegalPublication(legalPublicationRegistry, release)
if (release) {
  validateRequiredProductionProcessors(providers)
  await checkInfrastructure()
}

if (errors.length > 0) {
  console.error(`Compliance gate FAILED (${errors.length} blocker${errors.length === 1 ? '' : 's'}):`)
  for (const error of errors) console.error(`- ${error}`)
  process.exit(1)
}

if (documentaryWarnings.length) {
  console.warn(`Documentary review pending for ${documentaryWarnings.length} metadata items. Runtime readiness and published content integrity are checked independently.`)
}
console.log(`Compliance technical checks passed for scope=${scope}.`)
