'use client'

import Link from 'next/link'
import { usePortalAuthStore } from '@/store/portal-auth'
import { GoogleSignInButton } from './GoogleSignInButton'
import { Button } from '@/components/ui/button'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { useRouter } from 'next/navigation'
import { hasCapability } from '@/lib/types/platform'

interface Props {
  slug: string
  tenantName: string
  logoUrl?: string
  /** CSV de capabilities do tenant (ex.: "Appointments, Rentals"). */
  capabilities?: string
}

export function PortalNavbar({ slug, tenantName, logoUrl, capabilities }: Props) {
  const router = useRouter()
  const { customer, clearCustomerAuth } = usePortalAuthStore()

  // Sem capabilities conhecidas (ex.: falha ao resolver o tenant) mantemos o
  // comportamento histórico: mostrar agendamento. "Alugar" só quando confirmado.
  const known = (capabilities ?? '').length > 0
  const showAppointments = known ? hasCapability(capabilities, 'Appointments') : true
  const showRentals = hasCapability(capabilities, 'Rentals')

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
          {showAppointments && (
            <>
              <Link href={`/${slug}/servicos`} className="text-sm text-slate-600 hover:text-slate-900">
                Serviços
              </Link>
              <Link href={`/${slug}/agendar`} className="text-sm text-slate-600 hover:text-slate-900">
                Agendar
              </Link>
            </>
          )}
          {showRentals && (
            <Link href={`/${slug}/alugar`} className="text-sm text-slate-600 hover:text-slate-900">
              Alugar
            </Link>
          )}
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
