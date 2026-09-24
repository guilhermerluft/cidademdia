#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${CIDADEMDIA_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
POSTS_CSS="$ROOT/apps/web/src/styles/posts.css"
POSTS_PANEL="$ROOT/apps/web/src/modules/posts/PostManagementPanel.tsx"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

test -f "$POSTS_CSS" || fail "posts.css ausente"
test -f "$POSTS_PANEL" || fail "PostManagementPanel.tsx ausente"

grep -Fq ".posts-composer__form > label" "$POSTS_CSS" || fail "labels do composer sem padrão visual"
grep -Fq "min-height: 46px;" "$POSTS_CSS" || fail "inputs do composer sem altura padrão de ocorrências"
grep -Fq "border: 1px solid var(--ced-color-border);" "$POSTS_CSS" || fail "inputs do composer sem borda padrão"
grep -Fq "border-radius: var(--ced-radius-md);" "$POSTS_CSS" || fail "inputs do composer sem raio padrão"
grep -Fq "padding: 0.75rem 0.9rem;" "$POSTS_CSS" || fail "inputs do composer sem padding padrão"
grep -Fq "border-color: var(--ced-color-primary-300);" "$POSTS_CSS" || fail "focus do composer sem borda padrão"
grep -Fq "box-shadow: 0 0 0 3px rgba(25, 98, 169, 0.1);" "$POSTS_CSS" || fail "focus do composer sem ring padrão"
grep -Fq ".posts-composer__form input[type='file']" "$POSTS_CSS" || fail "upload do composer sem tratamento visual"
grep -Fq "background: var(--ced-color-canvas);" "$POSTS_CSS" || fail "estado de upload/disabled sem canvas padrão"

grep -Fq "<select value={type}" "$POSTS_PANEL" || fail "select de formato ausente"
grep -Fq 'placeholder="Título da publicação"' "$POSTS_PANEL" || fail "input de título ausente"
grep -Fq 'placeholder="Escreva o conteúdo da publicação"' "$POSTS_PANEL" || fail "textarea de conteúdo ausente"
grep -Fq 'type="file"' "$POSTS_PANEL" || fail "input de mídia ausente"

echo "posts_form_labels=OK"
echo "posts_form_inputs=OCCURRENCE_VISUAL_STANDARD"
echo "posts_form_focus=OK"
echo "posts_form_file_input=OK"
echo "POSTS FORM VISUAL GUARD: OK"
