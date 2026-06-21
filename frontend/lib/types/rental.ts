export interface RentableItem {
  id: string
  name: string
  description?: string
  category?: string
  quantity: number
  dailyRate: number
  securityDeposit: number
  bufferDays: number
  imageUrl?: string
  isActive: boolean
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
