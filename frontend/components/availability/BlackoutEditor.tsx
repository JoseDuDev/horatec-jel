'use client'

import { useCallback, useEffect, useState } from 'react'
import { format, addDays } from 'date-fns'
import { availabilityApi } from '@/lib/api/availability'
import type { BlackoutDateDto } from '@/lib/types/availability'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Trash2 } from 'lucide-react'

const currentYear = new Date().getFullYear()

export function BlackoutEditor() {
  const [year, setYear] = useState(currentYear)
  const [blackouts, setBlackouts] = useState<BlackoutDateDto[]>([])
  const [date, setDate] = useState(format(addDays(new Date(), 1), 'yyyy-MM-dd'))
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback((y: number) => {
    availabilityApi.getBlackouts(y)
      .then(list => setBlackouts([...list].sort((a, b) => a.date.localeCompare(b.date))))
      .catch(e => setError(e instanceof Error ? e.message : 'Falha ao carregar'))
  }, [])

  useEffect(() => { load(year) }, [year, load])

  const handleAdd = async () => {
    setError(null)
    setSaving(true)
    try {
      await availabilityApi.createBlackout({ date, reason: reason || undefined })
      setReason('')
      load(year)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Falha ao adicionar')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (d: string) => {
    setError(null)
    try {
      await availabilityApi.deleteBlackout(d)
      load(year)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Falha ao remover')
    }
  }

  return (
    <div className="space-y-8 max-w-xl">
      <div className="border rounded-lg p-4 space-y-4">
        <h3 className="font-medium text-sm text-slate-700">Novo bloqueio</h3>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <Label>Data</Label>
            <Input
              type="date"
              value={date}
              onChange={e => setDate(e.target.value)}
              className="mt-1"
            />
          </div>
          <div>
            <Label>Motivo (opcional)</Label>
            <Input
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder="Ex: Feriado, recesso..."
              className="mt-1"
            />
          </div>
        </div>
        <Button onClick={handleAdd} disabled={saving}>
          {saving ? 'Salvando...' : 'Adicionar bloqueio'}
        </Button>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="font-medium text-sm text-slate-700">Bloqueios de {year}</h3>
          <div className="flex items-center gap-2">
            <Button size="sm" variant="ghost" onClick={() => setYear(y => y - 1)}>◀</Button>
            <span className="text-sm font-medium w-12 text-center">{year}</span>
            <Button size="sm" variant="ghost" onClick={() => setYear(y => y + 1)}>▶</Button>
          </div>
        </div>
        {blackouts.length === 0 ? (
          <p className="text-sm text-slate-400">Nenhum bloqueio cadastrado para {year}.</p>
        ) : (
          blackouts.map(b => (
            <div
              key={b.id}
              className="flex items-center justify-between p-3 border rounded-lg"
            >
              <div className="space-y-1">
                <span className="text-sm font-medium">
                  {format(new Date(`${b.date}T00:00:00`), 'dd/MM/yyyy')}
                </span>
                {b.reason && <p className="text-xs text-slate-500">{b.reason}</p>}
              </div>
              <Button size="sm" variant="ghost" onClick={() => handleDelete(b.date)}>
                <Trash2 className="h-4 w-4 text-red-500" />
              </Button>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
