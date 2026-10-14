import { useQueryClient } from '@tanstack/react-query';
import { createContext, useCallback, useContext, useMemo, useSyncExternalStore, type ReactNode } from 'react';
import { api } from '../api/client';
import type { RegisteredUser, TokenResponse } from '../api/types';
import { clearTokens, currentSession, getTokens, setTokens, subscribe, type Session } from './session';

type AuthApi = {
  session: Session | null;
  login: (email: string, password: string) => Promise<void>;
  register: (input: { email: string; password: string; fullName: string }) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthApi | null>(null);

// The decoded session is cached per access token so useSyncExternalStore sees a stable value.
let cachedToken: string | undefined;
let cachedSession: Session | null = null;
function snapshot(): Session | null {
  const token = getTokens()?.accessToken;
  if (token !== cachedToken) {
    cachedToken = token;
    cachedSession = currentSession();
  }
  return cachedSession;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const session = useSyncExternalStore(subscribe, snapshot, snapshot);
  const queryClient = useQueryClient();

  const login = useCallback(async (email: string, password: string) => {
    const tokens = await api<TokenResponse>('/auth/login', { method: 'POST', json: { email, password }, anonymous: true });
    queryClient.clear();
    setTokens({ accessToken: tokens.accessToken, refreshToken: tokens.refreshToken });
  }, [queryClient]);

  const register = useCallback(async (input: { email: string; password: string; fullName: string }) => {
    await api<RegisteredUser>('/auth/register', { method: 'POST', json: input, anonymous: true });
    await login(input.email, input.password);
  }, [login]);

  const logout = useCallback(async () => {
    const refreshToken = getTokens()?.refreshToken;
    clearTokens();
    queryClient.clear();
    if (refreshToken) {
      await api('/auth/logout', { method: 'POST', json: { refreshToken }, anonymous: true }).catch(() => undefined);
    }
  }, [queryClient]);

  const value = useMemo(() => ({ session, login, register, logout }), [session, login, register, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthApi {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error('useAuth must be used inside AuthProvider');
  return auth;
}
