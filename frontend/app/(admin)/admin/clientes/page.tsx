'use client'

import { useEffect, useState } from 'react'
import { format, subDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import type { Booking } from '@/lib/types/booking'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table'
import { Input } from '@/components/ui/input'

interface CustomerSummary {
  customerId: string
  customerName: string
  customerEmail: string
  bookingCount: number
  completedCount: number
  totalSpent: number
  lastVisit: string
}

function buildCustomerSummaries(bookings: Booking[]): CustomerSummary[] {
  const map = new Map<string, CustomerSummary>()
  for (const b of bookings) {
    const existing = map.get(b.customerId)
    if (existing) {
      existing.bookingCount++
      if (b.status === 'Completed') {
        existing.completedCount++
        existing.totalSpent += b.totalAmount
      }
      if (b.scheduledAt > existing.lastVisit) existing.lastVisit = b.scheduledAt
    } else {
      map.set(b.customerId, {
        customerId: b.customerId,
        customerName: b.customerName,
        customerEmail: b.customerEmail,
        bookingCount: 1,
        completedCount: b.status === 'Completed' ? 1 : 0,
        totalSpent: b.status === 'Completed' ? b.totalAmount : 0,
        lastVisit: b.scheduledAt,
      })
    }
  }
  return Array.from(map.values()).sort((a, b) => b.totalSpent - a.totalSpent)
}

export default function ClientesPage() {
  const [summaries, setSummaries] = useState<CustomerSummary[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const from = format(subDays(new Date(), 365), "yyyy-MM-dd'T'HH:mm:ss")
    const to = format(new Date(), "yyyy-MM-dd'T'HH:mm:ss")
    bookingsApi.list({ from, to }).then(bookings => {
      setSummaries(buildCustomerSummaries(bookings))
    }).finally(() => setLoading(false))
  }, [])

  const filtered = summaries.filter(
    s => s.customerName.toLowerCase().includes(search.toLowerCase()) ||
         s.customerEmail.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Clientes</h1>
      <Input
        placeholder="Buscar por nome ou email..."
        value={search}
        onChange={e => setSearch(e.target.value)}
        className="max-w-xs"
      />
      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Cliente</TableHead>
              <TableHead>Agendamentos</TableHead>
              <TableHead>Concluídos</TableHead>
              <TableHead>Valor Gasto</TableHead>
              <TableHead>Última Visita</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.map(c => (
              <TableRow key={c.customerId}>
                <TableCell>
                  <div className="font-medium">{c.customerName}</div>
                  <div className="text-xs text-slate-500">{c.customerEmail}</div>
                </TableCell>
                <TableCell>{c.bookingCount}</TableCell>
                <TableCell>{c.completedCount}</TableCell>
                <TableCell>R$ {c.totalSpent.toFixed(2)}</TableCell>
                <TableCell>{format(new Date(c.lastVisit), 'dd/MM/yyyy')}</TableCell>
              </TableRow>
            ))}
            {filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center py-8 text-slate-500">
                  Nenhum cliente encontrado.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
