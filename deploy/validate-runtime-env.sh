#!/usr/bin/env bash
set -Eeuo pipefail

# Validate actual server configuration after overlays are merged. Never print
# credential values, and never require them to also exist in GitHub Secrets.
if [[ "${1:-}" == "--env-file" ]]; then
  [[ -n "${2:-}" && -f "$2" ]] || { echo "Runtime env file is missing" >&2; exit 1; }
  set -a
  # The deployment owns this mode-600 configuration file.
  # shellcheck disable=SC1090
  source "$2"
  set +a
elif [[ "$#" -ne 0 ]]; then
  echo "Usage: validate-runtime-env.sh [--env-file path]" >&2
  exit 1
fi

fail_runtime() { printf 'Runtime configuration: %s\n' "$*" >&2; exit 1; }
require_value() { [[ -n "${!1:-}" ]] || fail_runtime "$1 is required"; }
validate_bool() {
  local name="$1" default="$2" value
  value="${!name:-$default}"
  [[ "$value" == "true" || "$value" == "false" ]] || fail_runtime "$name must be true or false"
}

validate_bool AI_ENABLED true
if [[ "${AI_ENABLED:-true}" == "true" ]]; then
  case "$(printf '%s' "${AI_PROVIDER:-OpenAI}" | tr '[:upper:]' '[:lower:]')" in
    openai|chatgpt|gpt)
      require_value OPENAI_API_KEY
      [[ "${OPENAI_BASE_URL:-https://api.openai.com/v1}" =~ ^https://api\.openai\.com/v1/?$ ]] || fail_runtime "OPENAI_BASE_URL must be the official HTTPS endpoint"
      [[ -n "${OPENAI_MODEL-gpt-4o}" ]] || fail_runtime "OPENAI_MODEL is required"
      ;;
    deepseek)
      require_value DEEPSEEK_API_KEY
      [[ "${DEEPSEEK_BASE_URL:-https://api.deepseek.com}" =~ ^https://api\.deepseek\.com/?$ ]] || fail_runtime "DEEPSEEK_BASE_URL must be the official HTTPS endpoint"
      [[ -n "${DEEPSEEK_MODEL-deepseek-v4-flash}" ]] || fail_runtime "DEEPSEEK_MODEL is required"
      ;;
    *) fail_runtime "AI_PROVIDER must be OpenAI or DeepSeek" ;;
  esac
fi

validate_bool PAYMENT_ENABLED true
validate_bool PAYMENT_WEBHOOK_ENABLED true
if [[ "${PAYMENT_ENABLED:-true}" == "true" && "${PAYMENT_WEBHOOK_ENABLED:-true}" != "true" ]]; then
  fail_runtime "PAYMENT_WEBHOOK_ENABLED must be true when payments are enabled"
fi
if [[ "${PAYMENT_ENABLED:-true}" == "true" || "${PAYMENT_WEBHOOK_ENABLED:-true}" == "true" ]]; then
  case "$(printf '%s' "${PAYMENT_PROVIDER:-Yukassa}" | tr '[:upper:]' '[:lower:]')" in
    yukassa|yookassa)
      require_value YUKASSA_SHOP_ID
      require_value YUKASSA_SECRET_KEY
      [[ "${YUKASSA_API_BASE_URL:-https://api.yookassa.ru/v3/}" =~ ^https://api\.yookassa\.ru/v3/?$ ]] || fail_runtime "YUKASSA_API_BASE_URL must be the official HTTPS endpoint"
      ;;
    yoomoney)
      require_value YOOMONEY_RECEIVER
      require_value YOOMONEY_NOTIFICATION_SECRET
      [[ "${YOOMONEY_QUICKPAY_URL:-https://yoomoney.ru/quickpay/confirm}" == "https://yoomoney.ru/quickpay/confirm" ]] || fail_runtime "YOOMONEY_QUICKPAY_URL must be the official HTTPS endpoint"
      ;;
    *) fail_runtime "PAYMENT_PROVIDER must be Yukassa or YooMoney" ;;
  esac
fi

require_value EMAIL_FROM
case "$(printf '%s' "${EMAIL_TRANSPORT:-Smtp}" | tr '[:upper:]' '[:lower:]')" in
  regruwebmail)
    require_value EMAIL_USERNAME
    require_value EMAIL_PASSWORD
    ;;
  smtp)
    require_value EMAIL_HOST
    [[ "${EMAIL_USE_SSL:-true}" == "true" ]] || fail_runtime "EMAIL_USE_SSL must remain true for SMTP"
    ;;
  *) fail_runtime "EMAIL_TRANSPORT must be Smtp or RegruWebmail" ;;
esac
require_value SUPPORT_EMAIL

printf 'Actual runtime credentials and HTTPS/TLS endpoints are configured.\n'
