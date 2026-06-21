'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import {
  LayoutDashboard, CalendarDays, ClipboardList, Users,
  Scissors, Briefcase, Clock, DollarSign, Bell, Settings, Rocket, Wallet2, Package
} from 'lucide-react'
import { cn } from '@/lib/utils'

const NAV = [
  { href: '/admin/dashboard',      label: 'Dashboard',     icon: LayoutDashboard },
  { href: '/admin/agenda',         label: 'Agenda',        icon: CalendarDays },
  { href: '/admin/agendamentos',   label: 'Agendamentos',  icon: ClipboardList },
  { href: '/admin/clientes',       label: 'Clientes',      icon: Users },
  { href: '/admin/servicos',       label: 'Serviços',      icon: Scissors },
  { href: '/admin/recursos',        label: 'Recursos',        icon: Briefcase },
  { href: '/admin/locacoes',        label: 'Locações',        icon: Package },
  { href: '/admin/disponibilidade', label: 'Disponibilidade', icon: Clock },
  { href: '/admin/financeiro',      label: 'Financeiro',      icon: DollarSign },
  { href: '/admin/carteira',       label: 'Carteira',      icon: Wallet2 },
  { href: '/admin/notificacoes',   label: 'Notificações',  icon: Bell },
  { href: '/admin/onboarding',     label: 'Onboarding',    icon: Rocket },
  { href: '/admin/configuracoes',  label: 'Configurações', icon: Settings },
]

export function Sidebar() {
  const pathname = usePathname()
  return (
    <aside className="w-60 min-h-screen bg-white border-r flex flex-col">
      <div className="h-16 flex items-center px-6 border-b">
        <span className="font-bold text-xl text-slate-800">Horafy</span>
      </div>
      <nav className="flex-1 p-4 space-y-1">
        {NAV.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            className={cn(
              'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
              pathname === href
                ? 'bg-slate-100 text-slate-900'
                : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        ))}
      </nav>
    </aside>
  )
}
