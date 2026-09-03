export const PROFILE_AVATAR_CHANGED_EVENT = 'cidademdia:profile-avatar-changed';

export function notifyProfileAvatarChanged() {
  window.dispatchEvent(new Event(PROFILE_AVATAR_CHANGED_EVENT));
}
