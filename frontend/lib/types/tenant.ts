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
  cancellationPolicy: CancellationPolicy
  loyaltySettings: LoyaltySettings
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
