import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { CustomerProfile } from '@/lib/types/portal'

interface PortalAuthState {
  customer: CustomerProfile | null
  accessToken: string | null
  setCustomerAuth: (customer: CustomerProfile, accessToken: string) => void
  clearCustomerAuth: () => void
}

export const usePortalAuthStore = create<PortalAuthState>()(
  persist(
    (set) => ({
      customer: null,
      accessToken: null,
      setCustomerAuth: (customer, accessToken) => set({ customer, accessToken }),
      clearCustomerAuth: () => set({ customer: null, accessToken: null }),
    }),
    { name: 'horafy-portal-auth' }
  )
)
