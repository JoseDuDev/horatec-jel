'use client'

import { useEffect, useState, useCallback } from 'react'
import { format, subDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { BookingTable } from '@/components/bookings/BookingTable'
import { AdminBookingModal } from '@/components/bookings/AdminBookingModal'
import type { Booking, BookingStatus } from '@/lib/types/booking'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Plus } from 'lucide-react'

export default function AgendamentosPage() {
  const [bookings, setBookings]   = useState<Booking[]>([])
  const [loading, setLoading]     = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [from, setFrom] = useState(format(subDays(new Date(), 7), 'yyyy-MM-dd'))
  const [to, setTo]     = useState(format(new Date(), 'yyyy-MM-dd'))
  const [status, setStatus] = useState<BookingStatus | ''>('')

  const load = useCallback(() => {
    setLoading(true)
    bookingsApi
      .list({
        from: `${from}T00:00:00`,
        to:   `${to}T23:59:59`,
        ...(status ? { status } : {}),
      })
      .then(setBookings)
      .finally(() => setLoading(false))
  }, [from, to, status])

  useEffect(() => { load() }, [load])

  const handleAction = async (
    action: 'confirm' | 'cancel' | 'complete' | 'noshow',
    id: string
  ) => {
    if (action === 'confirm')       await bookingsApi.confirm(id)
    else if (action === 'cancel')   await bookingsApi.cancel(id)
    else if (action === 'complete') await bookingsApi.complete(id)
    else if (action === 'noshow')   await bookingsApi.noShow(id)
    load()
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Agendamentos</h1>
        <Button onClick={() => setModalOpen(true)}>
          <Plus className="h-4 w-4 mr-2" /> Nova Reserva
        </Button>
      </div>

      <div className="flex gap-4 flex-wrap">
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
        <Select
          value={status}
          onValueChange={v => setStatus(v as BookingStatus | '')}
        >
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Todos os status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Todos</SelectItem>
            <SelectItem value="Pending">Pendente</SelectItem>
            <SelectItem value="Confirmed">Confirmado</SelectItem>
            <SelectItem value="Completed">Concluído</SelectItem>
            <SelectItem value="Cancelled">Cancelado</SelectItem>
            <SelectItem value="NoShow">Não Compareceu</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <BookingTable bookings={bookings} onAction={handleAction} />
      )}

      <AdminBookingModal
        open={modalOpen}
        onOpenChange={setModalOpen}
        onCreated={load}
      />
    </div>
  )
}
