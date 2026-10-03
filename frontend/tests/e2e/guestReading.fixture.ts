export const previewReading = {
  id: '11111111-1111-1111-1111-111111111111',
  spreadType: 1,
  spreadName: 'Карта дня',
  question: 'На что мне сейчас стоит обратить внимание?',
  createdAt: '2026-10-03T12:00:00Z',
  deckType: 0,
  isPreview: true,
  cards: [{
    position: 0, positionName: 'Карта дня', positionMeaning: 'Взгляд на сегодняшний день',
    cardId: 20, cardName: 'Солнце', imagePath: '/cards/major/19.jpg',
    isReversed: false, meaning: 'Ясность, тепло и радость простых вещей.',
  }],
  interpretation: '## Твоя карта — Солнце\n\nЭта карта предлагает заметить то, что уже приносит тебе радость. Возможно, сейчас стоит уделить внимание простым вещам и людям рядом…',
}

export const fullReading = {
  ...previewReading,
  isPreview: false,
  interpretation: previewReading.interpretation.slice(0, -1) + '.\n\n## Следующий шаг\n\nВыбери одно небольшое дело, которое давно хотелось сделать для себя. Подумай, что поможет тебе двигаться спокойнее и увереннее. Карта может стать поводом для размышления: где ты уже чувствуешь ясность, а где хочется больше поддержки?',
}

export const guestResponse = () => ({
  reading: previewReading, ticket: 'qa-encrypted-ticket', expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
})

export const verifiedAuth = {
  accessToken: 'qa-verified-session', userId: 'qa-user', email: 'qa@example.com',
  isAdmin: false, expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
}
