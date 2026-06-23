'use client'

import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { servicesApi } from '@/lib/api/services'
import { resourcesApi } from '@/lib/api/resources'
import { availabilityApi } from '@/lib/api/availability'
import { bookingsApi } from '@/lib/api/bookings'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type Step = 1 | 2 | 3

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated: () => void
}

export function AdminBookingModal({ open, onOpenChange, onCreated }: Props) {
  const [step, setStep] = useState<Step>(1)
  const [services, setServices] = useState<Service[]>([])
  const [resources, setResources] = useState<Resource[]>([])

  // Passo 1
  const [serviceId, setServiceId] = useState('')
  const [resourceId, setResourceId] = useState('')

  // Passo 2
  const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [slots, setSlots] = useState<string[]>([])
  const [selectedSlot, setSelectedSlot] = useState('')
  const [loadingSlots, setLoadingSlots] = useState(false)

  // Passo 3
  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!open) return
    Promise.all([servicesApi.list(), resourcesApi.list()]).then(([s, r]) => {
      setServices(s)
      setResources(r)
    })
  }, [open])

  useEffect(() => {
    if (!resourceId || !date) return
    let cancelled = false
    setLoadingSlots(true)
    setSlots([])
    setSelectedSlot('')
    availabilityApi
      .getSlots(resourceId, date, serviceId || undefined)
      .then(data => { if (!cancelled) setSlots(data) })
      .finally(() => { if (!cancelled) setLoadingSlots(false) })
    return () => { cancelled = true }
  }, [resourceId, date, serviceId])

  const reset = () => {
    setStep(1)
    setServiceId('')
    setResourceId('')
    setDate(format(new Date(), 'yyyy-MM-dd'))
    setSlots([])
    setSelectedSlot('')
    setLoadingSlots(false)
    setCustomerName('')
    setCustomerEmail('')
    setCustomerPhone('')
    setNotes('')
    setSaving(false)
  }

  const handleOpenChange = (open: boolean) => {
    if (!open) reset()
    onOpenChange(open)
  }

  const handleConfirm = async () => {
    if (!selectedSlot || !customerName.trim()) return
    setSaving(true)
    try {
      await bookingsApi.adminCreate({
        serviceIds: [serviceId],
        resourceId,
        scheduledAt: selectedSlot,
        customerName: customerName.trim(),
        customerEmail: customerEmail || undefined,
        customerPhone: customerPhone || undefined,
        notes: notes || undefined,
      })
      reset()
      onOpenChange(false)
      onCreated()
    } finally {
      setSaving(false)
    }
  }

  const filteredResources = serviceId
    ? resources.filter(r => r.serviceIds.includes(serviceId))
    : resources

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Nova Reserva — Passo {step} de 3</DialogTitle>
        </DialogHeader>

        {step === 1 && (
          <div className="space-y-4">
            <div>
              <Label>Serviço</Label>
              <Select
                value={serviceId}
                onValueChange={v => {
                  setServiceId(v ?? '')
                  setResourceId('')
                }}
              >
                <SelectTrigger className="mt-1">
                  {/* base-ui exibe o valor cru (id) por padrão; mapeamos para o nome do serviço. */}
                  <SelectValue placeholder="Selecione um serviço...">
                    {(value) => services.find(s => s.id === value)?.name ?? 'Selecione um serviço...'}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {services
                    .filter(s => s.isActive)
                    .map(s => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label>Recurso</Label>
              <Select
                value={resourceId}
                onValueChange={v => setResourceId(v ?? '')}
              >
                <SelectTrigger className="mt-1">
                  {/* base-ui exibe o valor cru (id) por padrão; mapeamos para o nome do recurso. */}
                  <SelectValue placeholder="Selecione um recurso...">
                    {(value) => filteredResources.find(r => r.id === value)?.name ?? 'Selecione um recurso...'}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {filteredResources
                    .filter(r => r.isActive)
                    .map(r => (
                      <SelectItem key={r.id} value={r.id}>
                        {r.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex justify-end">
              <Button
                onClick={() => setStep(2)}
                disabled={!serviceId || !resourceId}
              >
                Próximo
              </Button>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-4">
            <div>
              <Label>Data</Label>
              <Input
                type="date"
                value={date}
                min={format(new Date(), 'yyyy-MM-dd')}
                onChange={e => setDate(e.target.value)}
                className="mt-1"
              />
            </div>

            {loadingSlots && (
              <p className="text-sm text-slate-500">Carregando horários...</p>
            )}

            {!loadingSlots && date && slots.length === 0 && (
              <p className="text-sm text-slate-400">
                Nenhum horário disponível nesta data.
              </p>
            )}

            {slots.length > 0 && (
              <div>
                <Label>Horário disponível</Label>
                <div className="grid grid-cols-4 gap-2 mt-2">
                  {slots.map(slot => (
                    <button
                      key={slot}
                      type="button"
                      onClick={() => setSelectedSlot(slot)}
                      className={`rounded border px-2 py-1.5 text-sm transition-colors ${
                        selectedSlot === slot
                          ? 'bg-slate-900 text-white border-slate-900'
                          : 'border-slate-200 hover:border-slate-400'
                      }`}
                    >
                      {format(new Date(slot), 'HH:mm')}
                    </button>
                  ))}
                </div>
              </div>
            )}

            <div className="flex justify-between">
              <Button variant="outline" onClick={() => setStep(1)}>
                Voltar
              </Button>
              <Button onClick={() => setStep(3)} disabled={!selectedSlot}>
                Próximo
              </Button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-4">
            <div>
              <Label>Nome do cliente *</Label>
              <Input
                value={customerName}
                onChange={e => setCustomerName(e.target.value)}
                placeholder="Nome completo"
                className="mt-1"
              />
            </div>
            <div>
              <Label>E-mail</Label>
              <Input
                type="email"
                value={customerEmail}
                onChange={e => setCustomerEmail(e.target.value)}
                placeholder="email@exemplo.com"
                className="mt-1"
              />
            </div>
            <div>
              <Label>Telefone</Label>
              <Input
                value={customerPhone}
                onChange={e => setCustomerPhone(e.target.value)}
                placeholder="(11) 99999-9999"
                className="mt-1"
              />
            </div>
            <div>
              <Label>Observações</Label>
              <Input
                value={notes}
                onChange={e => setNotes(e.target.value)}
                placeholder="Opcional"
                className="mt-1"
              />
            </div>
            <div className="flex justify-between">
              <Button variant="outline" onClick={() => setStep(2)}>
                Voltar
              </Button>
              <Button
                onClick={handleConfirm}
                disabled={saving || !customerName.trim()}
              >
                {saving ? 'Criando...' : 'Confirmar Reserva'}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
