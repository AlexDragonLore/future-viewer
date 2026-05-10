function envValue(key: string, fallback: string) {
  const value = import.meta.env[key]
  return typeof value === 'string' && value.trim() ? value : fallback
}

export const merchant = {
  serviceName: envValue('VITE_MERCHANT_SERVICE_NAME', 'Future Viewer'),
  ownerName: envValue('VITE_MERCHANT_OWNER_NAME', 'Дунецев Александр Дмитриевич'),
  taxStatus: envValue(
    'VITE_MERCHANT_TAX_STATUS',
    'самозанятый, плательщик налога на профессиональный доход',
  ),
  inn: envValue('VITE_MERCHANT_INN', '592108465422'),
  phone: envValue('VITE_MERCHANT_PHONE', '79967669613'),
  email: envValue('VITE_MERCHANT_EMAIL', 'duntsev010@mail.ru'),
  postalAddress: envValue('VITE_MERCHANT_POSTAL_ADDRESS', 'duntsev010@mail.ru'),
}

export const paidProduct = {
  title: envValue('VITE_PAID_PRODUCT_TITLE', 'Подписка Future Viewer Pro'),
  price: envValue('VITE_PAID_PRODUCT_PRICE', '300 ₽'),
  period: envValue('VITE_PAID_PRODUCT_PERIOD', '1 месяц'),
  description: envValue(
    'VITE_PAID_PRODUCT_DESCRIPTION',
    'Цифровая услуга доступа к безлимитным Таро-раскладам в онлайн-сервисе Future Viewer.',
  ),
  included: [
    'безлимитное создание раскладов на период действия подписки',
    'расклады «Карта дня», «Три карты» и «Кельтский крест»',
    'AI-интерпретация расклада по вопросу пользователя',
    'сохранение истории раскладов в личном кабинете',
  ],
}
