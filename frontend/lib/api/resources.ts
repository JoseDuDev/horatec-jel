import { apiFetch, apiUpload } from './client'
import type { Resource, UpsertResourceRequest } from '../types/resource'

export const resourcesApi = {
  list: () => apiFetch<Resource[]>('/api/v1/resources'),
  create: (data: UpsertResourceRequest) =>
    apiFetch<string>('/api/v1/resources', {
      method: 'POST',
      body: JSON.stringify({ name: data.name, type: data.type }),
    }),
  // O PUT da API sobrescreve todos os campos do recurso, inclusive a foto: por isso
  // o que não é editado aqui (contato, bio, avatarUrl) volta junto no payload.
  update: (id: string, data: UpsertResourceRequest) =>
    apiFetch<void>(`/api/v1/resources/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: data.name,
        email: data.email,
        phone: data.phone,
        specialty: data.specialty,
        bio: data.bio,
        avatarUrl: data.avatarUrl,
      }),
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

  // ── Foto ───────────────────────────────────────────────────────────────────

  setImage: (id: string, file: File) =>
    apiUpload<{ url: string }>(`/api/v1/resources/${id}/image`, file),

  removeImage: (id: string) =>
    apiFetch<void>(`/api/v1/resources/${id}/image`, { method: 'DELETE' }),
}
