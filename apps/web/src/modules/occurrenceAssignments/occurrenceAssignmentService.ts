import { api } from '../../services/api';
import type { PublicOccurrenceMediaItem } from '../home/homeService';

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
  coverMedia?: PublicOccurrenceMediaItem | null;
  assignment: OccurrenceAssignment | null;
}

export interface OccurrenceTargetDecision {
  targetId: string;
  occurrenceId: string;
  masterUserId: string;
  occurrenceStatus: string;
  targetStatus: string;
  rejectionReason?: string | null;
  sentAt: string;
  acceptedAt?: string | null;
  rejectedAt?: string | null;
  closedAt?: string | null;
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

export async function acceptOccurrenceTarget(
  occurrenceId: string,
  targetId: string,
): Promise<OccurrenceTargetDecision> {
  const { data } = await api.post<OccurrenceTargetDecision>(
    `/occurrences/${occurrenceId}/targets/${targetId}/accept`,
  );
  return data;
}

export async function rejectOccurrenceTarget(
  occurrenceId: string,
  targetId: string,
  reason: string,
): Promise<OccurrenceTargetDecision> {
  const { data } = await api.post<OccurrenceTargetDecision>(
    `/occurrences/${occurrenceId}/targets/${targetId}/reject`,
    { reason },
  );
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
