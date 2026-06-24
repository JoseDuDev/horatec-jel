'use client'

import { useEffect, useState } from 'react'
import { tenantsApi } from '@/lib/api/tenants'
import type { TenantUsage, UsageItem } from '@/lib/types/tenant'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

function UsageRow({ label, item }: { label: string; item: UsageItem }) {
  const unlimited = item.max < 0
  const pct = unlimited ? 0 : Math.min(100, Math.round((item.used / Math.max(item.max, 1)) * 100))
  const full = !unlimited && item.used >= item.max
  return (
    <div>
      <div className="flex justify-between text-sm mb-1">
        <span className="text-slate-600">{label}</span>
        <span className={full ? 'font-semibold text-red-600' : 'text-slate-500'}>
          {unlimited ? `${item.used} (ilimitado)` : `${item.used} / ${item.max}`}
        </span>
      </div>
      {!unlimited && (
        <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
          <div
            className={full ? 'h-full bg-red-500' : 'h-full bg-indigo-500'}
            style={{ width: `${pct}%` }}
          />
        </div>
      )}
    </div>
  )
}

export function PlanUsageCard() {
  const [usage, setUsage] = useState<TenantUsage | null>(null)
  const [error, setError] = useState(false)

  useEffect(() => {
    tenantsApi.usage().then(setUsage).catch(() => setError(true))
  }, [])

  if (error) return (
    <Card>
      <CardHeader><CardTitle>Plano e uso</CardTitle></CardHeader>
      <CardContent><p className="text-sm text-slate-400">Não foi possível carregar o uso.</p></CardContent>
    </Card>
  )
  if (!usage) return <p className="text-sm text-slate-400">Carregando uso...</p>

  const hasAppointments = usage.capabilities.includes('Appointments')
  const hasRentals = usage.capabilities.includes('Rentals')

  return (
    <Card>
      <CardHeader><CardTitle>Plano e uso</CardTitle></CardHeader>
      <CardContent className="space-y-4">
        <div className="flex items-center gap-2 text-sm">
          <span className="text-slate-600">Plano:</span>
          <span className="font-semibold">{usage.plan}</span>
          <span className="ml-auto flex gap-1">
            {hasAppointments && (
              <span className="px-2 py-0.5 rounded-full text-xs bg-sky-100 text-sky-700">Agendamento</span>
            )}
            {hasRentals && (
              <span className="px-2 py-0.5 rounded-full text-xs bg-teal-100 text-teal-700">Aluguel</span>
            )}
          </span>
        </div>

        <div className="space-y-3">
          {hasAppointments && <UsageRow label="Serviços" item={usage.services} />}
          {hasAppointments && <UsageRow label="Recursos" item={usage.resources} />}
          {hasRentals && <UsageRow label="Itens de locação" item={usage.rentableItems} />}
        </div>

        <p className="text-xs text-slate-400">
          Os limites são definidos pelo plano contratado. Para ampliar, fale com o suporte.
        </p>
      </CardContent>
    </Card>
  )
}
