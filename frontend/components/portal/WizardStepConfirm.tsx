'use client'

import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'

interface Props {
  service: Service
  resource: Resource
  slot: string
  notes: string
  onNotesChange: (v: string) => void
  onConfirm: () => void
  loading: boolean
}

export function WizardStepConfirm({ service, resource, slot, notes, onNotesChange, onConfirm, loading }: Props) {
  return (
    <div>
      <h2 className="text-xl font-bold mb-6">Confirme seu agendamento</h2>
      <div className="border rounded-lg p-6 space-y-4 mb-6">
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
        <div className="flex justify-between text-sm border-t pt-4">
          <span className="font-semibold">Total</span>
          <span className="font-bold text-lg">R$ {service.price.toFixed(2)}</span>
        </div>
      </div>

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
        {loading ? 'Agendando...' : 'Confirmar agendamento'}
      </Button>
    </div>
  )
}
