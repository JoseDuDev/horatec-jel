export interface CancellationPolicy {
  minCancellationHours: number
  cancellationFeePercent: number
  allowCustomerCancellation: boolean
}

export interface LoyaltySettings {
  isEnabled: boolean
  creditRatePercent: number
  minBookingAmount: number
}

export interface Tenant {
  id: string
  name: string
  slug: string
  logoUrl?: string
  primaryColor?: string
  customDomain?: string
  timezone: string
  plan: string
  /** Enum [Flags] serializado como CSV pelo backend, ex.: "Appointments, Rentals". */
  capabilities: string
  cancellationPolicy: CancellationPolicy
  loyaltySettings: LoyaltySettings
  isOnboardingCompleted: boolean
}

export interface UsageItem {
  used: number
  /** -1 = ilimitado. */
  max: number
}

export interface TenantUsage {
  capabilities: string
  plan: string
  services: UsageItem
  resources: UsageItem
  rentableItems: UsageItem
}

export interface UpdateTenantRequest {
  name?: string
  logoUrl?: string
  primaryColor?: string
  timezone?: string
}

export interface UpdateLoyaltySettingsRequest {
  isEnabled: boolean
  creditRatePercent: number
  minBookingAmount: number
}

export interface UpdateCancellationPolicyRequest {
  minCancellationHours: number
  cancellationFeePercent: number
  allowCustomerCancellation: boolean
}
