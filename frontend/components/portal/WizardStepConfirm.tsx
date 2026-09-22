'use client'

import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'

interface Props {
  slug: string
  service: Service
  resource: Resource
  slot: string
  notes: string
  onNotesChange: (v: string) => void
  onConfirm: () => void
  loading: boolean
}

/**
 * Passo final do agendamento. **Não cobra nada**: o portal registra a reserva e o
 * acerto acontece no balcão. O gateway do Mercado Pago é global (um único
 * AccessToken da plataforma, sem credencial por tenant), então cobrar aqui levaria
 * o dinheiro do cliente do lojista para a conta da plataforma.
 *
 * Por isso saíram daqui o cupom e os créditos da carteira: sem cobrança, não há
 * no que aplicar desconto.
 */
export function WizardStepConfirm({
  service, resource, slot, notes, onNotesChange, onConfirm, loading,
}: Props) {
  const brl = (v: number) => `R$ ${v.toFixed(2).replace('.', ',')}`

  return (
    <div>
      <h2 className="text-xl font-bold mb-6">Confirme seu agendamento</h2>

      {/* Resumo */}
      <div className="border rounded-lg p-6 space-y-3 mb-6">
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Serviço</span>
          <span className="font-medium">{service.name}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Profissional</span>
          <span className="font-medium">{resource.name}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Data e hora</span>
          <span className="font-medium">
            {format(new Date(slot), "dd 'de' MMMM 'às' HH:mm", { locale: ptBR })}
          </span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Duração</span>
          <span className="font-medium">{service.durationMinutes} min</span>
        </div>

        <div className="border-t pt-3">
          <div className="flex justify-between font-bold text-base">
            <span>Valor</span>
            <span>{brl(service.price)}</span>
          </div>
          <p className="text-xs text-slate-500 mt-1">
            O pagamento é feito no local, no dia do atendimento.
          </p>
        </div>
      </div>

      {/* Observações */}
      <div className="mb-6">
        <label className="block text-sm font-medium text-slate-700 mb-1">
          Observações (opcional)
        </label>
        <textarea
          value={notes}
          onChange={e => onNotesChange(e.target.value)}
          placeholder="Alguma preferência ou observação?"
          className="w-full border rounded-lg px-3 py-2 text-sm min-h-[80px]"
        />
      </div>

      <Button onClick={onConfirm} disabled={loading} size="lg" className="w-full">
        {loading ? 'Confirmando...' : 'Confirmar agendamento'}
      </Button>
    </div>
  )
}
