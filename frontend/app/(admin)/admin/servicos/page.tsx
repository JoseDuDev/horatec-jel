'use client'

import { useEffect, useState } from 'react'
import { servicesApi } from '@/lib/api/services'
import { ServiceForm } from '@/components/services/ServiceForm'
import type { Service, UpsertServiceRequest } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Plus, Pencil, Trash2 } from 'lucide-react'

export default function ServicosPage() {
  const [services, setServices] = useState<Service[]>([])
  const [editing, setEditing] = useState<Service | null | 'new'>(null)

  const load = () => servicesApi.list().then(setServices)
  useEffect(() => { load() }, [])

  const handleSubmit = async (data: UpsertServiceRequest) => {
    if (editing === 'new') await servicesApi.create(data)
    else if (editing) await servicesApi.update(editing.id, data)
    setEditing(null)
    load()
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Excluir este serviço?')) return
    await servicesApi.remove(id)
    load()
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Serviços</h1>
        <Button onClick={() => setEditing('new')}>
          <Plus className="h-4 w-4 mr-2" /> Novo Serviço
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {services.map(s => (
          <Card key={s.id}>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base">{s.name}</CardTitle>
                <Badge variant={s.isActive ? 'default' : 'secondary'}>
                  {s.isActive ? 'Ativo' : 'Inativo'}
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-500">{s.durationMinutes} min · R$ {s.price.toFixed(2)}</p>
              {s.description && <p className="text-xs text-slate-400 mt-1">{s.description}</p>}
              <div className="flex gap-2 mt-4">
                <Button size="sm" variant="outline" onClick={() => setEditing(s)}>
                  <Pencil className="h-3 w-3 mr-1" /> Editar
                </Button>
                <Button size="sm" variant="destructive" onClick={() => handleDelete(s.id)}>
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
            <DialogTitle>{editing === 'new' ? 'Novo Serviço' : 'Editar Serviço'}</DialogTitle>
          </DialogHeader>
          <ServiceForm
            initial={editing !== 'new' && editing !== null ? editing : undefined}
            onSubmit={handleSubmit}
            onCancel={() => setEditing(null)}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}
