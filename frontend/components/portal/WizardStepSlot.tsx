'use client'

import { format, addDays, isSameDay } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { cn } from '@/lib/utils'

interface Props {
  slots: string[]
  loadingSlots: boolean
  selectedDate: Date
  selectedSlot: string | null
  onDateChange: (date: Date) => void
  onSlotSelect: (slot: string) => void
}

const DAYS_AHEAD = 14

export function WizardStepSlot({
  slots, loadingSlots, selectedDate, selectedSlot, onDateChange, onSlotSelect
}: Props) {
  const today = new Date()
  const days = Array.from({ length: DAYS_AHEAD }, (_, i) => addDays(today, i))

  return (
    <div>
      <h2 className="text-xl font-bold mb-4">Escolha a data e horário</h2>

      {/* Date strip */}
      <div className="flex gap-2 overflow-x-auto pb-2 mb-6">
        {days.map(d => (
          <button
            key={d.toISOString()}
            type="button"
            onClick={() => onDateChange(d)}
            className={cn(
              'flex flex-col items-center border rounded-lg px-3 py-2 min-w-[60px] text-sm shrink-0 transition-all',
              isSameDay(d, selectedDate)
                ? 'border-indigo-600 bg-indigo-50 text-indigo-700'
                : 'border-slate-200 hover:border-slate-400'
            )}
          >
            <span className="text-xs text-slate-500">{format(d, 'EEE', { locale: ptBR })}</span>
            <span className="font-bold">{format(d, 'dd')}</span>
            <span className="text-xs text-slate-500">{format(d, 'MMM', { locale: ptBR })}</span>
          </button>
        ))}
      </div>

      {/* Slots */}
      {loadingSlots ? (
        <p className="text-slate-500 text-sm">Buscando horários...</p>
      ) : slots.length === 0 ? (
        <p className="text-slate-500 text-sm">Sem horários disponíveis nesta data.</p>
      ) : (
        <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
          {slots.map(slot => {
            const d = new Date(slot)
            const time = `${String(d.getUTCHours()).padStart(2, '0')}:${String(d.getUTCMinutes()).padStart(2, '0')}`
            return (
              <button
                key={slot}
                type="button"
                onClick={() => onSlotSelect(slot)}
                className={cn(
                  'border rounded-lg py-2 text-sm font-medium transition-all',
                  selectedSlot === slot
                    ? 'border-indigo-600 bg-indigo-600 text-white'
                    : 'border-slate-200 hover:border-indigo-400'
                )}
              >
                {time}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
