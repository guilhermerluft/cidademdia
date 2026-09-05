#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web/src/modules/occurrences"
MAIN="$ROOT/apps/web/src/main.tsx"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

grep -q "'/occurrences/\${occurrenceId}/media'\|\`/occurrences/\${occurrenceId}/media\`" "$WEB/occurrenceService.ts" \
  || grep -q 'occurrences/${occurrenceId}/media' "$WEB/occurrenceService.ts" \
  || fail "service não consulta as mídias vinculadas à ocorrência"

grep -q 'occurrence-media/${mediaId}/read-url' "$WEB/occurrenceService.ts" \
  || fail "service não solicita URL assinada de leitura"

grep -q "item.status === 'READY'" "$WEB/occurrenceService.ts" \
  || fail "apresentação não restringe mídia ao estado READY"

grep -q 'OccurrenceMediaGallery' "$WEB/OccurrenceCenter.tsx" \
  || fail "lista privada de ocorrências não renderiza a galeria"

grep -q 'contentType.startsWith('\''image/'\'')' "$WEB/OccurrenceMediaGallery.tsx" \
  || fail "galeria não renderiza imagens"

grep -q 'contentType.startsWith('\''video/'\'')' "$WEB/OccurrenceMediaGallery.tsx" \
  || fail "galeria não renderiza vídeos"

grep -q 'loading="lazy"' "$WEB/OccurrenceMediaGallery.tsx" \
  || fail "imagens privadas perderam lazy loading"

grep -q 'occurrence-media.css' "$MAIN" \
  || fail "estilos da galeria privada não estão carregados"

echo "occurrence_media_list_endpoint=OK"
echo "occurrence_media_signed_read=OK"
echo "occurrence_media_ready_only=OK"
echo "occurrence_media_images=OK"
echo "occurrence_media_videos=OK"
echo "occurrence_media_private_cards=OK"
echo "OCCURRENCE MEDIA PRESENTATION GUARD: OK"
