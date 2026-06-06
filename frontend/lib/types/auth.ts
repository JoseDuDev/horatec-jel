export interface TokenPair {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface AdminUser {
  id: string
  name: string
  email: string
  role: 'TenantOwner' | 'TenantAdmin'
  avatarUrl?: string
}
