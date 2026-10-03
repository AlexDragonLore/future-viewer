export const legalDocumentsResponse = {
  documents: [
    ['offer', 'Публичная оферта'],
    ['privacy', 'Политика обработки персональных данных'],
    ['personal-data-consent', 'Согласие на обработку персональных данных'],
    ['marketing-consent', 'Согласие на рекламные сообщения'],
    ['cookies', 'Политика cookies и хранилища браузера'],
    ['ai-disclaimer', 'Уведомление об ИИ и Таро'],
    ['data-request', 'Права пользователя и обращения о данных'],
    ['processors', 'Реестр обработчиков'],
  ].map(([documentType, title]) => ({
    documentType,
    title,
    version: `published-${documentType}`,
    contentHash: 'a'.repeat(64),
    effectiveAt: '2026-10-03',
    intro: 'Опубликованные условия сервиса «Вуаль Грядущего».',
    sections: [{ title: 'Условия использования', paragraphs: ['Пользователь управляет своими данными и необязательными согласиями.'] }],
  })),
}
