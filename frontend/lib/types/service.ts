export interface Service {
  id: string
  name: string
  description?: string
  durationMinutes: number
  price: number
  categoryId?: string
  isActive: boolean
}

export interface UpsertServiceRequest {
  name: string
  description?: string
  durationMinutes: number
  price: number
  categoryId?: string
  isActive?: boolean
}
