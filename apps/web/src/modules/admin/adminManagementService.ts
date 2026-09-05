import { api } from '../../services/api';
import type { AdminPlan } from './types';

export interface UpdateAdminPlanInput {
  priceCents: number;
  signupFeeCents: number;
  subaccountLimit: number;
  monthlyPublicationLimit: number;
  reason: string;
}

export interface AdminPlanVersionChange {
  plan: AdminPlan;
  changed: boolean;
}

export async function updateAdminPlan(
  planVersionId: string,
  input: UpdateAdminPlanInput,
): Promise<AdminPlanVersionChange> {
  const { data } = await api.put<AdminPlanVersionChange>(
    `/admin/plans/${planVersionId}`,
    input,
  );

  return data;
}
