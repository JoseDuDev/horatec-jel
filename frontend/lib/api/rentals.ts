import { apiFetch, apiUpload } from './client'
import type {
  RentableItem,
  RentableItemImage,
  CreateRentableItemRequest,
  UpdateRentableItemRequest,
} from '../types/rental'

/** Teto de fotos por item — espelha RentableItem.MaxImages no backend. */
export const MAX_ITEM_IMAGES = 6

// Operações de admin sobre itens de locação (usa JWT do admin via cookies).
export const rentalsApi = {
  // onlyActive=false para o admin enxergar também itens inativos.
  list: (onlyActive = false) =>
    apiFetch<RentableItem[]>(`/api/v1/rentals/items?onlyActive=${onlyActive}`),

  create: (data: CreateRentableItemRequest) =>
    apiFetch<string>('/api/v1/rentals/items', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  update: (id: string, data: UpdateRentableItemRequest) =>
    apiFetch<void>(`/api/v1/rentals/items/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  // ── Galeria ────────────────────────────────────────────────────────────────

  addImage: (itemId: string, file: File) =>
    apiUpload<RentableItemImage>(`/api/v1/rentals/items/${itemId}/images`, file),

  removeImage: (itemId: string, imageId: string) =>
    apiFetch<void>(`/api/v1/rentals/items/${itemId}/images/${imageId}`, {
      method: 'DELETE',
    }),

  setCoverImage: (itemId: string, imageId: string) =>
    apiFetch<void>(`/api/v1/rentals/items/${itemId}/images/${imageId}/cover`, {
      method: 'PUT',
    }),
}
