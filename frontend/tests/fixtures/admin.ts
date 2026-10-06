import { legalDocumentsResponse } from './legalDocuments'

export const adminFixtureSession = {
  token: 'local-admin-browser-fixture',
  email: 'admin.browser@example.com',
  userId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  isAdmin: true,
}

const readerId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
const readerEmail = 'reader.with.a.long.email.for.mobile.layout@example.com'

export const adminReadingFixtures = Array.from({ length: 21 }, (_, index) => ({
  id: `cccccccc-cccc-4ccc-8ccc-${String(index + 1).padStart(12, '0')}`,
  userId: readerId,
  userEmail: readerEmail,
  question: index === 0
    ? `Как решиться на смену работы и сохранить спокойствие? ${'Хочу обдумать новый этап и найти подходящий темп. '.repeat(5)}Финальная часть вопроса о переменах.`
    : index === 1
      ? 'Как поддержать себя после сложного разговора?'
      : `Архивный вопрос ${index + 1}: на что обратить внимание сегодня?`,
  interpretation: index === 0
    ? `## Следующий шаг\n\n${'Начни с небольшого действия, которое поможет тебе почувствовать опору. '.repeat(9)}\n\n**Финальный совет: запиши один конкретный шаг на завтра.**`
    : 'Выбери спокойный темп и обрати внимание на собственные силы.',
  spreadType: index === 0 ? 3 : 1,
  deckType: 0,
  createdAt: new Date(Date.UTC(2026, 9, 6, 12, 0) - index * 3_600_000).toISOString(),
  deletedFromHistoryAt: index === 1 ? '2026-10-06T11:30:00Z' : null,
}))

export const adminFeedbackFixtures = [{
  id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
  readingId: adminReadingFixtures[0].id,
  userId: readerId,
  userEmail: readerEmail,
  question: adminReadingFixtures[0].question,
  selfReport: `${'Я попробовал выделить время на отдых и спокойное планирование, и это помогло. '.repeat(6)}Финальная часть отзыва: стало проще действовать.`,
  aiScore: 9,
  aiScoreReason: `${'В отзыве есть конкретное действие и понятное наблюдение за результатом. '.repeat(4)}Финальная часть оценки: осмысленная обратная связь.`,
  isSincere: true,
  scheduledAt: '2026-10-06T10:00:00Z',
  notifiedAt: null,
  answeredAt: '2026-10-06T12:05:00Z',
  status: 3,
  createdAt: '2026-10-05T10:00:00Z',
}]

export const adminUserFixtures = [{
  id: readerId,
  email: readerEmail,
  createdAt: '2026-09-15T08:00:00Z',
  isAdmin: false,
  subscriptionStatus: 1,
  subscriptionExpiresAt: '2026-11-01T00:00:00Z',
  totalReadings: adminReadingFixtures.length,
  totalFeedbacks: 1,
  totalScore: 19,
}]

export const adminUserDetailFixture = {
  ...adminUserFixtures[0],
  yukassaSubscriptionId: null,
  recentReadings: adminReadingFixtures.slice(0, 3),
  recentFeedbacks: adminFeedbackFixtures,
  achievements: [{
    id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
    code: 'first-reading',
    name: 'Первый шаг',
    unlockedAt: '2026-09-15T08:30:00Z',
  }],
}

// Static, serializable responses can also be served by the local browser QA proxy.
// E2E adds search and pagination to the same source rows without any real mutations.
export const adminFixtureResponses: Record<string, unknown> = {
  '/api/public/config': { supportEmail: '', paymentsEnabled: false },
  '/api/public/legal-documents': legalDocumentsResponse,
  '/api/announcements/unread': [],
  '/api/subscription/status': {
    isActive: true, canCreateFreeReading: true, freeReadingsUsedToday: 0, freeReadingsDailyLimit: 1,
  },
  '/api/admin/readings': { items: adminReadingFixtures.slice(0, 20), total: adminReadingFixtures.length },
  '/api/admin/users': { items: adminUserFixtures, total: adminUserFixtures.length },
  [`/api/admin/users/${readerId}`]: adminUserDetailFixture,
  '/api/admin/feedbacks': { items: adminFeedbackFixtures, total: adminFeedbackFixtures.length },
  '/api/admin/stats': {
    totalUsers: 125, adminCount: 2, activeSubscriptions: 18,
    readingsToday: 30, readingsThisWeek: 143, scoredFeedbacksThisMonth: 28,
  },
}
