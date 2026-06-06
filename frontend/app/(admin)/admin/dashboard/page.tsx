'use client'

import { useEffect, useState } from 'react'
import { format, startOfDay, endOfDay, startOfWeek, endOfWeek } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { financeiroApi } from '@/lib/api/financeiro'
import type { FinancialSummary } from '@/lib/types/financeiro'
import type { Booking } from '@/lib/types/booking'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { CalendarDays, DollarSign, XCircle, CheckCircle } from 'lucide-react'

export default function DashboardPage() {
  const [todayBookings, setTodayBookings] = useState<Booking[]>([])
  const [weekBookings, setWeekBookings] = useState<Booking[]>([])
  const [summary, setSummary] = useState<FinancialSummary | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const today = new Date()
    const from = format(startOfWeek(today), "yyyy-MM-dd'T'HH:mm:ss")
    const to = format(endOfWeek(today), "yyyy-MM-dd'T'HH:mm:ss")
    const todayFrom = format(startOfDay(today), "yyyy-MM-dd'T'HH:mm:ss")
    const todayTo = format(endOfDay(today), "yyyy-MM-dd'T'HH:mm:ss")

    Promise.all([
      bookingsApi.list({ from: todayFrom, to: todayTo }),
      bookingsApi.list({ from, to }),
      financeiroApi.summary({ from, to }),
    ]).then(([tb, wb, s]) => {
      setTodayBookings(tb)
      setWeekBookings(wb)
      setSummary(s)
    }).finally(() => setLoading(false))
  }, [])

  const cancelled = weekBookings.filter(b => b.status === 'Cancelled').length

  const metrics = [
    {
      title: 'Agendamentos Hoje',
      value: loading ? '...' : todayBookings.length,
      icon: CalendarDays,
      sub: `${weekBookings.length} esta semana`,
    },
    {
      title: 'Receita (semana)',
      value: loading ? '...' : `R$ ${(summary?.netRevenue ?? 0).toFixed(2)}`,
      icon: DollarSign,
      sub: `Bruto: R$ ${(summary?.totalRevenue ?? 0).toFixed(2)}`,
    },
    {
      title: 'Cancelamentos (semana)',
      value: loading ? '...' : cancelled,
      icon: XCircle,
      sub: `${weekBookings.length} total`,
    },
    {
      title: 'Pagamentos Confirmados',
      value: loading ? '...' : summary?.paidBookings ?? 0,
      icon: CheckCircle,
      sub: `de ${summary?.totalBookings ?? 0} agendamentos`,
    },
  ]

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Dashboard</h1>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {metrics.map(({ title, value, icon: Icon, sub }) => (
          <Card key={title}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-slate-600">{title}</CardTitle>
              <Icon className="h-4 w-4 text-slate-400" />
            </CardHeader>
            <CardContent>
              <p className="text-2xl font-bold">{value}</p>
              <p className="text-xs text-slate-500 mt-1">{sub}</p>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
