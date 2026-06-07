'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { Building2, CreditCard, BarChart3, LogOut } from 'lucide-react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { cn } from '@/lib/utils'

const NAV = [
  { href: '/platform/tenants',    label: 'Tenants',    icon: Building2 },
  { href: '/platform/planos',     label: 'Planos',     icon: CreditCard },
  { href: '/platform/financeiro', label: 'Financeiro', icon: BarChart3 },
]

export function PlatformSidebar() {
  const pathname = usePathname()
  const router = useRouter()
  const { admin, logout } = usePlatformAdminStore()

  const handleLogout = () => {
    logout()
    document.cookie = 'platform_access_token=; path=/; max-age=0'
    router.push('/platform/login')
  }

  return (
    <aside className="w-60 min-h-screen bg-slate-900 text-white flex flex-col">
      <div className="h-16 flex items-center px-6 border-b border-slate-700">
        <div>
          <span className="font-bold text-lg">Horafy</span>
          <span className="ml-2 text-xs text-slate-400 bg-slate-700 px-1.5 py-0.5 rounded">Platform</span>
        </div>
      </div>
      <nav className="flex-1 p-4 space-y-1">
        {NAV.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            className={cn(
              'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
              pathname === href
                ? 'bg-indigo-600 text-white'
                : 'text-slate-300 hover:bg-slate-800 hover:text-white'
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        ))}
      </nav>
      <div className="p-4 border-t border-slate-700">
        <div className="text-xs text-slate-400 mb-3">{admin?.email}</div>
        <button
          onClick={handleLogout}
          className="flex items-center gap-2 text-sm text-slate-300 hover:text-white w-full"
        >
          <LogOut className="h-4 w-4" />
          Sair
        </button>
      </div>
    </aside>
  )
}
