import { api } from '../../services/api';

export interface PublicOccurrenceItem {
  id: string;
  publicCode: string;
  categoryName: string;
  categorySlug: string;
  title: string;
  description?: string | null;
  status: string;
  addressText: string;
  createdAt: string;
  updatedAt: string;
}

export interface PublicOccurrenceQuery {
  city?: string;
  latitude?: number;
  longitude?: number;
  radiusKm?: number;
  limit?: number;
}

export async function listPublicOccurrences(query: PublicOccurrenceQuery) {
  const { data } = await api.get<{ items: PublicOccurrenceItem[] }>('/public/occurrences', {
    params: query,
  });

  return data.items;
}
