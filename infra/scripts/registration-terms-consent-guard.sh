#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APP="$ROOT/apps/web/src/app/App.tsx"
TYPES="$ROOT/apps/web/src/modules/auth/types.ts"
MODAL="$ROOT/apps/web/src/modules/auth/TermsOfUseModal.tsx"
CSS="$ROOT/apps/web/src/modules/auth/terms-of-use.css"
AUTH_ENDPOINTS="$ROOT/apps/api/src/CidadeEmDia.Api/Endpoints/AuthEndpoints.cs"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for file in "$APP" "$TYPES" "$MODAL" "$CSS" "$AUTH_ENDPOINTS"; do
  test -f "$file" || fail "arquivo de consentimento ausente: $file"
done

grep -Fq "import { TermsOfUseModal }" "$APP" || fail "modal de Termos de Uso não está integrado ao cadastro"
grep -Fq 'const [termsAccepted, setTermsAccepted] = useState(false);' "$APP" || fail "estado de aceite não existe"
grep -Fq 'id="registration-terms"' "$APP" || fail "checkbox de aceite ausente"
grep -Fq 'type="checkbox"' "$APP" || fail "controle de aceite não é checkbox"
grep -Fq 'required' "$APP" || fail "aceite obrigatório não está marcado como required"
grep -Fq 'Li e aceito os' "$APP" || fail "texto do aceite ausente"
grep -Fq 'Termos de Uso' "$APP" || fail "ação para leitura dos termos ausente"
grep -Fq 'setTermsOpen(true)' "$APP" || fail "Termos de Uso não abrem em modal"
grep -Fq 'register({ email, password, displayName, termsAccepted })' "$APP" || fail "aceite não é enviado ao backend"
grep -Fq "mode === 'register' && !termsAccepted" "$APP" || fail "botão de cadastro não depende do aceite"
grep -Fq '<TermsOfUseModal open={termsOpen}' "$APP" || fail "modal não está renderizado"

grep -Fq 'termsAccepted: boolean;' "$TYPES" || fail "payload frontend não declara termsAccepted"

grep -Fq 'role="dialog"' "$MODAL" || fail "modal não possui semântica de diálogo"
grep -Fq 'aria-modal="true"' "$MODAL" || fail "modal não possui aria-modal"
grep -Fq "event.key === 'Escape'" "$MODAL" || fail "modal não fecha com Escape"
grep -Fq 'Termos de Uso e Condições de Navegação' "$MODAL" || fail "título dos termos ausente"
grep -Fq 'Sem Vínculo Governamental' "$MODAL" || fail "natureza privada da plataforma ausente"
grep -Fq 'Privacidade e Compartilhamento de Dados (LGPD)' "$MODAL" || fail "cláusula LGPD ausente"
grep -Fq 'Marco Civil da Internet' "$MODAL" || fail "cláusula Marco Civil ausente"
grep -Fq "import './terms-of-use.css';" "$MODAL" || fail "estilos do modal não estão importados"

grep -Fq '.auth-terms-consent' "$CSS" || fail "estilo do checkbox ausente"
grep -Fq '.terms-modal__dialog' "$CSS" || fail "estilo do diálogo ausente"

grep -Fq 'bool TermsAccepted' "$AUTH_ENDPOINTS" || fail "API não recebe o aceite"
grep -Fq 'if (!request.TermsAccepted)' "$AUTH_ENDPOINTS" || fail "API não valida aceite obrigatório"
grep -Fq 'terms_not_accepted' "$AUTH_ENDPOINTS" || fail "API não retorna erro específico para ausência de aceite"

echo 'registration_terms_checkbox=OK'
echo 'registration_terms_modal=OK'
echo 'registration_terms_api_enforcement=OK'
echo 'REGISTRATION TERMS CONSENT GUARD: OK'
