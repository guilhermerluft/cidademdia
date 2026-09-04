import { api } from '../../services/api';
import type {
  CreateOccurrencePayload,
  EligibleMaster,
  OccurrenceCategory,
  OccurrenceDetails,
  OccurrenceGeoFilters,
  OccurrenceMediaItem,
  OccurrenceMediaPresentation,
  OccurrenceMediaReadUrl,
  OccurrenceMediaUpload,
  OccurrencePage,
} from './types';

export async function listOccurrenceCategories() {
  const { data } = await api.get<OccurrenceCategory[]>('/occurrences/categories');
  return data;
}

export async function listEligibleMasters() {
  const { data } = await api.get<EligibleMaster[]>('/occurrences/masters');
  return data;
}

export async function listMyOccurrences(page = 1, pageSize = 10) {
  const { data } = await api.get<OccurrencePage>('/occurrences', {
    params: { page, pageSize },
  });
  return data;
}

export async function searchMyOccurrences(
  filters: OccurrenceGeoFilters,
  page = 1,
  pageSize = 10,
) {
  const { data } = await api.get<OccurrencePage>('/occurrences/geo-search', {
    params: {
      ...filters,
      page,
      pageSize,
    },
  });
  return data;
}

export async function listOccurrenceMedia(occurrenceId: string) {
  const { data } = await api.get<OccurrenceMediaItem[]>(`/occurrences/${occurrenceId}/media`);
  return data;
}

export async function getOccurrenceMediaReadUrl(mediaId: string) {
  const { data } = await api.get<OccurrenceMediaReadUrl>(`/occurrence-media/${mediaId}/read-url`);
  return data;
}

export async function listOccurrenceMediaForPresentation(occurrenceId: string) {
  const media = await listOccurrenceMedia(occurrenceId);
  const readyMedia = media.filter((item) => item.status === 'READY');

  const presented = await Promise.all(readyMedia.map(async (item) => {
    const read = await getOccurrenceMediaReadUrl(item.id);
    return {
      ...item,
      readUrl: read.readUrl,
      readUrlExpiresAt: read.readUrlExpiresAt,
    } satisfies OccurrenceMediaPresentation;
  }));

  return presented;
}

export async function requestOccurrenceMediaUpload(file: File) {
  const { data } = await api.post<OccurrenceMediaUpload>('/occurrence-media/uploads', {
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
  });

  return data;
}

export async function uploadOccurrenceMediaBinary(upload: OccurrenceMediaUpload, file: File) {
  const response = await fetch(upload.uploadUrl, {
    method: 'PUT',
    headers: {
      'Content-Type': file.type,
    },
    body: file,
    credentials: 'omit',
  });

  if (!response.ok) {
    throw new Error(`O armazenamento recusou o upload de ${file.name} (HTTP ${response.status}).`);
  }
}

export async function confirmOccurrenceMedia(mediaId: string) {
  const { data } = await api.post<OccurrenceMediaItem>(`/occurrence-media/${mediaId}/confirm`);
  return data;
}

export async function prepareOccurrenceMedia(file: File) {
  const upload = await requestOccurrenceMediaUpload(file);
  await uploadOccurrenceMediaBinary(upload, file);
  const media = await confirmOccurrenceMedia(upload.id);

  if (media.status !== 'READY') {
    throw new Error(`A mídia ${file.name} não ficou pronta para vinculação.`);
  }

  return media;
}

export async function createOccurrence(payload: CreateOccurrencePayload) {
  const { data } = await api.post<OccurrenceDetails>('/occurrences', payload);
  return data;
}
