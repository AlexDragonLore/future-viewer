#!/usr/bin/env node

const errors = []

function isTrue(name, defaultValue = false) {
  const raw = process.env[name]
  if (raw === undefined || raw === '') return defaultValue
  if (raw !== 'true' && raw !== 'false') errors.push(`${name} must be true or false`)
  return raw === 'true'
}

function normalizedEndpoint(value) {
  try {
    const url = new URL(value)
    if (url.username || url.password || url.search || url.hash) return null
    return url.href.replace(/\/$/, '').toLowerCase()
  } catch {
    return null
  }
}

if (process.env.APP_DOMAIN !== 'alex-taro.ru') errors.push('APP_DOMAIN must be exactly alex-taro.ru')
if ((process.env.FV_COMPOSE_PROJECT_NAME || 'future-viewer') !== 'future-viewer') {
  errors.push('FV_COMPOSE_PROJECT_NAME must be future-viewer')
}
if (!/^\d+$/.test(process.env.FV_SHARED_EDGE_PORT || '') || Number(process.env.FV_SHARED_EDGE_PORT) < 1024 || Number(process.env.FV_SHARED_EDGE_PORT) > 65535) {
  errors.push('FV_SHARED_EDGE_PORT must be a dedicated numeric loopback port between 1024 and 65535')
}
isTrue('PRIVACY_EXPORT_ENABLED', true)

if (isTrue('AI_ENABLED', true)) {
  const provider = process.env.AI_PROVIDER || 'OpenAI'
  if (/^(?:openai|chatgpt|gpt)$/i.test(provider)) {
    if (normalizedEndpoint(process.env.OPENAI_BASE_URL || 'https://api.openai.com/v1') !== 'https://api.openai.com/v1') {
      errors.push('OpenAI requires its official HTTPS endpoint')
    }
    if (!process.env.OPENAI_API_KEY?.trim()) errors.push('OPENAI_API_KEY is required when OpenAI is enabled')
    if (process.env.OPENAI_MODEL !== undefined && !process.env.OPENAI_MODEL.trim()) errors.push('OPENAI_MODEL is required')
  } else if (/^deepseek$/i.test(provider)) {
    if (normalizedEndpoint(process.env.DEEPSEEK_BASE_URL || 'https://api.deepseek.com') !== 'https://api.deepseek.com') {
      errors.push('DeepSeek requires its official HTTPS endpoint')
    }
    if (!process.env.DEEPSEEK_API_KEY?.trim()) errors.push('DEEPSEEK_API_KEY is required when DeepSeek is enabled')
    if (process.env.DEEPSEEK_MODEL !== undefined && !process.env.DEEPSEEK_MODEL.trim()) errors.push('DEEPSEEK_MODEL is required')
  } else {
    errors.push('AI_PROVIDER is unsupported')
  }
}

const paymentEnabled = isTrue('PAYMENT_ENABLED', true)
const webhookEnabled = isTrue('PAYMENT_WEBHOOK_ENABLED', true)
if (paymentEnabled && !webhookEnabled) errors.push('PAYMENT_WEBHOOK_ENABLED must be true when payments are enabled')
if (paymentEnabled || webhookEnabled) {
  const provider = process.env.PAYMENT_PROVIDER || 'Yukassa'
  if (/^(?:yukassa|yookassa)$/i.test(provider)) {
    if (normalizedEndpoint(process.env.YUKASSA_API_BASE_URL || 'https://api.yookassa.ru/v3/') !== 'https://api.yookassa.ru/v3') {
      errors.push('YooKassa requires its official HTTPS endpoint')
    }
    if (!process.env.YUKASSA_SHOP_ID || !process.env.YUKASSA_SECRET_KEY) errors.push('YooKassa credentials are required')
  } else if (/^yoomoney$/i.test(provider)) {
    if (normalizedEndpoint(process.env.YOOMONEY_QUICKPAY_URL || 'https://yoomoney.ru/quickpay/confirm') !== 'https://yoomoney.ru/quickpay/confirm') {
      errors.push('YooMoney requires its official HTTPS endpoint')
    }
    if (!process.env.YOOMONEY_RECEIVER || !process.env.YOOMONEY_NOTIFICATION_SECRET) errors.push('YooMoney receiver and webhook secret are required')
  } else {
    errors.push('PAYMENT_PROVIDER is unsupported')
  }
}

const emailTransport = (process.env.EMAIL_TRANSPORT || 'Smtp').trim().toLowerCase()
if (emailTransport === 'regruwebmail') {
  if (!process.env.EMAIL_USERNAME?.trim() || !process.env.EMAIL_PASSWORD?.trim()) {
    errors.push('REG.RU webmail mailbox credentials are required in production')
  }
  if (process.env.EMAIL_USERNAME?.trim().toLowerCase() !== process.env.EMAIL_FROM?.trim().toLowerCase()) {
    errors.push('REG.RU webmail EMAIL_FROM must match the mailbox username')
  }
} else if (emailTransport === 'smtp') {
  if (!process.env.EMAIL_HOST) errors.push('EMAIL_HOST is required for SMTP in production')
  if (!isTrue('EMAIL_USE_SSL', true)) errors.push('EMAIL_USE_SSL must be true for SMTP in production')
} else {
  errors.push('EMAIL_TRANSPORT must be Smtp or RegruWebmail')
}
if (!process.env.EMAIL_FROM) errors.push('EMAIL_FROM is required in production')
if (!process.env.SUPPORT_EMAIL) errors.push('SUPPORT_EMAIL is required in production')

if (errors.length) {
  console.error(`Runtime processor gate FAILED (${errors.length}):`)
  for (const error of errors) console.error(`- ${error}`)
  process.exit(1)
}

console.log('Runtime credentials and official encrypted provider endpoints are configured.')
