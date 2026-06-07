import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }), usePathname: () => '/platform/tenants' }))

vi.mock('@/store/platform-admin', () => ({
  usePlatformAdminStore: () => ({ admin: { email: 'admin@test.com' }, accessToken: 'tok123', logout: vi.fn() }),
}))

vi.mock('@/lib/api/platform', () => ({
  platformApi: {
    tenants: vi.fn().mockResolvedValue([
      {
        id: '1', name: 'Barbearia A', slug: 'barb-a', status: 'Active',
        plan: 'Starter', vertical: 'Barbershop', createdAt: '2026-01-01T00:00:00Z',
      },
      {
        id: '2', name: 'Clínica B', slug: 'clinic-b', status: 'Trial',
        plan: 'Free', vertical: 'MedicalClinic', createdAt: '2026-02-01T00:00:00Z',
      },
    ]),
    suspendTenant: vi.fn().mockResolvedValue(undefined),
    activateTenant: vi.fn().mockResolvedValue(undefined),
  },
}))

import PlatformTenantsPage from '@/app/platform/(dashboard)/tenants/page'

describe('PlatformTenantsPage', () => {
  it('renders tenant names after loading', async () => {
    render(<PlatformTenantsPage />)
    await waitFor(() => {
      expect(screen.getByText('Barbearia A')).toBeInTheDocument()
      expect(screen.getByText('Clínica B')).toBeInTheDocument()
    })
  })

  it('shows status badges', async () => {
    render(<PlatformTenantsPage />)
    await waitFor(() => {
      expect(screen.getByText('Active')).toBeInTheDocument()
      expect(screen.getByText('Trial')).toBeInTheDocument()
    })
  })
})
