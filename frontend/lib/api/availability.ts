import { apiFetch } from './client'
import type {
  BusinessHoursDto,
  AvailabilityRuleDto,
  AvailabilityExceptionDto,
  SetResourceRuleRequest,
  SetResourceExceptionRequest,
} from '../types/availability'

export const availabilityApi = {
  getBusinessHours: () =>
    apiFetch<BusinessHoursDto[]>('/api/v1/availability/business-hours'),

  setBusinessHours: (
    dayOfWeek: number,
    isOpen: boolean,
    openTime: string,
    closeTime: string
  ) =>
    apiFetch<void>('/api/v1/availability/business-hours', {
      method: 'PUT',
      body: JSON.stringify({ dayOfWeek, isOpen, openTime, closeTime }),
    }),

  getResourceRules: (resourceId: string) =>
    apiFetch<AvailabilityRuleDto[]>(
      `/api/v1/availability/resources/${resourceId}/rules`
    ),

  setResourceRule: (resourceId: string, data: SetResourceRuleRequest) =>
    apiFetch<void>(`/api/v1/availability/resources/${resourceId}/rules`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  getResourceExceptions: (resourceId: string, from: string, to: string) =>
    apiFetch<AvailabilityExceptionDto[]>(
      `/api/v1/availability/resources/${resourceId}/exceptions?from=${from}&to=${to}`
    ),

  setResourceException: (resourceId: string, data: SetResourceExceptionRequest) =>
    apiFetch<void>(`/api/v1/availability/resources/${resourceId}/exceptions`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  deleteResourceException: (resourceId: string, date: string) =>
    apiFetch<void>(
      `/api/v1/availability/resources/${resourceId}/exceptions/${date}`,
      { method: 'DELETE' }
    ),
}
