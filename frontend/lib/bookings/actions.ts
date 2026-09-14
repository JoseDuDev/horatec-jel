import { bookingsApi } from '@/lib/api/bookings'
import type { BookingAction, BookingActionOptions } from '@/components/bookings/BookingTable'

/**
 * Executa a ação de um agendamento/locação e devolve a mensagem a exibir, ou
 * `null` quando não há nada a dizer ao usuário.
 *
 * Compartilhado por `/admin/agendamentos` e pela aba **Reservas** de
 * `/admin/locacoes`: a devolução tem regra demais (destino do estorno, multa
 * por atraso) para viver em duas cópias que precisam ser mantidas iguais.
 */
export async function performBookingAction(
  action: BookingAction,
  id: string,
  opts?: BookingActionOptions
): Promise<string | null> {
  switch (action) {
    case 'confirm':
      await bookingsApi.confirm(id)
      return null
    case 'cancel':
      await bookingsApi.cancel(id)
      return null
    case 'complete':
      await bookingsApi.complete(id)
      return null
    case 'noshow':
      await bookingsApi.noShow(id)
      return null
    case 'pickup':
      await bookingsApi.rentalPickup(id)
      return null
    case 'return': {
      const r    = await bookingsApi.rentalReturn(id, opts?.refundToGateway ?? false)
      const dest = r.destination === 'Gateway' ? 'no cartão/PIX original' : 'na carteira'
      const fee  = r.lateFee > 0 ? ` Multa por atraso: R$ ${r.lateFee.toFixed(2)}.` : ''
      return `Devolução registrada. Caução de R$ ${r.depositRefunded.toFixed(2)} estornada ${dest}.${fee}`
    }
  }
}
