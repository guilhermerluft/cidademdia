import { api } from '../../services/api';

export interface PrivateUserProfile {
  userId: string;
  email: string;
  displayName: string;
  document?: string | null;
  phone?: string | null;
  avatarMediaId?: string | null;
  roles: string[];
}

export async function getMyProfile() {
  const { data } = await api.get<PrivateUserProfile>('/profile');
  return data;
}
