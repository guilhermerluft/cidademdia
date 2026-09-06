#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MAIN="$ROOT/apps/web/src/main.tsx"
PAGE="$ROOT/apps/web/src/modules/howItWorks/HowItWorksPage.tsx"
ROUTE="$ROOT/apps/web/src/modules/howItWorks/HowItWorksRoute.tsx"
CSS="$ROOT/apps/web/src/modules/howItWorks/how-it-works-page.css"
GUIDE="$ROOT/apps/web/src/modules/howItWorks/assets/guideImage.ts"
MODAL="$ROOT/apps/web/src/modules/home/HowItWorksModal.tsx"
HOME="$ROOT/apps/web/src/modules/home/PublicHome.tsx"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for file in "$MAIN" "$PAGE" "$ROUTE" "$CSS" "$GUIDE" "$MODAL" "$HOME"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -q 'path="/como-funciona"' "$MAIN" || fail "rota /como-funciona ausente"
echo "how_it_works_public_route=OK"

grep -q "HOW_IT_WORKS_VIDEO_URL = '/media/como-funciona.mp4'" "$PAGE" || fail "página não reutiliza vídeo Como funciona"
grep -q "HOW_IT_WORKS_VIDEO_URL = '/media/como-funciona.mp4'" "$MODAL" || fail "vídeo existente do modal foi alterado"
echo "how_it_works_existing_video_reused=OK"

VIDEO_RULE="$(sed -n '/^\.how-it-works-page__video-frame video {/,/^}/p' "$CSS")"
grep -q 'aspect-ratio: 16 / 9;' <<<"$VIDEO_RULE" || fail "elemento de vídeo da página não está em 16:9"
grep -q 'height: auto;' <<<"$VIDEO_RULE" || fail "elemento de vídeo da página não preserva altura 16:9"
echo "how_it_works_video_ratio=OK"

grep -q 'HOW_IT_WORKS_GUIDE_IMAGE' "$GUIDE" || fail "imagem do guia não foi montada"
grep -q 'HOW_IT_WORKS_GUIDE_IMAGE' "$PAGE" || fail "página não exibe imagem do guia"
grep -q 'Infográfico Como registrar sua ocorrência' "$PAGE" || fail "guia sem descrição acessível"
echo "how_it_works_guide_image=OK"

for label in 'Ver os dois' 'Assistir vídeo' 'Passo a passo'; do
  grep -q "$label" "$PAGE" || fail "seletor ausente: $label"
done
grep -q 'aria-pressed=' "$PAGE" || fail "seletor de conteúdo sem estado acessível"
echo "how_it_works_view_switch=OK"

grep -q '>Ampliar<' "$PAGE" || grep -q 'Ampliar' "$PAGE" || fail "ação para ampliar o guia ausente"
grep -q 'how-it-works-page__lightbox' "$PAGE" || fail "lightbox do guia ausente"
echo "how_it_works_guide_lightbox=OK"

grep -q "navigate('/?auth=login')" "$ROUTE" || fail "login direto ausente na rota pública"
grep -q "navigate('/?auth=register')" "$ROUTE" || fail "cadastro direto ausente na rota pública"
echo "how_it_works_direct_auth=OK"

grep -q 'Ver guia completo' "$MODAL" || fail "modal existente não oferece acesso ao guia completo"
grep -q "navigate('/como-funciona')" "$MODAL" || fail "modal não aponta para a nova página"
echo "how_it_works_existing_modal_preserved=OK"

grep -q 'setHowItWorksOpen(true)' "$HOME" || fail "CTA existente da Home deixou de abrir o modal"
for legacy_copy in 'Registre' 'Compartilhe' 'Acompanhe'; do
  grep -q "$legacy_copy" "$HOME" || fail "seção existente da Home perdeu conteúdo: $legacy_copy"
done
echo "how_it_works_existing_home_preserved=OK"

echo "HOW IT WORKS PAGE GUARD: OK"
