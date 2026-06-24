'use client'

import { useEffect, useState } from 'react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary, PlanConfig } from '@/lib/types/platform'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export default function PlatformPlanosPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [plans, setPlans] = useState<PlanConfig[]>([])
  const [savingPlan, setSavingPlan] = useState<string | null>(null)
  const [savedPlan, setSavedPlan] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken).then(setTenants).catch(() => {})
    platformApi.plans(accessToken).then(setPlans).catch(() => {})
  }, [accessToken])

  const countByPlan = (plan: string) => tenants.filter(t => t.plan === plan).length

  const setField = (plan: string, field: keyof PlanConfig, value: number) => {
    setPlans(ps => ps.map(p => (p.plan === plan ? { ...p, [field]: value } : p)))
    setSavedPlan(null)
  }

  const save = async (p: PlanConfig) => {
    if (!accessToken) return
    setSavingPlan(p.plan)
    try {
      await platformApi.updatePlan(accessToken, p.plan, {
        maxServices: p.maxServices,
        maxResources: p.maxResources,
        maxRentableItems: p.maxRentableItems,
      })
      setSavedPlan(p.plan)
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setSavingPlan(null)
    }
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Planos</h1>
        <p className="text-slate-500 text-sm">
          Limites de cadastro de cada plano. Use <span className="font-mono">-1</span> para ilimitado.
        </p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {plans.map(p => (
          <Card key={p.plan} className="flex flex-col">
            <CardHeader>
              <CardTitle className="text-lg">{p.plan}</CardTitle>
              <p className="text-xs text-slate-400">{countByPlan(p.plan)} tenant(s) neste plano</p>
            </CardHeader>
            <CardContent className="flex-1 space-y-3">
              <NumberField label="Serviços" value={p.maxServices}
                onChange={v => setField(p.plan, 'maxServices', v)} />
              <NumberField label="Recursos" value={p.maxResources}
                onChange={v => setField(p.plan, 'maxResources', v)} />
              <NumberField label="Itens de locação" value={p.maxRentableItems}
                onChange={v => setField(p.plan, 'maxRentableItems', v)} />
              <Button className="w-full" onClick={() => save(p)} disabled={savingPlan === p.plan}>
                {savingPlan === p.plan ? 'Salvando...' : savedPlan === p.plan ? 'Salvo ✓' : 'Salvar'}
              </Button>
            </CardContent>
          </Card>
        ))}
        {plans.length === 0 && <p className="text-slate-400">Carregando planos...</p>}
      </div>
    </div>
  )
}

function NumberField({
  label, value, onChange,
}: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <div>
      <Label className="text-xs">{label}</Label>
      <Input
        type="number"
        min={-1}
        value={value}
        onChange={e => {
          const n = parseInt(e.target.value, 10)
          onChange(Number.isNaN(n) ? 0 : n)
        }}
        className="mt-1"
      />
      {value < 0 && <p className="text-[10px] text-slate-400 mt-0.5">Ilimitado</p>}
    </div>
  )
}
