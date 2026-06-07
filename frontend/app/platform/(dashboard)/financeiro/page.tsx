'use client'

import { useEffect, useState } from 'react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary } from '@/lib/types/platform'
import { PLAN_LIMITS } from '@/lib/types/platform'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { TrendingUp, Users, DollarSign, Activity } from 'lucide-react'

export default function PlatformFinanceiroPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken)
      .then(setTenants)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [accessToken])

  const activeTenants = tenants.filter(t => t.status === 'Active' || t.status === 'Trial')

  const mrr = tenants
    .filter(t => t.status === 'Active')
    .reduce((sum, t) => {
      const plan = PLAN_LIMITS.find(p => p.plan === t.plan)
      return sum + (plan?.priceMonthly ?? 0)
    }, 0)

  const planDist = PLAN_LIMITS.map(p => ({
    plan: p.plan,
    count: tenants.filter(t => t.plan === p.plan).length,
    revenue: tenants.filter(t => t.plan === p.plan && t.status === 'Active').length * p.priceMonthly,
  }))

  const metrics = [
    { label: 'MRR (Receita Mensal)',  value: `R$ ${mrr.toLocaleString('pt-BR')}`,         icon: DollarSign, color: 'text-green-600' },
    { label: 'Tenants Ativos',        value: activeTenants.length,                          icon: Users,      color: 'text-indigo-600' },
    { label: 'Total Tenants',         value: tenants.length,                                icon: Activity,   color: 'text-slate-600' },
    { label: 'ARR (Estimado)',        value: `R$ ${(mrr * 12).toLocaleString('pt-BR')}`,   icon: TrendingUp, color: 'text-purple-600' },
  ]

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Financeiro da Plataforma</h1>
        <p className="text-slate-500 text-sm">Receita e distribuição de tenants por plano</p>
      </div>

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 mb-8">
            {metrics.map(m => (
              <Card key={m.label}>
                <CardContent className="pt-4">
                  <div className="flex items-center gap-3">
                    <m.icon className={`h-8 w-8 ${m.color}`} />
                    <div>
                      <p className="text-xs text-slate-500">{m.label}</p>
                      <p className="text-2xl font-bold text-slate-900">{m.value}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">Distribuição por Plano</CardTitle>
            </CardHeader>
            <CardContent>
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-slate-500">
                    <th className="text-left py-2 pr-4">Plano</th>
                    <th className="text-right py-2 pr-4">Tenants</th>
                    <th className="text-right py-2 pr-4">Receita Mensal</th>
                    <th className="text-right py-2">Receita Anual</th>
                  </tr>
                </thead>
                <tbody>
                  {planDist.map(p => (
                    <tr key={p.plan} className="border-b last:border-0">
                      <td className="py-3 pr-4 font-medium">{p.plan}</td>
                      <td className="py-3 pr-4 text-right">{p.count}</td>
                      <td className="py-3 pr-4 text-right text-green-700 font-medium">
                        R$ {p.revenue.toLocaleString('pt-BR')}
                      </td>
                      <td className="py-3 text-right text-slate-500">
                        R$ {(p.revenue * 12).toLocaleString('pt-BR')}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  )
}
