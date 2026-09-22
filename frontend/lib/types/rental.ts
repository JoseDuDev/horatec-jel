export interface RentableItemImage {
  id: string
  url: string
  /** 0 = capa. A lista já vem ordenada pela API. */
  sortOrder: number
}

export interface RentableItem {
  id: string
  name: string
  description?: string
  category?: string
  quantity: number
  dailyRate: number
  securityDeposit: number
  bufferDays: number
  /** Capa — espelho da primeira foto de `images`. */
  imageUrl?: string
  isActive: boolean
  images: RentableItemImage[]
}

export interface CreateRentableItemRequest {
  name: string
  quantity: number
  dailyRate: number
  securityDeposit: number
  bufferDays: number
  description?: string
  category?: string
  imageUrl?: string
}

export interface UpdateRentableItemRequest {
  name: string
  quantity: number
  dailyRate: number
  securityDeposit: number
  bufferDays: number
  description?: string
  category?: string
  /** Item inativo some da vitrine, mas continua no cadastro e no histórico. */
  isActive: boolean
}

export interface RentalAvailability {
  itemId: string
  startDate: string
  endDate: string
  days: number
  totalQuantity: number
  reservedUnits: number
  availableUnits: number
  isAvailable: boolean
}

export interface CreateRentalBookingRequest {
  items: { itemId: string; quantity: number }[]
  startDate: string // yyyy-MM-dd
  endDate: string   // yyyy-MM-dd
  notes?: string
}
