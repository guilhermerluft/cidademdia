export type SubaccountPermissionKey =
  | 'occurrence.read.targeted'
  | 'occurrence.status.change'
  | 'chat.read'
  | 'chat.message.send'
  | 'chat.audio.send';

export interface SubaccountPermissionOption {
  key: SubaccountPermissionKey;
  label: string;
  description: string;
}

export const SUBACCOUNT_PERMISSION_OPTIONS: readonly SubaccountPermissionOption[] = [
  {
    key: 'occurrence.read.targeted',
    label: 'Visualizar ocorrências',
    description: 'Permite consultar ocorrências direcionadas à conta Master.',
  },
  {
    key: 'occurrence.status.change',
    label: 'Alterar status',
    description: 'Permite alterar o status das ocorrências quando a transição for autorizada.',
  },
  {
    key: 'chat.read',
    label: 'Ler conversas',
    description: 'Permite acessar chats vinculados às ocorrências autorizadas.',
  },
  {
    key: 'chat.message.send',
    label: 'Enviar mensagens',
    description: 'Permite responder por texto nos chats autorizados.',
  },
  {
    key: 'chat.audio.send',
    label: 'Enviar áudios',
    description: 'Permite enviar áudio nos chats autorizados.',
  },
] as const;

export interface MasterSubaccountMember {
  linkId: string;
  userId: string;
  email: string;
  displayName: string;
  status: string;
  permissions: string[];
}

export interface MasterSubaccountInvitation {
  invitationId: string;
  email: string;
  permissions: string[];
  expiresAt: string;
}

export interface MasterSubaccountTeam {
  limit: number | null;
  activeCount: number;
  pendingInvitationCount: number;
  members: MasterSubaccountMember[];
  invitations: MasterSubaccountInvitation[];
}

export interface SubaccountContext {
  linkId: string;
  masterUserId: string;
  permissions: string[];
}

export interface CreateSubaccountInput {
  email: string;
  permissions: string[];
}

export interface UpdateSubaccountPermissionsInput {
  permissions: string[];
}

export interface SubaccountInvitationPreview {
  email: string;
  masterDisplayName: string;
  permissions: string[];
  expiresAt: string;
}

export interface AcceptSubaccountInvitationInput {
  token: string;
  password: string;
  displayName: string;
}
