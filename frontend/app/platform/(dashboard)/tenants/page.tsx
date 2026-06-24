'use client'

import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { usePlatformAdminStore } from '@/store/platform-admin'
import { platformApi } from '@/lib/api/platform'
import type { TenantSummary, TenantPlan } from '@/lib/types/platform'
import { hasCapability } from '@/lib/types/platform'
import { EditTenantPackageDialog } from '@/components/platform/EditTenantPackageDialog'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'

const STATUS_COLOR: Record<string, string> = {
  Active:    'bg-green-100 text-green-700',
  Trial:     'bg-blue-100 text-blue-700',
  Suspended: 'bg-red-100 text-red-700',
  Cancelled: 'bg-slate-100 text-slate-600',
}

const PLAN_COLOR: Record<string, string> = {
  Free:         'bg-slate-100 text-slate-600',
  Starter:      'bg-indigo-100 text-indigo-700',
  Professional: 'bg-purple-100 text-purple-700',
  Enterprise:   'bg-amber-100 text-amber-700',
}

export default function PlatformTenantsPage() {
  const { accessToken } = usePlatformAdminStore()
  const [tenants, setTenants] = useState<TenantSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  const [editing, setEditing] = useState<TenantSummary | null>(null)

  const handleSaved = (id: string, capabilities: string, plan: TenantPlan) => {
    setTenants(ts => ts.map(t => t.id === id ? { ...t, capabilities, plan } : t))
  }

  useEffect(() => {
    if (!accessToken) return
    platformApi.tenants(accessToken)
      .then(setTenants)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false))
  }, [accessToken])

  const handleSuspend = async (id: string) => {
    if (!accessToken) return
    const reason = prompt('Motivo da suspensão:')
    if (!reason) return
    setActionLoading(id)
    try {
      await platformApi.suspendTenant(accessToken, id, reason)
      setTenants(ts => ts.map(t => t.id === id ? { ...t, status: 'Suspended' } : t))
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro')
    } finally {
      setActionLoading(null)
    }
  }

  const handleActivate = async (id: string) => {
    if (!accessToken) return
    setActionLoading(id)
    try {
      await platformApi.activateTenant(accessToken, id)
      setTenants(ts => ts.map(t => t.id === id ? { ...t, status: 'Active' as const } : t))
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro')
    } finally {
      setActionLoading(null)
    }
  }

  const filtered = tenants.filter(t =>
    t.name.toLowerCase().includes(search.toLowerCase()) ||
    t.slug.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Tenants</h1>
          <p className="text-slate-500 text-sm">{tenants.length} estabelecimentos cadastrados</p>
        </div>
        <Input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Buscar por nome ou slug..."
          className="w-64"
        />
      </div>

      {error && <div className="mb-4 p-3 bg-red-50 text-red-700 rounded-lg text-sm">{error}</div>}

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Lista de tenants</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-slate-500">
                    <th className="text-left py-2 pr-4">Nome</th>
                    <th className="text-left py-2 pr-4">Slug</th>
                    <th className="text-left py-2 pr-4">Status</th>
                    <th className="text-left py-2 pr-4">Plano</th>
                    <th className="text-left py-2 pr-4">Módulos</th>
                    <th className="text-left py-2 pr-4">Cadastro</th>
                    <th className="text-left py-2">Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map(t => (
                    <tr key={t.id} className="border-b last:border-0 hover:bg-slate-50">
                      <td className="py-3 pr-4 font-medium">{t.name}</td>
                      <td className="py-3 pr-4 text-slate-500 font-mono text-xs">{t.slug}</td>
                      <td className="py-3 pr-4">
                        <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${STATUS_COLOR[t.status] ?? ''}`}>
                          {t.status}
                        </span>
                      </td>
                      <td className="py-3 pr-4">
                        <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${PLAN_COLOR[t.plan] ?? ''}`}>
                          {t.plan}
                        </span>
                      </td>
                      <td className="py-3 pr-4">
                        <div className="flex gap-1 flex-wrap">
                          {hasCapability(t.capabilities, 'Appointments') && (
                            <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-sky-100 text-sky-700">Agend.</span>
                          )}
                          {hasCapability(t.capabilities, 'Rentals') && (
                            <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-teal-100 text-teal-700">Aluguel</span>
                          )}
                        </div>
                      </td>
                      <td className="py-3 pr-4 text-slate-500">
                        {format(new Date(t.createdAt), 'dd/MM/yyyy', { locale: ptBR })}
                      </td>
                      <td className="py-3">
                        <div className="flex gap-2">
                          <Button size="sm" variant="outline" onClick={() => setEditing(t)}>
                            Pacote
                          </Button>
                          {t.status === 'Suspended' ? (
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleActivate(t.id)}
                              disabled={actionLoading === t.id}
                            >
                              Reativar
                            </Button>
                          ) : t.status === 'Active' || t.status === 'Trial' ? (
                            <Button
                              size="sm"
                              variant="destructive"
                              onClick={() => handleSuspend(t.id)}
                              disabled={actionLoading === t.id}
                            >
                              Suspender
                            </Button>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  ))}
                  {filtered.length === 0 && (
                    <tr>
                      <td colSpan={7} className="py-8 text-center text-slate-400">
                        Nenhum tenant encontrado.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      <EditTenantPackageDialog
        tenant={editing}
        onClose={() => setEditing(null)}
        onSaved={handleSaved}
      />
    </div>
  )
}
