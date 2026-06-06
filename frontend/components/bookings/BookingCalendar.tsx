'use client'

import { useState } from 'react'
import { format, addDays, startOfWeek, isSameDay } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Booking } from '@/lib/types/booking'
import { Button } from '@/components/ui/button'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

type View = 'dia' | 'semana'

const HOURS = Array.from({ length: 24 }, (_, i) => i) // 00:00 – 23:00

interface Props {
  bookings: Booking[]
  onMove: (bookingId: string, newStart: string) => void
}

export function BookingCalendar({ bookings }: Props) {
  const [view, setView] = useState<View>('dia')
  const [current, setCurrent] = useState(new Date())

  const days = view === 'dia'
    ? [current]
    : Array.from({ length: 7 }, (_, i) => addDays(startOfWeek(current, { weekStartsOn: 1 }), i))

  const getBookingsForDayAndHour = (day: Date, hour: number) =>
    bookings.filter(b => {
      const d = new Date(b.scheduledAt)
      return isSameDay(d, day) && d.getHours() === hour
    })

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4">
        <Button variant="outline" size="sm" onClick={() => setView('dia')}
          className={cn(view === 'dia' && 'bg-slate-100')}>Dia</Button>
        <Button variant="outline" size="sm" onClick={() => setView('semana')}
          className={cn(view === 'semana' && 'bg-slate-100')}>Semana</Button>
        <div className="flex items-center gap-2 ml-4">
          <Button variant="outline" size="icon" onClick={() => setCurrent(d => addDays(d, -1))}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-sm font-medium w-40 text-center">
            {format(current, view === 'dia' ? "dd 'de' MMMM" : "'Semana de' dd/MM", { locale: ptBR })}
          </span>
          <Button variant="outline" size="icon" onClick={() => setCurrent(d => addDays(d, 1))}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <div className="border rounded-lg overflow-auto">
        <div className={cn('grid', view === 'semana' ? 'grid-cols-8' : 'grid-cols-2')}>
          <div className="border-r border-b p-2 text-xs text-slate-400">Hora</div>
          {days.map(d => (
            <div key={d.toISOString()} className="border-r border-b p-2 text-xs font-medium text-center">
              {format(d, 'EEE dd/MM', { locale: ptBR })}
            </div>
          ))}

          {HOURS.map(hour => (
            <>
              <div key={`h-${hour}`} className="border-r border-b p-2 text-xs text-slate-400">
                {String(hour).padStart(2, '0')}:00
              </div>
              {days.map(d => {
                const dayBookings = getBookingsForDayAndHour(d, hour)
                return (
                  <div key={`${d.toISOString()}-${hour}`} className="border-r border-b p-1 min-h-[48px]">
                    {dayBookings.map(b => (
                      <div
                        key={b.id}
                        className="text-xs bg-indigo-100 text-indigo-800 rounded p-1 mb-1 truncate"
                        title={`${b.customerName} — ${b.serviceName}`}
                      >
                        <div className="font-medium">{b.customerName}</div>
                        <div>{b.serviceName}</div>
                      </div>
                    ))}
                  </div>
                )
              })}
            </>
          ))}
        </div>
      </div>
    </div>
  )
}
