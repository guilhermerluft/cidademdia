import { api } from '../../services/api';
import { notifyProfileAvatarChanged } from './profileEvents';

export interface PrivateUserProfile {
  userId: string;
  email: string;
  displayName: string;
  document?: string | null;
  phone?: string | null;
  avatarMediaId?: string | null;
  roles: string[];
}

export interface ProfileAvatarUpload {
  avatarMediaId: string;
  contentType: string;
  uploadUrl: string;
  uploadUrlExpiresAt: string;
}

export interface ProfileAvatarRead {
  avatarMediaId: string;
  readUrl: string;
  readUrlExpiresAt: string;
}

export interface ProfileAvatarConfirmation {
  profile: PrivateUserProfile;
  avatar: ProfileAvatarRead;
}

export async function getMyProfile() {
  const { data } = await api.get<PrivateUserProfile>('/profile');
  return data;
}

export async function updateMyProfile(input: {
  displayName: string;
  document?: string | null;
  phone?: string | null;
}) {
  const { data } = await api.put<PrivateUserProfile>('/profile', input);
  return data;
}

export async function requestProfileAvatarUpload(file: File) {
  const { data } = await api.post<ProfileAvatarUpload>('/profile/avatar/upload', {
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
  });
  return data;
}

export async function uploadProfileAvatarBinary(upload: ProfileAvatarUpload, file: File) {
  const response = await fetch(upload.uploadUrl, {
    method: 'PUT',
    headers: {
      'Content-Type': upload.contentType,
    },
    body: file,
    credentials: 'omit',
  });

  if (!response.ok) {
    throw new Error(`O armazenamento recusou a foto de perfil (HTTP ${response.status}).`);
  }
}

export async function confirmProfileAvatar(upload: ProfileAvatarUpload) {
  const { data } = await api.post<ProfileAvatarConfirmation>('/profile/avatar/confirm', {
    avatarMediaId: upload.avatarMediaId,
    contentType: upload.contentType,
  });
  return data;
}

export async function prepareProfileAvatar(file: File) {
  const upload = await requestProfileAvatarUpload(file);
  await uploadProfileAvatarBinary(upload, file);
  const confirmation = await confirmProfileAvatar(upload);
  notifyProfileAvatarChanged();
  return confirmation;
}

export async function getMyProfileAvatar() {
  const { data } = await api.get<ProfileAvatarRead>('/profile/avatar');
  return data;
}

export async function removeMyProfileAvatar() {
  const { data } = await api.delete<PrivateUserProfile>('/profile/avatar');
  notifyProfileAvatarChanged();
  return data;
}
