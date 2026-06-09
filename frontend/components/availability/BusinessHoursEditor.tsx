'use client'

import { useEffect, useState } from 'react'
import { availabilityApi } from '@/lib/api/availability'
import type { BusinessHoursDto } from '@/lib/types/availability'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const DAY_LABELS = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado']

export function BusinessHoursEditor() {
  const [schedule, setSchedule] = useState<BusinessHoursDto[]>([])
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    availabilityApi.getBusinessHours().then(setSchedule)
  }, [])

  const update = (
    dayOfWeek: number,
    field: keyof BusinessHoursDto,
    value: string | boolean
  ) => {
    setSchedule(s =>
      s.map(d => (d.dayOfWeek === dayOfWeek ? { ...d, [field]: value } : d))
    )
  }

  const handleSave = async () => {
    setSaving(true)
    try {
      await Promise.all(
        schedule.map(d =>
          availabilityApi.setBusinessHours(d.dayOfWeek, d.isOpen, d.openTime, d.closeTime)
        )
      )
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-4 max-w-lg">
      {schedule.map(d => (
        <div key={d.dayOfWeek} className="flex items-center gap-3">
          <label className="flex items-center gap-2 w-28 shrink-0 cursor-pointer">
            <input
              type="checkbox"
              checked={d.isOpen}
              onChange={e => update(d.dayOfWeek, 'isOpen', e.target.checked)}
              className="rounded"
            />
            <span className="text-sm font-medium">{DAY_LABELS[d.dayOfWeek]}</span>
          </label>
          {d.isOpen ? (
            <>
              <Input
                type="time"
                value={d.openTime.slice(0, 5)}
                onChange={e => update(d.dayOfWeek, 'openTime', `${e.target.value}:00`)}
                className="w-28"
              />
              <span className="text-slate-400 text-sm">até</span>
              <Input
                type="time"
                value={d.closeTime.slice(0, 5)}
                onChange={e => update(d.dayOfWeek, 'closeTime', `${e.target.value}:00`)}
                className="w-28"
              />
            </>
          ) : (
            <span className="text-sm text-slate-400">Fechado</span>
          )}
        </div>
      ))}
      <div className="flex items-center gap-4 pt-2">
        <Button onClick={handleSave} disabled={saving || schedule.length === 0}>
          {saving ? 'Salvando...' : 'Salvar'}
        </Button>
        {saved && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
      </div>
    </div>
  )
}
