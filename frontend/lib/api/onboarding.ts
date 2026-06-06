import { apiFetch } from './client'

export interface OnboardingTenantData {
  name: string
  timezone: string
}

export interface OnboardingThemeData {
  primaryColor: string
  logoUrl?: string
}

export interface OnboardingServiceData {
  name: string
  description?: string
  durationMinutes: number
  price: number
}

export interface OnboardingResourceData {
  name: string
  type: string
}

export interface OnboardingHoursData {
  schedule: Array<{
    dayOfWeek: number
    isOpen: boolean
    openTime: string
    closeTime: string
  }>
}

export const onboardingApi = {
  updateTenant: (data: OnboardingTenantData) =>
    apiFetch<void>('/api/v1/tenants/me', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  updateTheme: (data: OnboardingThemeData) =>
    apiFetch<void>('/api/v1/tenants/me/theme', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  createService: (data: OnboardingServiceData) =>
    apiFetch<{ id: string }>('/api/v1/services', {
      method: 'POST',
      body: JSON.stringify({ ...data, isActive: true }),
    }),

  createResource: (data: OnboardingResourceData) =>
    apiFetch<{ id: string }>('/api/v1/resources', {
      method: 'POST',
      body: JSON.stringify({ ...data, serviceIds: [] }),
    }),

  setBusinessHours: (dayOfWeek: number, isOpen: boolean, openTime: string, closeTime: string) =>
    apiFetch<void>('/api/v1/availability/business-hours', {
      method: 'PUT',
      body: JSON.stringify({ dayOfWeek, isOpen, openTime, closeTime }),
    }),
}
