'use client'

import { useState } from 'react'
import type { OnboardingHoursData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const DAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb']

const DEFAULT_SCHEDULE = DAYS.map((_, i) => ({
  dayOfWeek: i,
  isOpen: i >= 1 && i <= 6,
  openTime: '09:00:00',
  closeTime: '18:00:00',
}))

interface Props {
  onFinish: (data: OnboardingHoursData) => void
  onBack: () => void
  loading: boolean
}

export function OnboardingStepHours({ onFinish, onBack, loading }: Props) {
  const [schedule, setSchedule] = useState(DEFAULT_SCHEDULE)

  const update = (index: number, field: string, value: string | boolean) => {
    setSchedule(s => s.map((day, i) => i === index ? { ...day, [field]: value } : day))
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Horários de funcionamento</h2>
        <p className="text-sm text-slate-500 mb-6">Defina quando seu negócio está aberto.</p>
      </div>

      <div className="space-y-3">
        {schedule.map((day, i) => (
          <div key={day.dayOfWeek} className="flex items-center gap-3">
            <label className="flex items-center gap-2 w-24 shrink-0 cursor-pointer">
              <input
                type="checkbox"
                checked={day.isOpen}
                onChange={e => update(i, 'isOpen', e.target.checked)}
                className="rounded"
              />
              <span className="text-sm font-medium">{DAYS[day.dayOfWeek]}</span>
            </label>
            {day.isOpen ? (
              <>
                <Input
                  type="time"
                  value={day.openTime.slice(0, 5)}
                  onChange={e => update(i, 'openTime', `${e.target.value}:00`)}
                  className="w-28"
                />
                <span className="text-slate-400 text-sm">até</span>
                <Input
                  type="time"
                  value={day.closeTime.slice(0, 5)}
                  onChange={e => update(i, 'closeTime', `${e.target.value}:00`)}
                  className="w-28"
                />
              </>
            ) : (
              <span className="text-sm text-slate-400">Fechado</span>
            )}
          </div>
        ))}
      </div>

      <div className="flex gap-3">
        <Button variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button
          onClick={() => onFinish({ schedule })}
          disabled={loading}
          className="flex-1"
        >
          {loading ? 'Salvando...' : 'Concluir ✓'}
        </Button>
      </div>
    </div>
  )
}
