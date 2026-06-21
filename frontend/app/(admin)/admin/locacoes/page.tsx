'use client'

import { useEffect, useState } from 'react'
import { rentalsApi } from '@/lib/api/rentals'
import { RentableItemForm } from '@/components/rentals/RentableItemForm'
import type { RentableItem, CreateRentableItemRequest } from '@/lib/types/rental'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Plus } from 'lucide-react'

export default function LocacoesPage() {
  const [items, setItems] = useState<RentableItem[]>([])
  const [creating, setCreating] = useState(false)

  const load = () => rentalsApi.list().then(setItems).catch(() => setItems([]))
  useEffect(() => { load() }, [])

  const handleSubmit = async (data: CreateRentableItemRequest) => {
    await rentalsApi.create(data)
    setCreating(false)
    load()
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Itens de Locação</h1>
        <Button onClick={() => setCreating(true)}>
          <Plus className="h-4 w-4 mr-2" /> Novo Item
        </Button>
      </div>

      {items.length === 0 && (
        <p className="text-sm text-slate-500">Nenhum item de locação cadastrado.</p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {items.map(i => (
          <Card key={i.id}>
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base">{i.name}</CardTitle>
                <Badge variant={i.isActive ? 'default' : 'secondary'}>
                  {i.isActive ? 'Ativo' : 'Inativo'}
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-500">
                R$ {i.dailyRate.toFixed(2)}/dia · {i.quantity} un. · caução R$ {i.securityDeposit.toFixed(2)}
              </p>
              {i.bufferDays > 0 && (
                <p className="text-xs text-slate-400 mt-1">Buffer de {i.bufferDays} dia(s) entre locações</p>
              )}
              {i.description && <p className="text-xs text-slate-400 mt-1">{i.description}</p>}
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={creating} onOpenChange={open => !open && setCreating(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Novo Item de Locação</DialogTitle>
          </DialogHeader>
          <RentableItemForm onSubmit={handleSubmit} onCancel={() => setCreating(false)} />
        </DialogContent>
      </Dialog>
    </div>
  )
}
