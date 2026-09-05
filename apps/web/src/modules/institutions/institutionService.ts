import { api } from '../../services/api';
import type { InstitutionDirectoryPage, MasterDirectoryPage } from './types';

export interface InstitutionDirectoryFilters {
  search?: string;
  type?: string;
  stateCode?: string;
  page?: number;
  pageSize?: number;
}

export interface MasterDirectoryFilters {
  search?: string;
  page?: number;
  pageSize?: number;
}

export async function listInstitutions(filters: InstitutionDirectoryFilters = {}) {
  const { data } = await api.get<InstitutionDirectoryPage>('/institutions', {
    params: {
      search: filters.search || undefined,
      type: filters.type || undefined,
      stateCode: filters.stateCode || undefined,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    },
  });

  return data;
}

export async function listActiveMasters(filters: MasterDirectoryFilters = {}) {
  const { data } = await api.get<MasterDirectoryPage>('/masters', {
    params: {
      search: filters.search || undefined,
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
    },
  });

  return data;
}
