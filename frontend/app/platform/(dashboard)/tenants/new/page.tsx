'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import { buildCapabilities } from '@/lib/types/platform'
import type { TenantPlan, TenantVertical } from '@/lib/types/platform'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const PLANS: TenantPlan[] = ['Free', 'Starter', 'Professional', 'Enterprise']

const VERTICALS: TenantVertical[] = [
  'Barbershop', 'EventHall', 'SportsCourt',
  'ToyRental', 'ToolRental',
  'MedicalClinic', 'AestheticClinic', 'Other',
]

export default function NewTenantPage() {
  const router = useRouter()
  const { accessToken } = usePlatformAdminStore()

  const [name, setName] = useState('')
  const [slug, setSlug] = useState('')
  const [vertical, setVertical] = useState<TenantVertical>('Barbershop')
  const [ownerName, setOwnerName] = useState('')
  const [ownerEmail, setOwnerEmail] = useState('')
  const [ownerPassword, setOwnerPassword] = useState('')
  const [appointments, setAppointments] = useState(true)
  const [rentals, setRentals] = useState(false)
  const [plan, setPlan] = useState<TenantPlan>('Free')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!accessToken) return
    if (!appointments && !rentals) {
      setError('Selecione ao menos um módulo.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await platformApi.createTenant(accessToken, {
        name,
        slug,
        vertical,
        ownerName,
        ownerEmail,
        ownerPassword,
        capabilities: buildCapabilities(appointments, rentals),
        plan,
      })
      router.push('/platform/tenants')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao criar tenant')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="max-w-2xl">
      <div className="mb-6">
        <Link href="/platform/tenants" className="text-sm text-slate-500 underline">
          &larr; Voltar para tenants
        </Link>
        <h1 className="text-2xl font-bold text-slate-900 mt-2">Novo Tenant</h1>
        <p className="text-slate-500 text-sm">
          Cria um novo estabelecimento e seu usuário proprietário.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dados do estabelecimento</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="name">Nome do estabelecimento</Label>
                <Input id="name" value={name} onChange={e => setName(e.target.value)} required />
              </div>
              <div>
                <Label htmlFor="slug">Slug</Label>
                <Input
                  id="slug"
                  value={slug}
                  onChange={e => setSlug(e.target.value)}
                  placeholder="meu-negocio"
                  required
                />
              </div>
            </div>

            <div>
              <Label>Segmento</Label>
              <Select value={vertical} onValueChange={v => setVertical(v as TenantVertical)}>
                <SelectTrigger className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {VERTICALS.map(v => <SelectItem key={v} value={v}>{v}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="ownerName">Nome do proprietário</Label>
                <Input id="ownerName" value={ownerName} onChange={e => setOwnerName(e.target.value)} required />
              </div>
              <div>
                <Label htmlFor="ownerEmail">E-mail do proprietário</Label>
                <Input
                  id="ownerEmail"
                  type="email"
                  value={ownerEmail}
                  onChange={e => setOwnerEmail(e.target.value)}
                  required
                />
              </div>
            </div>

            <div>
              <Label htmlFor="ownerPassword">Senha do proprietário</Label>
              <Input
                id="ownerPassword"
                type="password"
                value={ownerPassword}
                onChange={e => setOwnerPassword(e.target.value)}
                required
              />
              <p className="text-xs text-slate-400 mt-1">Mínimo 8 caracteres, 1 maiúscula e 1 número.</p>
            </div>

            <div>
              <Label>Módulos contratados</Label>
              <div className="mt-2 space-y-2">
                <label className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="checkbox"
                    checked={appointments}
                    onChange={e => setAppointments(e.target.checked)}
                  />
                  Agendamento
                </label>
                <label className="flex items-center gap-2 text-sm cursor-pointer">
                  <input type="checkbox" checked={rentals} onChange={e => setRentals(e.target.checked)} />
                  Aluguel
                </label>
              </div>
            </div>

            <div>
              <Label>Plano</Label>
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
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push('/platform/tenants')}
                disabled={saving}
              >
                Cancelar
              </Button>
              <Button type="submit" disabled={saving}>
                {saving ? 'Criando...' : 'Criar tenant'}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
