import { beforeEach, describe, expect, it, vi } from 'vitest'
import { legalDocumentsResponse } from '../fixtures/legalDocuments'

const get = vi.fn()
vi.mock('@/api/httpClient', () => ({ httpClient: { get: (...args: unknown[]) => get(...args) } }))

describe('published legal catalog', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.unstubAllEnvs()
    get.mockReset()
    get.mockResolvedValue({ data: legalDocumentsResponse })
  })

  it('uses exact backend versions and text with empty approval/version environment', async () => {
    for (const name of ['VITE_LEGAL_OPERATOR_VERIFIED', 'VITE_LEGAL_PROCESSORS_VERIFIED', 'VITE_LEGAL_DOCUMENT_VERSION', 'VITE_LEGAL_OFFER_VERSION']) {
      vi.stubEnv(name, '')
    }
    const legal = await import('@/content/legal')
    expect(legal.legalPublication.isVerified).toBe(false)
    await legal.loadLegalDocuments()
    expect(legal.legalDocumentVersions.offer).toBe('published-offer')
    expect(legal.legalDocumentVersions.personalDataConsent).toBe('published-personal-data-consent')
    expect(legal.getLegalDocument('offer')?.title).toBe('Публичная оферта')
    expect(get).toHaveBeenCalledWith('/api/public/legal-documents')
  })

  it('retries a failed network load and never assigns fabricated document versions', async () => {
    get.mockRejectedValueOnce(new Error('offline'))
    const legal = await import('@/content/legal')
    await expect(legal.loadLegalDocuments()).rejects.toThrow('offline')
    expect(legal.getLegalDocument('offer')).toBeNull()
    await legal.loadLegalDocuments()
    expect(legal.getLegalDocument('offer')?.version).toBe('published-offer')
    expect(get).toHaveBeenCalledTimes(2)
  })

  it('rejects an incomplete backend catalog', async () => {
    get.mockResolvedValue({ data: { documents: legalDocumentsResponse.documents.slice(0, 2) } })
    const legal = await import('@/content/legal')
    await expect(legal.loadLegalDocuments()).rejects.toThrow('Не удалось загрузить условия')
    expect(legal.getLegalDocument('offer')).toBeNull()
  })

  it.each([
    ['https://yoomoney.ru/checkout/payments/v2/contract?orderId=123', true],
    ['https://yookassa.ru/payment', true],
    ['http://yoomoney.ru/payment', false],
    ['https://yoomoney.ru.attacker.org/payment', false],
    ['https://attacker.org/yoomoney.ru', false],
    ['https://user:password@yoomoney.ru/payment', false],
    ['https://yoomoney.ru:8080/payment', false],
    ['https://yoomoney.ru/payment#sensitive', false],
    ['javascript:alert(1)', false],
  ])('validates the exact HTTPS checkout destination %s', async (url, expected) => {
    const legal = await import('@/content/legal')
    expect(legal.isApprovedProcessorUrl(url, 'payment')).toBe(expected)
  })
})
