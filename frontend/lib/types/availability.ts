export interface BusinessHoursDto {
  dayOfWeek: number   // 0 = Domingo … 6 = Sábado
  openTime: string    // "HH:mm:ss"
  closeTime: string   // "HH:mm:ss"
  isOpen: boolean
}

export interface AvailabilityRuleDto {
  id: string
  resourceId: string
  dayOfWeek: number
  startTime: string           // "HH:mm:ss"
  endTime: string             // "HH:mm:ss"
  slotDurationMinutes: number
  breakAfterMinutes: number
}

export interface AvailabilityExceptionDto {
  id: string
  resourceId: string
  date: string                // "yyyy-MM-dd"
  isBlocked: boolean
  customStart?: string        // "HH:mm:ss"
  customEnd?: string          // "HH:mm:ss"
  reason?: string
}

export interface SetResourceRuleRequest {
  dayOfWeek: number
  startTime: string           // "HH:mm:ss"
  endTime: string             // "HH:mm:ss"
  slotDurationMinutes: number
  breakAfterMinutes: number
}

export interface SetResourceExceptionRequest {
  date: string                // "yyyy-MM-dd"
  isBlocked: boolean
  customStart?: string        // "HH:mm:ss"
  customEnd?: string          // "HH:mm:ss"
  reason?: string
}

/** Bloqueio global por data — afeta todos os recursos do tenant. */
export interface BlackoutDateDto {
  id: string
  date: string                // "yyyy-MM-dd"
  reason?: string
}

export interface CreateBlackoutDateRequest {
  date: string                // "yyyy-MM-dd"
  reason?: string
}
