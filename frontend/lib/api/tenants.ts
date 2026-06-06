import { apiFetch } from './client'
import type { Tenant, UpdateTenantRequest } from '../types/tenant'

export const tenantsApi = {
  me: () => apiFetch<Tenant>('/api/v1/tenants/me'),
  update: (data: UpdateTenantRequest) =>
    apiFetch<void>('/api/v1/tenants/me', { method: 'PUT', body: JSON.stringify(data) }),
  updateTheme: (primaryColor: string, logoUrl?: string) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify({ primaryColor, logoUrl }),
    }),
}
