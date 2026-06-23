'use client'

import { useCallback, useEffect, useState } from 'react'
import { format, addDays } from 'date-fns'
import { availabilityApi } from '@/lib/api/availability'
import type { AvailabilityExceptionDto } from '@/lib/types/availability'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Trash2 } from 'lucide-react'

interface Props {
  resources: Resource[]
}

interface ExceptionForm {
  date: string
  isBlocked: boolean
  customStart: string
  customEnd: string
  reason: string
}

export function ExceptionsEditor({ resources }: Props) {
  const [resourceId, setResourceId] = useState<string>('')
  const [exceptions, setExceptions] = useState<AvailabilityExceptionDto[]>([])
  const [form, setForm] = useState<ExceptionForm>({
    date: format(addDays(new Date(), 1), 'yyyy-MM-dd'),
    isBlocked: true,
    customStart: '09:00',
    customEnd: '12:00',
    reason: '',
  })
  const [saving, setSaving] = useState(false)

  const loadExceptions = useCallback((id: string) => {
    const from = format(new Date(), 'yyyy-MM-dd')
    const to = format(addDays(new Date(), 90), 'yyyy-MM-dd')
    availabilityApi.getResourceExceptions(id, from, to).then(setExceptions)
  }, [])

  useEffect(() => {
    if (resourceId) loadExceptions(resourceId)
  }, [resourceId, loadExceptions])

  const handleAdd = async () => {
    if (!resourceId) return
    setSaving(true)
    try {
      await availabilityApi.setResourceException(resourceId, {
        date: form.date,
        isBlocked: form.isBlocked,
        customStart: form.isBlocked ? undefined : `${form.customStart}:00`,
        customEnd: form.isBlocked ? undefined : `${form.customEnd}:00`,
        reason: form.reason || undefined,
      })
      loadExceptions(resourceId)
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (date: string) => {
    if (!resourceId) return
    await availabilityApi.deleteResourceException(resourceId, date)
    loadExceptions(resourceId)
  }

  return (
    <div className="space-y-8 max-w-xl">
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
          <div className="border rounded-lg p-4 space-y-4">
            <h3 className="font-medium text-sm text-slate-700">Nova exceção</h3>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Data</Label>
                <Input
                  type="date"
                  value={form.date}
                  min={format(new Date(), 'yyyy-MM-dd')}
                  onChange={e => setForm(f => ({ ...f, date: e.target.value }))}
                  className="mt-1"
                />
              </div>
              <div className="flex items-end pb-1">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={form.isBlocked}
                    onChange={e => setForm(f => ({ ...f, isBlocked: e.target.checked }))}
                    className="rounded"
                  />
                  <span className="text-sm font-medium">Dia bloqueado</span>
                </label>
              </div>
            </div>

            {!form.isBlocked && (
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Início</Label>
                  <Input
                    type="time"
                    value={form.customStart}
                    onChange={e => setForm(f => ({ ...f, customStart: e.target.value }))}
                    className="mt-1"
                  />
                </div>
                <div>
                  <Label>Fim</Label>
                  <Input
                    type="time"
                    value={form.customEnd}
                    onChange={e => setForm(f => ({ ...f, customEnd: e.target.value }))}
                    className="mt-1"
                  />
                </div>
              </div>
            )}

            <div>
              <Label>Motivo (opcional)</Label>
              <Input
                value={form.reason}
                onChange={e => setForm(f => ({ ...f, reason: e.target.value }))}
                placeholder="Ex: Feriado, folga, manutenção..."
                className="mt-1"
              />
            </div>

            <Button onClick={handleAdd} disabled={saving}>
              {saving ? 'Salvando...' : 'Adicionar exceção'}
            </Button>
          </div>

          <div className="space-y-3">
            <h3 className="font-medium text-sm text-slate-700">
              Exceções nos próximos 90 dias
            </h3>
            {exceptions.length === 0 ? (
              <p className="text-sm text-slate-400">Nenhuma exceção cadastrada.</p>
            ) : (
              exceptions.map(e => (
                <div
                  key={e.id}
                  className="flex items-center justify-between p-3 border rounded-lg"
                >
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{e.date}</span>
                      <Badge variant={e.isBlocked ? 'destructive' : 'secondary'}>
                        {e.isBlocked
                          ? 'Bloqueado'
                          : `${e.customStart?.slice(0, 5)} – ${e.customEnd?.slice(0, 5)}`}
                      </Badge>
                    </div>
                    {e.reason && (
                      <p className="text-xs text-slate-500">{e.reason}</p>
                    )}
                  </div>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => handleDelete(e.date)}
                  >
                    <Trash2 className="h-4 w-4 text-red-500" />
                  </Button>
                </div>
              ))
            )}
          </div>
        </>
      )}
    </div>
  )
}
