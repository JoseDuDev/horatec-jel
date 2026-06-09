import { apiFetch } from './client'
import type { Resource, UpsertResourceRequest } from '../types/resource'

export const resourcesApi = {
  list: () => apiFetch<Resource[]>('/api/v1/resources'),
  create: (data: UpsertResourceRequest) =>
    apiFetch<string>('/api/v1/resources', {
      method: 'POST',
      body: JSON.stringify({ name: data.name, type: data.type }),
    }),
  update: (id: string, data: UpsertResourceRequest) =>
    apiFetch<void>(`/api/v1/resources/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ name: data.name, type: data.type }),
    }),
  remove: (id: string) =>
    apiFetch<void>(`/api/v1/resources/${id}`, { method: 'DELETE' }),
  addService: (resourceId: string, serviceId: string) =>
    apiFetch<void>(`/api/v1/availability/resources/${resourceId}/services/${serviceId}`, {
      method: 'POST',
    }),
  removeService: (resourceId: string, serviceId: string) =>
    apiFetch<void>(`/api/v1/availability/resources/${resourceId}/services/${serviceId}`, {
      method: 'DELETE',
    }),
}
