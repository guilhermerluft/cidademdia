import { api } from '../../services/api';
import type {
  AdminAuditLog,
  AdminBillingSnapshot,
  AdminInstitution,
  AdminOccurrence,
  AdminOverview,
  AdminPage,
  AdminPost,
  AdminUser,
} from './types';

export async function getAdminOverview(): Promise<AdminOverview> {
  const { data } = await api.get<AdminOverview>('/admin/overview');
  return data;
}

export async function listAdminUsers(params?: {
  search?: string;
  status?: string;
  role?: string;
  page?: number;
  pageSize?: number;
}): Promise<AdminPage<AdminUser>> {
  const { data } = await api.get<AdminPage<AdminUser>>('/admin/users', { params });
  return data;
}

export async function changeAdminUserStatus(
  userId: string,
  status: 'Active' | 'Suspended' | 'Blocked',
  reason: string,
): Promise<{ user: AdminUser; changed: boolean }> {
  const { data } = await api.post<{ user: AdminUser; changed: boolean }>(
    `/admin/users/${userId}/status`,
    { status, reason },
  );
  return data;
}

export async function listAdminInstitutions(params?: {
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<AdminPage<AdminInstitution>> {
  const { data } = await api.get<AdminPage<AdminInstitution>>('/admin/institutions', { params });
  return data;
}

export async function listAdminOccurrences(params?: {
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<AdminPage<AdminOccurrence>> {
  const { data } = await api.get<AdminPage<AdminOccurrence>>('/admin/occurrences', { params });
  return data;
}

export async function listAdminPosts(params?: {
  search?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<AdminPage<AdminPost>> {
  const { data } = await api.get<AdminPage<AdminPost>>('/admin/posts', { params });
  return data;
}

export async function getAdminBilling(page = 1, pageSize = 20): Promise<AdminBillingSnapshot> {
  const { data } = await api.get<AdminBillingSnapshot>('/admin/billing', {
    params: { page, pageSize },
  });
  return data;
}

export async function listAdminAuditLogs(page = 1, pageSize = 20): Promise<AdminPage<AdminAuditLog>> {
  const { data } = await api.get<AdminPage<AdminAuditLog>>('/admin/audit-logs', {
    params: { page, pageSize },
  });
  return data;
}
