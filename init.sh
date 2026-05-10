#!/usr/bin/env sh
set -eu

ROOT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
ROOT_ENV="$ROOT_DIR/.env"
FRONTEND_ENV="$ROOT_DIR/frontend/.env.local"

append_env_if_missing() {
  file="$1"
  key="$2"
  value="$3"

  mkdir -p "$(dirname "$file")"
  touch "$file"

  if ! grep -q "^${key}=" "$file"; then
    printf '%s=%s\n' "$key" "$value" >> "$file"
  fi
}

append_legal_env() {
  file="$1"

  append_env_if_missing "$file" "VITE_MERCHANT_SERVICE_NAME" "Future Viewer"
  append_env_if_missing "$file" "VITE_MERCHANT_OWNER_NAME" "Дунецев Александр Дмитриевич"
  append_env_if_missing "$file" "VITE_MERCHANT_TAX_STATUS" "самозанятый, плательщик налога на профессиональный доход"
  append_env_if_missing "$file" "VITE_MERCHANT_INN" "592108465422"
  append_env_if_missing "$file" "VITE_MERCHANT_PHONE" "79967669613"
  append_env_if_missing "$file" "VITE_MERCHANT_EMAIL" "duntsev010@mail.ru"
  append_env_if_missing "$file" "VITE_MERCHANT_POSTAL_ADDRESS" "duntsev010@mail.ru"
  append_env_if_missing "$file" "VITE_PAID_PRODUCT_TITLE" "Подписка Future Viewer Pro"
  append_env_if_missing "$file" "VITE_PAID_PRODUCT_PRICE" "300 ₽"
  append_env_if_missing "$file" "VITE_PAID_PRODUCT_PERIOD" "1 месяц"
  append_env_if_missing "$file" "VITE_PAID_PRODUCT_DESCRIPTION" "Цифровая услуга доступа к безлимитным Таро-раскладам в онлайн-сервисе Future Viewer."
}

append_legal_env "$ROOT_ENV"
append_legal_env "$FRONTEND_ENV"

printf 'Environment defaults are ready:\n'
printf '  %s\n' "$ROOT_ENV"
printf '  %s\n' "$FRONTEND_ENV"
