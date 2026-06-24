'use client'

import { useEffect, useState } from 'react'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import { buildCapabilities, hasCapability } from '@/lib/types/platform'
import type { TenantSummary, TenantPlan } from '@/lib/types/platform'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const PLANS: TenantPlan[] = ['Free', 'Starter', 'Professional', 'Enterprise']

interface Props {
  tenant: TenantSummary | null
  onClose: () => void
  onSaved: (id: string, capabilities: string, plan: TenantPlan) => void
}

export function EditTenantPackageDialog({ tenant, onClose, onSaved }: Props) {
  const { accessToken } = usePlatformAdminStore()
  const [appointments, setAppointments] = useState(false)
  const [rentals, setRentals] = useState(false)
  const [plan, setPlan] = useState<TenantPlan>('Free')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Reinicializa os campos sempre que um tenant é selecionado.
  useEffect(() => {
    if (!tenant) return
    setAppointments(hasCapability(tenant.capabilities, 'Appointments'))
    setRentals(hasCapability(tenant.capabilities, 'Rentals'))
    setPlan(tenant.plan)
    setError(null)
  }, [tenant])

  const handleSave = async () => {
    if (!tenant || !accessToken) return
    if (!appointments && !rentals) {
      setError('Selecione ao menos um módulo.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const capabilities = buildCapabilities(appointments, rentals)
      await platformApi.updateTenantPlan(accessToken, tenant.id, { capabilities, plan })
      onSaved(tenant.id, capabilities, plan)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open={tenant !== null} onOpenChange={o => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Editar pacote — {tenant?.name}</DialogTitle>
        </DialogHeader>

        <div className="space-y-5">
          <div>
            <Label>Módulos contratados</Label>
            <div className="mt-2 space-y-2">
              <label className="flex items-center gap-2 text-sm cursor-pointer">
                <input type="checkbox" checked={appointments}
                  onChange={e => setAppointments(e.target.checked)} />
                Agendamento
              </label>
              <label className="flex items-center gap-2 text-sm cursor-pointer">
                <input type="checkbox" checked={rentals}
                  onChange={e => setRentals(e.target.checked)} />
                Aluguel
              </label>
            </div>
          </div>

          <div>
            <Label>Plano (limites de cadastro)</Label>
            <Select value={plan} onValueChange={v => setPlan(v as TenantPlan)}>
              <SelectTrigger className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PLANS.map(p => <SelectItem key={p} value={p}>{p}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onClose} disabled={saving}>Cancelar</Button>
            <Button onClick={handleSave} disabled={saving}>
              {saving ? 'Salvando...' : 'Salvar'}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}
