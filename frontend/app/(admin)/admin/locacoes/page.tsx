'use client'

import { useCallback, useEffect, useState } from 'react'
import { addDays, format, subDays } from 'date-fns'
import { Plus } from 'lucide-react'

import { rentalsApi } from '@/lib/api/rentals'
import { bookingsApi } from '@/lib/api/bookings'
import { performBookingAction } from '@/lib/bookings/actions'
import { RentableItemForm } from '@/components/rentals/RentableItemForm'
import {
  BookingTable,
  type BookingAction,
  type BookingActionOptions,
} from '@/components/bookings/BookingTable'
import type { RentableItem, CreateRentableItemRequest } from '@/lib/types/rental'
import type { Booking } from '@/lib/types/booking'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

/**
 * Locações = **Reservas** (o dia a dia: retirar, devolver, estornar a caução) e
 * **Itens** (o cadastro do catálogo).
 *
 * As duas abas moram aqui porque o tenant que contratou só `Rentals` não vê o
 * menu Agendamentos — ele exige a capability `Appointments` (ver `Sidebar.tsx`).
 * Antes disso, uma locadora conseguia cadastrar itens e receber reservas pelo
 * portal, mas não tinha por onde registrar que o item saiu e voltou.
 */
export default function LocacoesPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Locações</h1>

      <Tabs defaultValue="reservas">
        <TabsList>
          <TabsTrigger value="reservas">Reservas</TabsTrigger>
          <TabsTrigger value="itens">Itens</TabsTrigger>
        </TabsList>

        <TabsContent value="reservas" className="mt-4">
          <ReservasTab />
        </TabsContent>

        <TabsContent value="itens" className="mt-4">
          <ItensTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function ReservasTab() {
  const [bookings, setBookings] = useState<Booking[]>([])
  const [loading, setLoading]   = useState(true)
  // Locação olha para a frente: o que importa é a reserva do próximo fim de
  // semana e o que ainda não voltou. Por isso a janela padrão vai até +30 dias,
  // ao contrário de /admin/agendamentos, que termina hoje.
  const [from, setFrom] = useState(format(subDays(new Date(), 7), 'yyyy-MM-dd'))
  const [to, setTo]     = useState(format(addDays(new Date(), 30), 'yyyy-MM-dd'))

  const load = useCallback(() => {
    setLoading(true)
    bookingsApi
      .list({ from: `${from}T00:00:00`, to: `${to}T23:59:59` })
      .then(all => setBookings(all.filter(b => b.kind === 'Rental')))
      .catch(() => setBookings([]))
      .finally(() => setLoading(false))
  }, [from, to])

  useEffect(() => { load() }, [load])

  const handleAction = async (
    action: BookingAction,
    id: string,
    opts?: BookingActionOptions
  ) => {
    const message = await performBookingAction(action, id, opts)
    if (message) alert(message)
    load()
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-4 flex-wrap items-center">
        <Input
          type="date"
          value={from}
          onChange={e => setFrom(e.target.value)}
          className="w-40"
        />
        <Input
          type="date"
          value={to}
          onChange={e => setTo(e.target.value)}
          className="w-40"
        />
        <p className="text-sm text-slate-500">
          Reservas do período — retirada, devolução e estorno da caução.
        </p>
      </div>

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <BookingTable bookings={bookings} onAction={handleAction} />
      )}

      <p className="text-xs text-slate-400">
        As reservas chegam pelo seu link público. Para reservar no balcão, abra o
        próprio link e faça a reserva pelo cliente.
      </p>
    </div>
  )
}

function ItensTab() {
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
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">
          O catálogo que aparece no seu link público.
        </p>
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
