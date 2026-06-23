'use client'

import { useEffect, useState } from 'react'
import { availabilityApi } from '@/lib/api/availability'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const DAY_LABELS = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado']

interface DayRow {
  dayOfWeek: number
  enabled: boolean
  startTime: string
  endTime: string
  slotDurationMinutes: number
  breakAfterMinutes: number
}

const DEFAULT_ROWS: DayRow[] = Array.from({ length: 7 }, (_, i) => ({
  dayOfWeek: i,
  enabled: i >= 1 && i <= 5,
  startTime: '09:00',
  endTime: '18:00',
  slotDurationMinutes: 60,
  breakAfterMinutes: 0,
}))

interface Props {
  resources: Resource[]
}

export function ResourceRulesEditor({ resources }: Props) {
  const [resourceId, setResourceId] = useState<string>('')
  const [rows, setRows] = useState<DayRow[]>(DEFAULT_ROWS)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    if (!resourceId) return
    availabilityApi.getResourceRules(resourceId).then(rules => {
      setRows(
        DEFAULT_ROWS.map(d => {
          const rule = rules.find(r => r.dayOfWeek === d.dayOfWeek)
          if (!rule) return { ...d, enabled: false }
          return {
            dayOfWeek: d.dayOfWeek,
            enabled: true,
            startTime: rule.startTime.slice(0, 5),
            endTime: rule.endTime.slice(0, 5),
            slotDurationMinutes: rule.slotDurationMinutes,
            breakAfterMinutes: rule.breakAfterMinutes,
          }
        })
      )
    })
  }, [resourceId])

  const update = (
    dayOfWeek: number,
    field: keyof DayRow,
    value: string | boolean | number
  ) => {
    setRows(r =>
      r.map(d => (d.dayOfWeek === dayOfWeek ? { ...d, [field]: value } : d))
    )
  }

  const handleSave = async () => {
    if (!resourceId) return
    setSaving(true)
    try {
      await Promise.all(
        rows
          .filter(d => d.enabled)
          .map(d =>
            availabilityApi.setResourceRule(resourceId, {
              dayOfWeek: d.dayOfWeek,
              startTime: `${d.startTime}:00`,
              endTime: `${d.endTime}:00`,
              slotDurationMinutes: d.slotDurationMinutes,
              breakAfterMinutes: d.breakAfterMinutes,
            })
          )
      )
      setSaved(true)
      setTimeout(() => setSaved(false), 3000)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="space-y-6 max-w-3xl">
      <div className="max-w-xs">
        <Label>Recurso</Label>
        <Select value={resourceId} onValueChange={v => setResourceId(v ?? '')}>
          <SelectTrigger className="mt-1">
            {/* base-ui exibe o valor cru (id) por padrão; mapeamos para o nome do recurso. */}
            <SelectValue placeholder="Selecione um recurso...">
              {(value) => resources.find(r => r.id === value)?.name ?? 'Selecione um recurso...'}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {resources.map(r => (
              <SelectItem key={r.id} value={r.id}>
                {r.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {resourceId && (
        <>
          <div className="space-y-2">
            <div className="grid grid-cols-[9rem_1fr_1fr_6rem_6rem] gap-2 text-xs text-slate-500 font-medium px-1">
              <span>Dia</span>
              <span>Início</span>
              <span>Fim</span>
              <span>Slot (min)</span>
              <span>Break (min)</span>
            </div>
            {rows.map(d => (
              <div
                key={d.dayOfWeek}
                className="grid grid-cols-[9rem_1fr_1fr_6rem_6rem] gap-2 items-center"
              >
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={d.enabled}
                    onChange={e => update(d.dayOfWeek, 'enabled', e.target.checked)}
                    className="rounded"
                  />
                  <span className="text-sm font-medium">{DAY_LABELS[d.dayOfWeek]}</span>
                </label>
                {d.enabled ? (
                  <>
                    <Input
                      type="time"
                      value={d.startTime}
                      onChange={e => update(d.dayOfWeek, 'startTime', e.target.value)}
                    />
                    <Input
                      type="time"
                      value={d.endTime}
                      onChange={e => update(d.dayOfWeek, 'endTime', e.target.value)}
                    />
                    <Input
                      type="number"
                      min={5}
                      max={480}
                      value={d.slotDurationMinutes}
                      onChange={e =>
                        update(d.dayOfWeek, 'slotDurationMinutes', parseInt(e.target.value, 10) || 60)
                      }
                    />
                    <Input
                      type="number"
                      min={0}
                      max={120}
                      value={d.breakAfterMinutes}
                      onChange={e =>
                        update(d.dayOfWeek, 'breakAfterMinutes', parseInt(e.target.value, 10) || 0)
                      }
                    />
                  </>
                ) : (
                  <span className="text-sm text-slate-400 col-span-4">Sem atendimento</span>
                )}
              </div>
            ))}
          </div>

          <div className="flex items-center gap-4">
            <Button onClick={handleSave} disabled={saving}>
              {saving ? 'Salvando...' : 'Salvar grade'}
            </Button>
            {saved && <span className="text-sm text-green-600">Salvo!</span>}
          </div>
        </>
      )}
    </div>
  )
}
