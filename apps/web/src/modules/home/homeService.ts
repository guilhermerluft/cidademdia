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
  page?: number;
  pageSize?: number;
}

export interface PublicOccurrencePage {
  items: PublicOccurrenceItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface PublicPlanOffer {
  offerId: string;
  planVersionId: string;
  planKey: string;
  planName: string;
  categoryKey: string;
  categoryName: string;
  billingIntervalMonths: number;
  priceCents: number;
  signupFeeCents: number;
  marketingReferencePriceCents?: number | null;
  subaccountLimit: number;
  monthlyPublicationLimit: number;
  version: number;
}

export async function searchPublicOccurrences(query: PublicOccurrenceQuery) {
  const { data } = await api.get<PublicOccurrencePage>('/public/occurrences', {
    params: query,
  });

  return data;
}

export async function listPublicOccurrences(query: PublicOccurrenceQuery) {
  return (await searchPublicOccurrences(query)).items;
}

export async function listPublicPlans() {
  const { data } = await api.get<PublicPlanOffer[]>('/billing/catalog');
  return data;
}
