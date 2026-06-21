import { apiFetch } from './client'
import type { RentableItem, CreateRentableItemRequest } from '../types/rental'

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
}
