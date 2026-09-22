export interface Service {
  id: string
  name: string
  description?: string
  durationMinutes: number
  price: number
  categoryId?: string
  imageUrl?: string
  isActive: boolean
}

export interface UpsertServiceRequest {
  name: string
  description?: string
  durationMinutes: number
  price: number
  categoryId?: string
  imageUrl?: string
  isActive?: boolean
}
