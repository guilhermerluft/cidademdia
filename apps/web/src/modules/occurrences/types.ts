export interface OccurrenceCategory {
  id: string;
  name: string;
  slug: string;
  displayOrder: number;
}

export interface OccurrenceListItem {
  id: string;
  publicCode: string;
  categoryId: string;
  categoryName: string;
  title: string;
  status: string;
  addressText: string;
  createdAt: string;
  updatedAt: string;
}

export interface OccurrencePage {
  items: OccurrenceListItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface OccurrenceGeoFilters {
  status?: string;
  categoryId?: string;
  city?: string;
  latitude?: number;
  longitude?: number;
  radiusKm?: number;
}

export interface OccurrenceDetails extends OccurrenceListItem {
  description: string | null;
  postalCode: string | null;
  cityId: string | null;
  stateCode: string | null;
  latitude: number;
  longitude: number;
  externalProtocolNumber: string | null;
  externalProtocolAgency: string | null;
  closedAt: string | null;
  cancelledAt: string | null;
  currentServiceForecast: string | null;
}

export interface OccurrenceMediaUpload {
  id: string;
  status: 'PENDING';
  contentType: string;
  expectedSizeBytes: number;
  uploadUrl: string;
  uploadUrlExpiresAt: string;
}

export interface OccurrenceMediaItem {
  id: string;
  occurrenceId: string | null;
  status: 'PENDING' | 'READY';
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
  readyAt: string | null;
  attachedAt: string | null;
}

export interface OccurrenceMediaReadUrl {
  id: string;
  readUrl: string;
  readUrlExpiresAt: string;
}

export interface OccurrenceMediaPresentation extends OccurrenceMediaItem {
  readUrl: string;
  readUrlExpiresAt: string;
}

export interface CreateOccurrencePayload {
  categoryId: string;
  title: string;
  description: string | null;
  addressText: string;
  latitude: number;
  longitude: number;
  postalCode: string | null;
  cityId: null;
  stateCode: string | null;
  externalProtocolNumber: string | null;
  externalProtocolAgency: string | null;
  mediaIds: string[];
}
