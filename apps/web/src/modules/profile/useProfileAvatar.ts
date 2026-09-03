import { useEffect, useState } from 'react';
import type { AuthenticatedUser } from '../auth/types';
import { getMyProfileAvatar } from './profileService';

const PROFILE_AVATAR_CHANGED_EVENT = 'cidademdia:profile-avatar-changed';

export function notifyProfileAvatarChanged() {
  window.dispatchEvent(new Event(PROFILE_AVATAR_CHANGED_EVENT));
}

export function useProfileAvatar(user?: AuthenticatedUser | null) {
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!user) {
      setAvatarUrl(null);
      return;
    }

    let active = true;
    let refreshTimer: number | null = null;

    function clearRefreshTimer() {
      if (refreshTimer !== null) {
        window.clearTimeout(refreshTimer);
        refreshTimer = null;
      }
    }

    async function refreshAvatar() {
      clearRefreshTimer();
      try {
        const avatar = await getMyProfileAvatar();
        if (!active) return;

        setAvatarUrl(avatar.readUrl);
        const expiresAt = new Date(avatar.readUrlExpiresAt).getTime();
        const refreshIn = Math.max(30_000, expiresAt - Date.now() - 30_000);
        refreshTimer = window.setTimeout(() => void refreshAvatar(), refreshIn);
      } catch {
        if (active) setAvatarUrl(null);
      }
    }

    const handleAvatarChanged = () => void refreshAvatar();
    void refreshAvatar();
    window.addEventListener(PROFILE_AVATAR_CHANGED_EVENT, handleAvatarChanged);

    return () => {
      active = false;
      clearRefreshTimer();
      window.removeEventListener(PROFILE_AVATAR_CHANGED_EVENT, handleAvatarChanged);
    };
  }, [user?.id]);

  return avatarUrl;
}
