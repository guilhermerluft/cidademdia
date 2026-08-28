import { api } from '../../services/api';
import type {
  AcceptSubaccountInvitationInput,
  CreateSubaccountInput,
  MasterSubaccountInvitation,
  MasterSubaccountMember,
  MasterSubaccountTeam,
  SubaccountContext,
  SubaccountInvitationPreview,
  UpdateSubaccountPermissionsInput,
} from './types';

export async function listMasterSubaccounts(): Promise<MasterSubaccountTeam> {
  const { data } = await api.get<MasterSubaccountTeam>('/master/subaccounts');
  return data;
}

export async function createMasterSubaccount(input: CreateSubaccountInput): Promise<MasterSubaccountMember> {
  const { data } = await api.post<MasterSubaccountMember>('/master/subaccounts', input);
  return data;
}

export async function inviteMasterSubaccount(input: CreateSubaccountInput): Promise<MasterSubaccountInvitation> {
  const { data } = await api.post<MasterSubaccountInvitation>('/master/subaccounts/invitations', input);
  return data;
}

export async function previewSubaccountInvitation(token: string): Promise<SubaccountInvitationPreview> {
  const { data } = await api.get<SubaccountInvitationPreview>('/subaccount-invitations/preview', {
    params: { token },
  });
  return data;
}

export async function acceptSubaccountInvitation(input: AcceptSubaccountInvitationInput): Promise<void> {
  await api.post('/subaccount-invitations/accept', input);
}

export async function listSubaccountContexts(): Promise<SubaccountContext[]> {
  const { data } = await api.get<SubaccountContext[]>('/subaccount/contexts');
  return data;
}

export async function updateMasterSubaccountPermissions(
  linkId: string,
  input: UpdateSubaccountPermissionsInput,
): Promise<MasterSubaccountMember> {
  const { data } = await api.put<MasterSubaccountMember>(`/master/subaccounts/${linkId}/permissions`, input);
  return data;
}

export async function revokeMasterSubaccount(linkId: string): Promise<void> {
  await api.delete(`/master/subaccounts/${linkId}`);
}
