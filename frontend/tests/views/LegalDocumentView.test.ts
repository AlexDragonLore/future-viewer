/// <reference types="node" />
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import LegalDocumentView from '@/views/LegalDocumentView.vue'
import { router } from '@/router'
import type { LegalDocumentType } from '@/content/legal'
import legalViewSource from '@/views/LegalDocumentView.vue?raw'
import indexSource from '../../index.html?raw'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

const legalSource = readFileSync(path.resolve(process.cwd(), '../backend/src/FutureViewer.Infrastructure/Compliance/published-legal-documents.json'), 'utf8')

vi.mock('@/api/httpClient', () => ({
  httpClient: { get: vi.fn().mockImplementation(async () => ({ data: legalDocumentsResponse })) },
}))

const types: LegalDocumentType[] = [
  'privacy',
  'personal-data-consent',
  'cookies',
  'offer',
  'marketing-consent',
  'ai-disclaimer',
  'data-request',
  'processors',
]

const forbidden = /\bTODO\b|указать адрес|указать провайдера|\[ФИО\]|\[ИНН\]|example\.com/iu

describe('published legal documents', () => {
  it.each(types)('loads the actual published %s version without approval environment flags', async (documentType) => {
    const wrapper = mount(LegalDocumentView, {
      props: { documentType },
      global: { stubs: { RouterLink: true } },
    })
    await flushPromises()
    expect(wrapper.find('[data-testid="legal-document"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="legal-publication-blocked"]').exists()).toBe(false)
    expect(wrapper.text()).toContain(`published-${documentType}`)
    expect(wrapper.text()).not.toMatch(forbidden)
  })

  it('declares all eight required legal routes and redirects legacy paths', () => {
    const paths = router.getRoutes().map((route) => route.path)
    for (const path of [
      '/legal/privacy',
      '/legal/personal-data-consent',
      '/legal/cookies',
      '/legal/offer',
      '/legal/marketing-consent',
      '/legal/ai-disclaimer',
      '/legal/data-request',
      '/legal/processors',
    ]) expect(paths).toContain(path)
  })

  it('contains no forbidden public-document placeholders in the legal source', () => {
    const source = [legalSource, legalViewSource].join('\n')
    expect(source).not.toMatch(forbidden)
  })

  it('keeps the personal-data consent separate and documents the processing scope', () => {
    expect(legalSource).toContain('Согласие на обработку персональных данных подтверждается отдельным действием')
    expect(legalSource).toContain('не объединяется с принятием публичной оферты')
    expect(legalSource).toContain('Для создания и работы аккаунта обрабатываются email, хеш пароля')
    expect(legalSource).toContain('Операции включают получение, запись, систематизацию')
    expect(legalSource).toContain('Изменение документа не создаёт согласие автоматически')
  })

  it('does not load remote fonts or optional analytics from the application shell', () => {
    expect(indexSource).not.toContain('fonts.googleapis.com')
    expect(indexSource).not.toContain('fonts.gstatic.com')
    expect(indexSource).not.toMatch(/mc\.yandex|googletagmanager|google-analytics/iu)
  })
})
