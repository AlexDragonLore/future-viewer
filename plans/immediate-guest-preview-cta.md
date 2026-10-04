# Гостевое толкование без лишнего ожидания

## Причина и изменение

4 октября тест рекламы получает переходы, но продаж пока нет. В коде гостевой результат уже целиком получен сервером, однако cardsReady включает искусственную печать примерно по 55 символов в секунду. Кнопка регистрации скрыта до окончания печати. Метрика считает показ preview раньше доступности этой кнопки, поэтому текущие события не доказывают, что посетители увидели призыв продолжить.

Показывать готовый гостевой текст сразу, вместе с существующим бесплатным продолжением. Сохранить потоковую выдачу обычных авторизованных раскладов, условия регистрации, подтверждение почты, цену, оплату и аналитику. Это устранение задержки интерфейса, а не обещание роста продаж.

## QA / verification

- Добавить содержательную проверку: весь preview и CTA доступны без ожидания анимационных кадров; обычное потоковое толкование сохраняет постепенный вывод.
- Выполнить связанные ResultView тесты, npm run type-check и npm run build.
- В Browser Use/in-app browser проверить отрендеренный preview с изолированным локальным mock API на desktop и 390×844: готовый текст, видимый CTA, отсутствие переполнения, переход в регистрацию с redirect=/result. Не отправлять настоящие регистрации, письма, искусственные цели или платежи для QA.
- Диагностику production проводить на чтение: доступность формы и health, обезличенные категории серверных ошибок. Различать поломку, неудобство и гипотезы о конверсии.

## Deploy

- Из чистого managed worktree production-guest-seo, ветка codex/production-guest-seo, commit/push ветки и обычный fast-forward origin/main без force. Сохранить посторонние изменения исходного checkout.
- Собрать frontend linux/amd64 с VITE_SITE_URL=https://alex-taro.ru и VITE_YANDEX_METRICA_ID=113366341; доставить новый образ на deploy@80.87.104.245 и обновить /opt/future-viewer до опубликованного коммита.
- Сохранить защищённую резервную копию /opt/fv-app/.env.production, изменить только FV_IMAGE_TAG; текущий backend образ дополнительно пометить новым тегом без его пересоздания.
- Обновить только frontend: sudo -n docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -f docker-compose.prod.prebuilt.yml -f docker-compose.prod.legacy-edge.yml -p future-viewer up -d --no-build --pull never --no-deps frontend.
- Проверить sudo -n docker compose --env-file /opt/fv-app/.env.production -f docker-compose.prod.yml -p future-viewer ps, GET https://alex-taro.ru/health, GET https://janetka.ru/ и production browser smoke главной/регистрации без создания пользователей. Проверить новую frontend сборку и отсутствие ошибок; точный гостевой результат проверен локальным mock без настоящего AI-вызова.
