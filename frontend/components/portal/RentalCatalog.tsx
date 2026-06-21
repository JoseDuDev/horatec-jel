'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import type { RentableItem, RentalAvailability } from '@/lib/types/rental'
import { portalApi } from '@/lib/api/portal'
import { usePortalAuthStore } from '@/store/portal-auth'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'

const brl = (v: number) => `R$ ${v.toFixed(2).replace('.', ',')}`
const today = () => new Date().toISOString().slice(0, 10)
const daysBetween = (start: string, end: string) =>
  start && end ? Math.round((new Date(end).getTime() - new Date(start).getTime()) / 86_400_000) : 0

interface Props {
  slug: string
  items: RentableItem[]
}

export function RentalCatalog({ slug, items }: Props) {
  const router = useRouter()
  const { accessToken, customer } = usePortalAuthStore()

  const [selected, setSelected] = useState<RentableItem | null>(null)
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [availability, setAvailability] = useState<RentalAvailability | null>(null)
  const [checking, setChecking] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const open = (item: RentableItem) => {
    setSelected(item)
    setStartDate(''); setEndDate(''); setAvailability(null); setError(null)
  }

  const days = daysBetween(startDate, endDate)
  const validPeriod = !!startDate && !!endDate && days > 0

  const check = async () => {
    if (!selected || !validPeriod) return
    setChecking(true); setError(null); setAvailability(null)
    try {
      setAvailability(await portalApi.rentalAvailability(slug, selected.id, startDate, endDate))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao verificar disponibilidade.')
    } finally {
      setChecking(false)
    }
  }

  const reserve = async () => {
    if (!selected || !validPeriod) return
    if (!customer || !accessToken) {
      setError('Você precisa entrar para reservar.')
      return
    }
    setSubmitting(true); setError(null)
    try {
      const booking = await portalApi.createRentalBooking(slug, accessToken, {
        items: [{ itemId: selected.id, quantity: 1 }],
        startDate, endDate,
      })
      router.push(`/${slug}/agendar/${booking.id}/status`)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao reservar.')
      setSubmitting(false)
    }
  }

  const rentalTotal = selected ? selected.dailyRate * days : 0

  return (
    <>
      {items.length === 0 && (
        <p className="text-slate-500">Nenhum item disponível para locação no momento.</p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {items.map(item => (
          <Card key={item.id}>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">{item.name}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-600">{brl(item.dailyRate)}/dia</p>
              {item.securityDeposit > 0 && (
                <p className="text-xs text-slate-400 mt-1">Caução {brl(item.securityDeposit)}</p>
              )}
              {item.description && <p className="text-xs text-slate-400 mt-1">{item.description}</p>}
              <Button className="mt-4 w-full" onClick={() => open(item)}>Alugar</Button>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={selected !== null} onOpenChange={o => !o && setSelected(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Alugar {selected?.name}</DialogTitle>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="startDate">Retirada</Label>
                <Input id="startDate" type="date" min={today()} value={startDate}
                  onChange={e => { setStartDate(e.target.value); setAvailability(null) }} />
              </div>
              <div>
                <Label htmlFor="endDate">Devolução</Label>
                <Input id="endDate" type="date" min={startDate || today()} value={endDate}
                  onChange={e => { setEndDate(e.target.value); setAvailability(null) }} />
              </div>
            </div>

            {validPeriod && selected && (
              <div className="rounded-md bg-slate-50 p-3 text-sm">
                <div className="flex justify-between">
                  <span className="text-slate-500">{days} diária(s) × {brl(selected.dailyRate)}</span>
                  <span className="font-medium">{brl(rentalTotal)}</span>
                </div>
                {selected.securityDeposit > 0 && (
                  <div className="flex justify-between text-slate-500">
                    <span>Caução (reembolsável)</span>
                    <span>{brl(selected.securityDeposit)}</span>
                  </div>
                )}
              </div>
            )}

            {availability && (
              <p className={availability.isAvailable ? 'text-sm text-green-600' : 'text-sm text-red-500'}>
                {availability.isAvailable
                  ? `${availability.availableUnits} de ${availability.totalQuantity} unidade(s) disponível(is)`
                  : 'Sem unidades disponíveis para o período.'}
              </p>
            )}

            {error && <p className="text-sm text-red-500">{error}</p>}

            <div className="flex gap-2 justify-end">
              <Button variant="outline" onClick={check} disabled={!validPeriod || checking}>
                {checking ? 'Verificando…' : 'Verificar disponibilidade'}
              </Button>
              <Button onClick={reserve} disabled={!validPeriod || submitting || availability?.isAvailable === false}>
                {submitting ? 'Reservando…' : 'Reservar'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </>
  )
}
