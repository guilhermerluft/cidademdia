export interface AuthenticatedUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AuthSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthenticatedUser;
}

export interface LoginInput {
  email: string;
  password: string;
}

export interface RegisterInput extends LoginInput {
  displayName: string;
}
