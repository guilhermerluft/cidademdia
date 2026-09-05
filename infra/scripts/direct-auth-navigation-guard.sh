#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MAIN="$ROOT/apps/web/src/main.tsx"
MODAL="$ROOT/apps/web/src/components/CommercialSignupModal.tsx"
OCCURRENCES="$ROOT/apps/web/src/modules/occurrences/PublicOccurrencesRoute.tsx"
REPRESENTATIVES="$ROOT/apps/web/src/modules/institutions/RepresentativesRoute.tsx"
PLANS="$ROOT/apps/web/src/modules/plans/PlansRoute.tsx"

fail() {
  echo "DIRECT AUTH NAVIGATION GUARD: FAIL — $1" >&2
  exit 1
}

for file in "$MAIN" "$MODAL" "$OCCURRENCES" "$REPRESENTATIVES" "$PLANS"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -Fq 'BrowserRouter, Route, Routes, useLocation' "$MAIN" || fail "main não observa mudanças de localização"
grep -Fq 'function AppRoute()' "$MAIN" || fail "wrapper de entrada da App ausente"
grep -Fq 'const location = useLocation();' "$MAIN" || fail "AppRoute não lê a localização atual"
grep -Fq '<Route path="*" element={<AppRoute />} />' "$MAIN" || fail "rota wildcard não usa AppRoute"
grep -Fq 'location.key' "$MAIN" || fail "App não remonta em nova intenção de navegação"
grep -Fq 'location.search' "$MAIN" || fail "App não remonta quando muda o auth query param"

grep -Fq "navigate('/?auth=register')" "$MODAL" || fail "modal comercial não abre cadastro diretamente"

for file in "$OCCURRENCES" "$REPRESENTATIVES" "$PLANS"; do
  grep -Fq "navigate('/?auth=login')" "$file" || fail "header público sem navegação direta para login: $file"
  grep -Fq "navigate('/?auth=register')" "$file" || fail "header público sem navegação direta para cadastro: $file"
done

if grep -Eq "onLogin=.*navigate\('/'\)" "$OCCURRENCES" "$REPRESENTATIVES" "$PLANS"; then
  fail "header público ainda contém login apontando apenas para a Home"
fi

if grep -Eq "onRegister=.*navigate\('/'\)" "$OCCURRENCES" "$REPRESENTATIVES" "$PLANS"; then
  fail "header público ainda contém cadastro apontando apenas para a Home"
fi

echo "commercial_modal_direct_register=OK"
echo "public_header_direct_login=OK"
echo "public_header_direct_register=OK"
echo "auth_query_remount=OK"
echo "DIRECT AUTH NAVIGATION GUARD: OK"
