'use client'

import { useEffect, useState } from 'react'
import { resourcesApi } from '@/lib/api/resources'
import { servicesApi } from '@/lib/api/services'
import { ResourceForm } from '@/components/resources/ResourceForm'
import type { Resource, UpsertResourceRequest } from '@/lib/types/resource'
import type { Service } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Plus, Pencil, Trash2 } from 'lucide-react'

export default function RecursosPage() {
  const [resources, setResources] = useState<Resource[]>([])
  const [services, setServices] = useState<Service[]>([])
  const [editing, setEditing] = useState<Resource | null | 'new'>(null)

  const load = () =>
    Promise.all([resourcesApi.list(), servicesApi.list()]).then(([r, s]) => {
      setResources(r)
      setServices(s)
    })

  useEffect(() => { load() }, [])

  const handleSubmit = async (data: UpsertResourceRequest) => {
    if (editing === 'new') await resourcesApi.create(data)
    else if (editing) await resourcesApi.update(editing.id, data)
    setEditing(null)
    load()
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Excluir este recurso?')) return
    await resourcesApi.remove(id)
    load()
  }

  const getServiceNames = (ids: string[]) =>
    ids.map(id => services.find(s => s.id === id)?.name ?? id).join(', ')

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Recursos</h1>
        <Button onClick={() => setEditing('new')}>
          <Plus className="h-4 w-4 mr-2" /> Novo Recurso
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {resources.map(r => (
          <Card key={r.id}>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">{r.name}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-500">Tipo: {r.type}</p>
              {r.serviceIds.length > 0 && (
                <p className="text-xs text-slate-400 mt-1">
                  Serviços: {getServiceNames(r.serviceIds)}
                </p>
              )}
              <div className="flex gap-2 mt-4">
                <Button size="sm" variant="outline" onClick={() => setEditing(r)}>
                  <Pencil className="h-3 w-3 mr-1" /> Editar
                </Button>
                <Button size="sm" variant="destructive" onClick={() => handleDelete(r.id)}>
                  <Trash2 className="h-3 w-3 mr-1" /> Excluir
                </Button>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={editing !== null} onOpenChange={open => !open && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing === 'new' ? 'Novo Recurso' : 'Editar Recurso'}</DialogTitle>
          </DialogHeader>
          <ResourceForm
            initial={editing !== 'new' && editing !== null ? editing : undefined}
            services={services}
            onSubmit={handleSubmit}
            onCancel={() => setEditing(null)}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}
