import { PortalNavbar } from '@/components/portal/PortalNavbar'
import { portalApi } from '@/lib/api/portal'

interface Props {
  children: React.ReactNode
  params: Promise<{ slug: string }>
}

export default async function PortalLayout({ children, params }: Props) {
  const { slug } = await params
  let tenantName = slug
  let logoUrl: string | undefined
  let capabilities = ''

  try {
    const tenant = await portalApi.tenant(slug)
    tenantName = tenant.name
    logoUrl = tenant.logoUrl
    capabilities = tenant.capabilities ?? ''
  } catch {
    // tenant not found — still render with slug as name
  }

  return (
    <div className="min-h-screen bg-white">
      <PortalNavbar slug={slug} tenantName={tenantName} logoUrl={logoUrl} capabilities={capabilities} />
      <main>{children}</main>
      <footer className="border-t mt-16 py-8">
        <p className="text-center text-sm text-slate-400">
          Um produto{' '}
          <a
            href="https://mjml.com.br"
            target="_blank"
            rel="noreferrer"
            className="font-semibold underline-offset-4 hover:underline"
          >
            MJML
          </a>
        </p>
      </footer>
    </div>
  )
}
