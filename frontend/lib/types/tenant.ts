export interface Tenant {
  id: string
  name: string
  slug: string
  logoUrl?: string
  primaryColor?: string
  customDomain?: string
  timezone: string
  plan: string
}

export interface UpdateTenantRequest {
  name?: string
  logoUrl?: string
  primaryColor?: string
  timezone?: string
}
