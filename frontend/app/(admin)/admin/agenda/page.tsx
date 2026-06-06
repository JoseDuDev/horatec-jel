'use client'

import { useEffect, useState } from 'react'
import { format, startOfWeek, endOfWeek, addDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { BookingCalendar } from '@/components/bookings/BookingCalendar'
import type { Booking } from '@/lib/types/booking'

export default function AgendaPage() {
  const [bookings, setBookings] = useState<Booking[]>([])

  useEffect(() => {
    const today = new Date()
    const from = format(addDays(startOfWeek(today, { weekStartsOn: 1 }), -7), "yyyy-MM-dd'T'HH:mm:ss")
    const to = format(addDays(endOfWeek(today, { weekStartsOn: 1 }), 14), "yyyy-MM-dd'T'HH:mm:ss")
    bookingsApi.list({ from, to }).then(setBookings)
  }, [])

  const handleMove = async (bookingId: string, _newStart: string) => {
    console.log('Move booking', bookingId, 'to', _newStart)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Agenda</h1>
      <BookingCalendar bookings={bookings} onMove={handleMove} />
    </div>
  )
}
