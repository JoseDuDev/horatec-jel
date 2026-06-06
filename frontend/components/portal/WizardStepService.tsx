import type { Service } from '@/lib/types/service'
import { cn } from '@/lib/utils'
import { Clock, DollarSign } from 'lucide-react'

interface Props {
  services: Service[]
  selectedId: string | null
  onSelect: (id: string) => void
}

export function WizardStepService({ services, selectedId, onSelect }: Props) {
  return (
    <div>
      <h2 className="text-xl font-bold mb-4">Escolha o serviço</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        {services.map(s => (
          <button
            key={s.id}
            type="button"
            onClick={() => onSelect(s.id)}
            className={cn(
              'text-left border rounded-lg p-4 transition-all hover:border-indigo-400',
              selectedId === s.id
                ? 'border-indigo-600 bg-indigo-50 ring-2 ring-indigo-300'
                : 'border-slate-200'
            )}
          >
            <p className="font-medium mb-1">{s.name}</p>
            {s.description && <p className="text-xs text-slate-500 mb-2">{s.description}</p>}
            <div className="flex gap-4 text-sm text-slate-600">
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" /> {s.durationMinutes} min
              </span>
              <span className="flex items-center gap-1">
                <DollarSign className="h-3 w-3" /> R$ {s.price.toFixed(2)}
              </span>
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}
