import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface PlatformAdmin {
  email: string
}

interface PlatformAdminState {
  admin: PlatformAdmin | null
  accessToken: string | null
  login: (email: string, accessToken: string) => void
  logout: () => void
}

export const usePlatformAdminStore = create<PlatformAdminState>()(
  persist(
    (set) => ({
      admin: null,
      accessToken: null,
      login: (email, accessToken) => set({ admin: { email }, accessToken }),
      logout: () => set({ admin: null, accessToken: null }),
    }),
    { name: 'horafy-platform-admin' }
  )
)
