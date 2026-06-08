import { apiFetch } from './client'
import type {
  Tenant, UpdateTenantRequest,
  UpdateLoyaltySettingsRequest, UpdateCancellationPolicyRequest,
} from '../types/tenant'

export const tenantsApi = {
  me: () => apiFetch<Tenant>('/api/v1/tenants/me'),
  update: (data: UpdateTenantRequest) =>
    apiFetch<void>('/api/v1/tenants/me', { method: 'PUT', body: JSON.stringify(data) }),
  updateTheme: (primaryColor: string, logoUrl?: string) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify({ primaryColor, logoUrl }),
    }),
  updateLoyaltySettings: (data: UpdateLoyaltySettingsRequest) =>
    apiFetch<void>('/api/v1/tenants/loyalty-settings', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  updateCancellationPolicy: (data: UpdateCancellationPolicyRequest) =>
    apiFetch<void>('/api/v1/tenants/cancellation-policy', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  completeOnboarding: () =>
    apiFetch<void>('/api/v1/tenants/me/onboarding-complete', { method: 'POST' }),
}
