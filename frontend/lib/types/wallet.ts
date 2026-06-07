export type WalletTransactionType =
  | 'CreditAdded'
  | 'BookingPayment'
  | 'BookingRefund'
  | 'LoyaltyBonus'

export interface WalletTransactionResult {
  id: string
  type: WalletTransactionType
  amount: number
  description: string
  bookingId?: string
  createdAt: string
}

export interface WalletResult {
  walletId: string
  balance: number
  transactions: WalletTransactionResult[]
}

export type VoucherDiscountType = 'Percentage' | 'Fixed'

export interface VoucherSummary {
  id: string
  code: string
  discountType: VoucherDiscountType
  discountValue: number
  description?: string
  expiresAt?: string
  maxUses?: number
  usedCount: number
  isActive: boolean
  createdAt: string
}

export interface VoucherValidationResult {
  code: string
  discountType: VoucherDiscountType
  discountValue: number
  discountAmount: number
  finalPrice: number
  description?: string
}

export interface CreateVoucherRequest {
  code: string
  discountType: VoucherDiscountType
  discountValue: number
  description?: string
  expiresAt?: string
  maxUses?: number
}
