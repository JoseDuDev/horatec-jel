import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { CustomerProfile } from '@/lib/types/portal'

interface PortalAuthState {
  customer: CustomerProfile | null
  accessToken: string | null
  refreshToken: string | null
  setCustomerAuth: (customer: CustomerProfile, accessToken: string, refreshToken: string) => void
  clearCustomerAuth: () => void
}

export const usePortalAuthStore = create<PortalAuthState>()(
  persist(
    (set) => ({
      customer: null,
      accessToken: null,
      refreshToken: null,
      setCustomerAuth: (customer, accessToken, refreshToken) =>
        set({ customer, accessToken, refreshToken }),
      clearCustomerAuth: () => set({ customer: null, accessToken: null, refreshToken: null }),
    }),
    { name: 'horafy-portal-auth' }
  )
)
