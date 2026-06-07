'use client'

import { useEffect, useState } from 'react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary } from '@/lib/types/platform'
import { PLAN_LIMITS } from '@/lib/types/platform'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Check } from 'lucide-react'

export default function PlatformPlanosPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken).then(setTenants).catch(() => {})
  }, [accessToken])

  const countByPlan = (plan: string) => tenants.filter(t => t.plan === plan).length

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Planos</h1>
        <p className="text-slate-500 text-sm">Limites e preços de cada plano da plataforma</p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {PLAN_LIMITS.map(p => (
          <Card key={p.plan} className="flex flex-col">
            <CardHeader>
              <CardTitle className="text-lg">{p.plan}</CardTitle>
              <p className="text-2xl font-bold mt-1">
                {p.priceMonthly === 0 ? 'Grátis' : `R$ ${p.priceMonthly}/mês`}
              </p>
            </CardHeader>
            <CardContent className="flex-1">
              <ul className="space-y-2 text-sm text-slate-600">
                <li className="flex items-center gap-2">
                  <Check className="h-4 w-4 text-green-500 shrink-0" />
                  {p.maxServices === 999 ? 'Serviços ilimitados' : `${p.maxServices} serviços`}
                </li>
                <li className="flex items-center gap-2">
                  <Check className="h-4 w-4 text-green-500 shrink-0" />
                  {p.maxResources === 99 ? 'Recursos ilimitados' : `${p.maxResources} recursos`}
                </li>
                <li className="flex items-center gap-2">
                  <Check className="h-4 w-4 text-green-500 shrink-0" />
                  {p.maxBookingsPerMonth === 9999
                    ? 'Agendamentos ilimitados'
                    : `${p.maxBookingsPerMonth} agendamentos/mês`}
                </li>
              </ul>
              <div className="mt-4 pt-4 border-t">
                <p className="text-xs text-slate-400">Tenants neste plano</p>
                <p className="text-2xl font-bold text-slate-900">{countByPlan(p.plan)}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
