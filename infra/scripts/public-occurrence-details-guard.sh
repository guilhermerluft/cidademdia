#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

CONTRACTS="$ROOT/apps/api/src/CidadeEmDia.Application/Occurrences/IOccurrenceGeoSearchService.cs"
SERVICE="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/OccurrenceGeoSearchService.cs"
ENDPOINTS="$ROOT/apps/api/src/CidadeEmDia.Api/Endpoints/OccurrenceGeoEndpoints.cs"
HOME_SERVICE="$ROOT/apps/web/src/modules/home/homeService.ts"
HOME="$ROOT/apps/web/src/modules/home/PublicHome.tsx"
CARD="$ROOT/apps/web/src/modules/occurrences/PublicOccurrenceCard.tsx"
LIST="$ROOT/apps/web/src/modules/occurrences/PublicOccurrences.tsx"
MODAL="$ROOT/apps/web/src/modules/occurrences/PublicOccurrenceDetailsModal.tsx"
CSS="$ROOT/apps/web/src/modules/occurrences/public-occurrences.css"

for file in "$CONTRACTS" "$SERVICE" "$ENDPOINTS" "$HOME_SERVICE" "$HOME" "$CARD" "$LIST" "$MODAL" "$CSS"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -q 'PublicOccurrenceMediaItem' "$CONTRACTS" || fail "contrato público de mídia ausente"
grep -q 'CoverMedia' "$CONTRACTS" || fail "capa pública ausente do contrato"
grep -q 'GetPublicDetailsAsync' "$CONTRACTS" || fail "contrato de detalhe público ausente"
grep -q 'ContentType.StartsWith("image/")' "$SERVICE" || fail "capa não está limitada a imagem"
grep -q 'media.Status == OccurrenceMediaStatus.Ready' "$SERVICE" || fail "mídia pública não está limitada a READY"
grep -q 'OrderBy(media => media.CreatedAt)' "$SERVICE" || fail "ordem original de upload não está preservada"
grep -q 'CreateReadUrl' "$SERVICE" || fail "mídia pública não usa URL assinada"
grep -q '/public/occurrences/{occurrenceId:guid}' "$ENDPOINTS" || fail "endpoint de detalhe público ausente"
grep -q 'getPublicOccurrenceDetails' "$HOME_SERVICE" || fail "client de detalhe público ausente"
grep -q 'occurrence.coverMedia.readUrl' "$CARD" || fail "card não usa primeira foto como capa"
grep -q 'object-fit: cover' "$CSS" || fail "foto de capa não preenche a área disponível"
grep -q 'role={interactive ? .button.' "$CARD" || fail "card público não está acessível como botão"
grep -q 'PublicOccurrenceDetailsModal' "$LIST" || fail "listagem pública não abre detalhe"
grep -q 'onOpen={openOccurrence}' "$HOME" || fail "listagem da Home não abre detalhe"
grep -q 'getPublicOccurrenceDetails' "$HOME" || fail "Home não carrega detalhe público"
grep -q 'PublicOccurrenceDetailsModal' "$HOME" || fail "Home não reutiliza modal público de ocorrência"
grep -q 'role="dialog"' "$MODAL" || fail "detalhe público não usa dialog acessível"
grep -q 'occurrence.media.map' "$MODAL" || fail "galeria completa não é renderizada"
grep -q '<video controls' "$MODAL" || fail "vídeos não são reproduzíveis no detalhe"
grep -q 'Fechar detalhes da ocorrência' "$MODAL" || fail "modal sem fechamento acessível"

echo "public_occurrence_cover_contract=OK"
echo "public_occurrence_ready_media_only=OK"
echo "public_occurrence_signed_media=OK"
echo "public_occurrence_first_photo_cover=OK"
echo "public_occurrence_clickable_card=OK"
echo "public_occurrence_details_dialog=OK"
echo "public_occurrence_full_gallery=OK"
echo "home_occurrence_details=OK"
echo "PUBLIC OCCURRENCE DETAILS GUARD: OK"
