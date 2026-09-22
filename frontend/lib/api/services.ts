import { apiFetch, apiUpload } from './client'
import type { Service, UpsertServiceRequest } from '../types/service'

export const servicesApi = {
  list: () => apiFetch<Service[]>('/api/v1/services'),
  create: (data: UpsertServiceRequest) =>
    apiFetch<string>('/api/v1/services', { method: 'POST', body: JSON.stringify(data) }),
  update: (id: string, data: UpsertServiceRequest) =>
    apiFetch<void>(`/api/v1/services/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  remove: (id: string) =>
    apiFetch<void>(`/api/v1/services/${id}`, { method: 'DELETE' }),

  // ── Foto ───────────────────────────────────────────────────────────────────

  setImage: (id: string, file: File) =>
    apiUpload<{ url: string }>(`/api/v1/services/${id}/image`, file),

  removeImage: (id: string) =>
    apiFetch<void>(`/api/v1/services/${id}/image`, { method: 'DELETE' }),
}
