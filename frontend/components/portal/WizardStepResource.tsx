import type { Resource } from '@/lib/types/resource'
import { cn } from '@/lib/utils'

interface Props {
  resources: Resource[]
  selectedId: string | null
  onSelect: (id: string) => void
}

export function WizardStepResource({ resources, selectedId, onSelect }: Props) {
  return (
    <div>
      <h2 className="text-xl font-bold mb-4">Escolha o profissional/recurso</h2>
      {resources.length === 0 ? (
        <p className="text-slate-500">Nenhum recurso disponível para este serviço.</p>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          {resources.map(r => (
            <button
              key={r.id}
              type="button"
              onClick={() => onSelect(r.id)}
              className={cn(
                'text-left border rounded-lg p-4 flex items-center gap-3 transition-all hover:border-indigo-400',
                selectedId === r.id
                  ? 'border-indigo-600 bg-indigo-50 ring-2 ring-indigo-300'
                  : 'border-slate-200'
              )}
            >
              <div className="h-10 w-10 rounded-full bg-slate-200 flex items-center justify-center font-bold text-slate-600 shrink-0">
                {r.name[0]}
              </div>
              <div>
                <p className="font-medium">{r.name}</p>
                <p className="text-xs text-slate-500">{r.type}</p>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
