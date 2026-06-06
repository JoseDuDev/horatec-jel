import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AdminUser, TokenPair } from '@/lib/types/auth'

interface AuthState {
  user: AdminUser | null
  accessToken: string | null
  refreshToken: string | null
  tenantSlug: string | null
  setAuth: (user: AdminUser, tokens: TokenPair, tenantSlug: string) => void
  clearAuth: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      tenantSlug: null,
      setAuth: (user, tokens, tenantSlug) =>
        set({ user, accessToken: tokens.accessToken, refreshToken: tokens.refreshToken, tenantSlug }),
      clearAuth: () =>
        set({ user: null, accessToken: null, refreshToken: null, tenantSlug: null }),
    }),
    { name: 'horafy-auth' }
  )
)
