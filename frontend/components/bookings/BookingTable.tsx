'use client'

import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Booking, BookingStatus } from '@/lib/types/booking'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

const STATUS_LABEL: Record<BookingStatus, string> = {
  Pending: 'Pendente',
  Confirmed: 'Confirmado',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  NoShow: 'Não Compareceu',
}

const STATUS_VARIANT: Record<BookingStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'secondary',
  Confirmed: 'default',
  Completed: 'outline',
  Cancelled: 'destructive',
  NoShow: 'destructive',
}

interface Props {
  bookings: Booking[]
  onAction: (action: 'confirm' | 'cancel' | 'complete' | 'noshow', id: string) => void
}

export function BookingTable({ bookings, onAction }: Props) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Cliente</TableHead>
          <TableHead>Serviço</TableHead>
          <TableHead>Recurso</TableHead>
          <TableHead>Data/Hora</TableHead>
          <TableHead>Status</TableHead>
          <TableHead>Valor</TableHead>
          <TableHead>Ações</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {bookings.map(b => (
          <TableRow key={b.id}>
            <TableCell>
              <div className="font-medium">{b.customerName}</div>
              <div className="text-xs text-slate-500">{b.customerEmail}</div>
            </TableCell>
            <TableCell>{b.serviceName}</TableCell>
            <TableCell>{b.resourceName}</TableCell>
            <TableCell>
              {format(new Date(b.scheduledAt), "dd/MM/yyyy HH:mm", { locale: ptBR })}
            </TableCell>
            <TableCell>
              <Badge variant={STATUS_VARIANT[b.status]}>{STATUS_LABEL[b.status]}</Badge>
            </TableCell>
            <TableCell>R$ {b.totalAmount.toFixed(2)}</TableCell>
            <TableCell>
              <div className="flex gap-2">
                {b.status === 'Pending' && (
                  <Button size="sm" onClick={() => onAction('confirm', b.id)}>
                    Confirmar
                  </Button>
                )}
                {(b.status === 'Pending' || b.status === 'Confirmed') && (
                  <Button size="sm" variant="outline" onClick={() => onAction('cancel', b.id)}>
                    Cancelar
                  </Button>
                )}
                {b.status === 'Confirmed' && (
                  <Button size="sm" variant="secondary" onClick={() => onAction('complete', b.id)}>
                    Concluir
                  </Button>
                )}
              </div>
            </TableCell>
          </TableRow>
        ))}
        {bookings.length === 0 && (
          <TableRow>
            <TableCell colSpan={7} className="text-center text-slate-500 py-8">
              Nenhum agendamento encontrado.
            </TableCell>
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}
