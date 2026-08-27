import { api } from '../../services/api';
import type {
  CreateSubaccountInput,
  MasterSubaccountMember,
  MasterSubaccountTeam,
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
