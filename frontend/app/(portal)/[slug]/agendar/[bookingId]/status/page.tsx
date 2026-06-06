'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { usePortalAuthStore } from '@/store/portal-auth'
import { portalApi } from '@/lib/api/portal'
import type { CustomerBooking } from '@/lib/types/portal'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { buttonVariants } from '@/components/ui/button'
import { CheckCircle, Clock, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

const STATUS_CONFIG = {
  Pending:   { label: 'Aguardando confirmação', icon: Clock,        variant: 'secondary' as const, color: 'text-amber-600' },
  Confirmed: { label: 'Confirmado',             icon: CheckCircle,  variant: 'default' as const,   color: 'text-green-600' },
  Completed: { label: 'Concluído',              icon: CheckCircle,  variant: 'outline' as const,   color: 'text-slate-600' },
  Cancelled: { label: 'Cancelado',              icon: XCircle,      variant: 'destructive' as const, color: 'text-red-600' },
  NoShow:    { label: 'Não compareceu',         icon: XCircle,      variant: 'destructive' as const, color: 'text-red-600' },
}

interface Props {
  params: { slug: string; bookingId: string }
}

export default function BookingStatusPage({ params }: Props) {
  const { slug, bookingId } = params
  const { accessToken } = usePortalAuthStore()
  const [booking, setBooking] = useState<CustomerBooking | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!accessToken) { setLoading(false); return }
    portalApi.myBookings(slug, accessToken)
      .then(bookings => setBooking(bookings.find(b => b.id === bookingId) ?? null))
      .finally(() => setLoading(false))
  }, [slug, bookingId, accessToken])

  if (loading) return <div className="max-w-lg mx-auto px-4 py-20 text-center text-slate-500">Carregando...</div>

  if (!booking) {
    return (
      <div className="max-w-lg mx-auto px-4 py-20 text-center">
        <p className="text-slate-500 mb-4">Agendamento não encontrado ou sessão expirada.</p>
        <Link href={`/${slug}`} className={cn(buttonVariants({ variant: 'outline' }))}>Voltar ao início</Link>
      </div>
    )
  }

  const config = STATUS_CONFIG[booking.status] ?? STATUS_CONFIG.Pending
  const Icon = config.icon

  return (
    <div className="max-w-lg mx-auto px-4 py-12">
      <div className="text-center mb-8">
        <Icon className={`h-16 w-16 mx-auto mb-4 ${config.color}`} />
        <h1 className="text-2xl font-bold mb-2">{config.label}</h1>
        <Badge variant={config.variant}>{booking.status}</Badge>
      </div>

      <Card>
        <CardHeader><CardTitle>Detalhes do agendamento</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {[
            { label: 'Serviço', value: booking.serviceName },
            { label: 'Profissional', value: booking.resourceName },
            { label: 'Data e hora', value: format(new Date(booking.scheduledAt), "dd 'de' MMMM 'às' HH:mm", { locale: ptBR }) },
            { label: 'Duração', value: `${booking.durationMinutes} min` },
            { label: 'Valor', value: `R$ ${booking.totalAmount.toFixed(2)}` },
          ].map(({ label, value }) => (
            <div key={label} className="flex justify-between text-sm">
              <span className="text-slate-500">{label}</span>
              <span className="font-medium">{value}</span>
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="flex gap-3 mt-6">
        <Link href={`/${slug}/minha-conta`} className={cn(buttonVariants({ variant: 'outline' }), 'flex-1 justify-center')}>Minha conta</Link>
        <Link href={`/${slug}/agendar`} className={cn(buttonVariants({ variant: 'default' }), 'flex-1 justify-center')}>Novo agendamento</Link>
      </div>
    </div>
  )
}
