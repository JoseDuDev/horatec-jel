import { apiFetch } from './client'
import type { TokenPair, AdminUser } from '../types/auth'

export const authApi = {
  login: (email: string, password: string) =>
    apiFetch<TokenPair>('/api/v1/auth/email', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  me: () => apiFetch<AdminUser>('/api/v1/auth/me'),

  refresh: (refreshToken: string) =>
    apiFetch<TokenPair>('/api/v1/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ refreshToken }),
    }),
}
