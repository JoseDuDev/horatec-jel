export type TenantStatus = 'Active' | 'Suspended' | 'Trial' | 'Cancelled'
export type TenantPlan = 'Free' | 'Starter' | 'Professional' | 'Enterprise'
export type TenantVertical =
  | 'Barbershop' | 'EventHall' | 'SportsCourt'
  | 'ToyRental' | 'ToolRental'
  | 'MedicalClinic' | 'AestheticClinic' | 'Other'

export interface TenantSummary {
  id: string
  name: string
  slug: string
  status: TenantStatus
  plan: TenantPlan
  vertical: TenantVertical
  email?: string
  createdAt: string
  trialEndsAt?: string
  planRenewsAt?: string
}

export interface PlanLimits {
  plan: TenantPlan
  maxServices: number
  maxResources: number
  maxBookingsPerMonth: number
  priceMonthly: number
}

export const PLAN_LIMITS: PlanLimits[] = [
  { plan: 'Free',         maxServices: 3,   maxResources: 1,  maxBookingsPerMonth: 50,   priceMonthly: 0 },
  { plan: 'Starter',      maxServices: 10,  maxResources: 3,  maxBookingsPerMonth: 200,  priceMonthly: 49 },
  { plan: 'Professional', maxServices: 50,  maxResources: 10, maxBookingsPerMonth: 1000, priceMonthly: 149 },
  { plan: 'Enterprise',   maxServices: 999, maxResources: 99, maxBookingsPerMonth: 9999, priceMonthly: 399 },
]
