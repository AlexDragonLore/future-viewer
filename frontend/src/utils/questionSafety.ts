export interface LocalQuestionSafetyResult {
  blocked: boolean
  category?: 'personal_data' | 'sensitive_data' | 'self_harm' | 'violence' | 'medical' | 'medication' | 'financial' | 'legal'
  message?: string
  suggestedQuestion?: string
}

const privacyMessage =
  'Вопрос содержит сведения, которые могут относиться к персональным или чувствительным данным. Уберите имена, контакты, диагнозы, адреса и сведения, позволяющие определить конкретного человека.'

const genericSuggestion = 'Какой безопасный и не связанный с личными данными взгляд на мою ситуацию я могу рассмотреть?'

const rules: Array<{
  category: NonNullable<LocalQuestionSafetyResult['category']>
  pattern: RegExp
  message: string
}> = [
  {
    category: 'self_harm',
    pattern: /(?:самоубий|суицид|покончить с собой|убить себя|самоповреж|не хочу жить)/iu,
    message: 'Если есть риск причинить вред себе или другому человеку, немедленно позвоните 112 или обратитесь к близкому человеку и профильному специалисту. Сервис Таро не подходит для помощи в кризисе.',
  },
  {
    category: 'violence',
    pattern: /(?:избил|изнасил|нападени|угрожает убить|домашн(?:ее|его) насили|похитил)/iu,
    message: 'При угрозе насилия перейдите в безопасное место и обратитесь по номеру 112 или в профильную службу помощи. Не используйте интерпретацию карт вместо экстренной поддержки.',
  },
  {
    category: 'medical',
    pattern: /(?:диагноз|диагностир|беремен|онколог|рак\b|инфаркт|инсульт|кровотеч|срочн.{0,12}(?:врач|медицин)|болен ли)/iu,
    message: 'Сервис не диагностирует заболевания и не оценивает срочность состояния. При острых симптомах позвоните 103 или 112, в остальных случаях обратитесь к врачу.',
  },
  {
    category: 'medication',
    pattern: /(?:лекарств|таблетк|дозиров|антидепрессант|антибиотик|прекратить принимать|начать принимать)/iu,
    message: 'Не начинайте, не отменяйте и не меняйте дозировку лекарств по ответу Таро или AI. Обсудите решение с врачом или фармацевтом.',
  },
  {
    category: 'financial',
    pattern: /(?:взять кредит|оформить кредит|инвестир|купить акци|криптовалют|вложить деньги|гарантированн.{0,8}доход)/iu,
    message: 'Сервис не даёт персональных инвестиционных или кредитных рекомендаций. Проверьте условия, риски и обратитесь к независимому квалифицированному специалисту.',
  },
  {
    category: 'legal',
    pattern: /(?:подписать договор|подать в суд|признать вину|дать показани|уголовн.{0,12}(?:дело|обвин)|юридически значим)/iu,
    message: 'Не совершайте юридически значимые действия на основании Таро или AI. Получите консультацию квалифицированного юриста с учётом документов и обстоятельств.',
  },
  {
    category: 'sensitive_data',
    pattern: /(?:религи|вероисповед|политическ.{0,12}взгляд|национальност|интимн.{0,8}жизн|сексуальн.{0,12}жизн|несовершеннолетн|реб[её]нк.{0,12}(?:адрес|диагноз|телефон))/iu,
    message: privacyMessage,
  },
]

const emailPattern = /\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b/iu
const phonePattern = /(?:^|\D)(?:\+?7|8)[\s()-]*\d{3}[\s()-]*\d{3}[\s-]*\d{2}[\s-]*\d{2}(?:\D|$)/u
const documentPattern = /(?:паспорт|снилс|инн|номер карты|cvv|cvc|банковск.{0,8}сч[её]т|\b\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{4}\b)/iu
const addressPattern = /(?:улиц|проспект|переулок|шоссе|дом|квартир)[а-яё.\s-]{0,24}\d+/iu
const accountPattern = /(?:telegram|телеграм|аккаунт|username|профиль)\s*[:@]\s*@?[a-z0-9_]{3,}/iu

export function assessQuestionLocally(source: string): LocalQuestionSafetyResult {
  const text = source.normalize('NFKC').replace(/\s+/g, ' ').trim()
  if (emailPattern.test(text) || phonePattern.test(text) || documentPattern.test(text) || addressPattern.test(text) || accountPattern.test(text)) {
    return { blocked: true, category: 'personal_data', message: privacyMessage, suggestedQuestion: genericSuggestion }
  }

  for (const rule of rules) {
    if (rule.pattern.test(text)) {
      return { blocked: true, category: rule.category, message: rule.message, suggestedQuestion: genericSuggestion }
    }
  }

  return { blocked: false }
}
