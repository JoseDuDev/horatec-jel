'use client'

import Link from 'next/link'
import { usePortalAuthStore } from '@/store/portal-auth'
import { GoogleSignInButton } from './GoogleSignInButton'
import { Button } from '@/components/ui/button'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { useRouter } from 'next/navigation'

interface Props {
  slug: string
  tenantName: string
  logoUrl?: string
}

export function PortalNavbar({ slug, tenantName, logoUrl }: Props) {
  const router = useRouter()
  const { customer, clearCustomerAuth } = usePortalAuthStore()

  const handleLogout = () => {
    document.cookie = 'portal_access_token=; path=/; max-age=0'
    clearCustomerAuth()
    router.refresh()
  }

  return (
    <header className="border-b bg-white sticky top-0 z-50">
      <div className="max-w-5xl mx-auto px-4 h-16 flex items-center justify-between">
        <Link href={`/${slug}`} className="flex items-center gap-3">
          {logoUrl ? (
            <img src={logoUrl} alt={tenantName} className="h-8 w-auto" />
          ) : (
            <span className="font-bold text-xl">{tenantName}</span>
          )}
        </Link>

        <nav className="hidden md:flex items-center gap-6">
          <Link href={`/${slug}/servicos`} className="text-sm text-slate-600 hover:text-slate-900">
            Serviços
          </Link>
          <Link href={`/${slug}/agendar`} className="text-sm text-slate-600 hover:text-slate-900">
            Agendar
          </Link>
          {customer && (
            <Link href={`/${slug}/minha-conta`} className="text-sm text-slate-600 hover:text-slate-900">
              Minha Conta
            </Link>
          )}
        </nav>

        <div className="flex items-center gap-3">
          {customer ? (
            <>
              <Avatar className="h-8 w-8">
                <AvatarFallback>{customer.name[0]}</AvatarFallback>
              </Avatar>
              <Button variant="ghost" size="sm" onClick={handleLogout}>Sair</Button>
            </>
          ) : (
            <GoogleSignInButton slug={slug} />
          )}
        </div>
      </div>
    </header>
  )
}
