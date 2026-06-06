import { apiFetch } from './client'
import type { Resource, UpsertResourceRequest } from '../types/resource'

export const resourcesApi = {
  list: () => apiFetch<Resource[]>('/api/v1/resources'),
  create: (data: UpsertResourceRequest) =>
    apiFetch<Resource>('/api/v1/resources', { method: 'POST', body: JSON.stringify(data) }),
  update: (id: string, data: UpsertResourceRequest) =>
    apiFetch<void>(`/api/v1/resources/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  remove: (id: string) =>
    apiFetch<void>(`/api/v1/resources/${id}`, { method: 'DELETE' }),
}
