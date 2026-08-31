import { api } from '../../services/api';

export interface OccurrenceAssignment {
  assignmentId: string;
  targetId: string;
  occurrenceId: string;
  masterUserId: string;
  masterSubaccountId: string;
  subaccountUserId: string;
  subaccountDisplayName: string;
  assignedAt: string;
}

export interface MasterOccurrenceTarget {
  targetId: string;
  occurrenceId: string;
  publicCode: string;
  title: string;
  addressText: string;
  occurrenceStatus: string;
  targetStatus: string;
  updatedAt: string;
  assignment: OccurrenceAssignment | null;
}

export interface AssignedOccurrence {
  assignmentId: string;
  targetId: string;
  occurrenceId: string;
  masterUserId: string;
  masterSubaccountId: string;
  publicCode: string;
  title: string;
  addressText: string;
  occurrenceStatus: string;
  targetStatus: string;
  canChangeStatus: boolean;
  assignedAt: string;
  updatedAt: string;
}

export async function listMasterOccurrenceTargets(): Promise<MasterOccurrenceTarget[]> {
  const { data } = await api.get<MasterOccurrenceTarget[]>('/master/occurrence-targets');
  return data;
}

export async function assignOccurrenceTarget(targetId: string, masterSubaccountId: string): Promise<OccurrenceAssignment> {
  const { data } = await api.put<OccurrenceAssignment>(`/master/occurrence-targets/${targetId}/assignment`, {
    masterSubaccountId,
  });
  return data;
}

export async function unassignOccurrenceTarget(targetId: string): Promise<void> {
  await api.delete(`/master/occurrence-targets/${targetId}/assignment`);
}

export async function listAssignedOccurrences(): Promise<AssignedOccurrence[]> {
  const { data } = await api.get<AssignedOccurrence[]>('/subaccount/occurrence-assignments');
  return data;
}

export async function changeAssignedOccurrenceStatus(occurrenceId: string, status: string): Promise<void> {
  await api.post(`/occurrences/${occurrenceId}/status`, { status, reason: null });
}
