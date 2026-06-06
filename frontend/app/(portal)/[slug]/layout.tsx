import { PortalNavbar } from '@/components/portal/PortalNavbar'
import { portalApi } from '@/lib/api/portal'

interface Props {
  children: React.ReactNode
  params: { slug: string }
}

export default async function PortalLayout({ children, params }: Props) {
  const { slug } = params
  let tenantName = slug
  let logoUrl: string | undefined

  try {
    const tenant = await portalApi.tenant(slug)
    tenantName = tenant.name
    logoUrl = tenant.logoUrl
  } catch {
    // tenant not found — still render with slug as name
  }

  return (
    <div className="min-h-screen bg-white">
      <PortalNavbar slug={slug} tenantName={tenantName} logoUrl={logoUrl} />
      <main>{children}</main>
      <footer className="border-t mt-16 py-8">
        <p className="text-center text-sm text-slate-400">
          Powered by <span className="font-semibold">Horafy</span>
        </p>
      </footer>
    </div>
  )
}
