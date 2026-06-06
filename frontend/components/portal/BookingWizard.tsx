'use client'

import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { format } from 'date-fns'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { WizardStepService } from './WizardStepService'
import { WizardStepResource } from './WizardStepResource'
import { WizardStepSlot } from './WizardStepSlot'
import { WizardStepConfirm } from './WizardStepConfirm'
import { portalApi } from '@/lib/api/portal'
import { usePortalAuthStore } from '@/store/portal-auth'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const STEPS = ['Serviço', 'Recurso', 'Data/Hora', 'Confirmar']

interface Props {
  slug: string
  services: Service[]
  resources: Resource[]
  initialServiceId?: string
}

export function BookingWizard({ slug, services, resources, initialServiceId }: Props) {
  const router = useRouter()
  const { accessToken, customer } = usePortalAuthStore()

  const [step, setStep] = useState(initialServiceId ? 1 : 0)
  const [serviceId, setServiceId] = useState<string | null>(initialServiceId ?? null)
  const [resourceId, setResourceId] = useState<string | null>(null)
  const [selectedDate, setSelectedDate] = useState(new Date())
  const [selectedSlot, setSelectedSlot] = useState<string | null>(null)
  const [slots, setSlots] = useState<string[]>([])
  const [loadingSlots, setLoadingSlots] = useState(false)
  const [notes, setNotes] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const activeServices = services.filter(s => s.isActive)
  const capableResources = resources.filter(
    r => r.isActive && (serviceId ? r.serviceIds.includes(serviceId) : true)
  )

  useEffect(() => {
    if (step === 2 && resourceId) {
      setLoadingSlots(true)
      setSelectedSlot(null)
      const dateStr = format(selectedDate, 'yyyy-MM-dd')
      portalApi.slots(slug, resourceId, dateStr, serviceId ?? undefined)
        .then(setSlots)
        .catch(() => setSlots([]))
        .finally(() => setLoadingSlots(false))
    }
  }, [step, resourceId, selectedDate, slug, serviceId])

  const canNext = [
    !!serviceId,
    !!resourceId,
    !!selectedSlot,
    true,
  ][step]

  const handleNext = () => {
    if (step < STEPS.length - 1) setStep(s => s + 1)
  }

  const handleConfirm = async () => {
    if (!serviceId || !resourceId || !selectedSlot) return
    if (!customer || !accessToken) {
      alert('Você precisa entrar com Google para agendar.')
      return
    }
    setSubmitting(true)
    try {
      const result = await portalApi.createBooking(slug, accessToken, {
        serviceId,
        resourceId,
        scheduledAt: selectedSlot,
        notes: notes || undefined,
      })
      router.push(`/${slug}/agendar/${result.id}/status`)
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Erro ao criar agendamento.')
    } finally {
      setSubmitting(false)
    }
  }

  const selectedService = activeServices.find(s => s.id === serviceId)
  const selectedResource = capableResources.find(r => r.id === resourceId)

  return (
    <div className="max-w-2xl mx-auto">
      {/* Step indicator */}
      <div className="flex items-center gap-2 mb-8">
        {STEPS.map((label, i) => (
          <div key={label} className="flex items-center gap-2">
            <div className={cn(
              'h-8 w-8 rounded-full flex items-center justify-center text-sm font-bold',
              i < step ? 'bg-indigo-600 text-white' :
              i === step ? 'bg-indigo-100 text-indigo-700 border-2 border-indigo-600' :
              'bg-slate-100 text-slate-400'
            )}>
              {i + 1}
            </div>
            <span className={cn('text-xs hidden sm:block', i === step ? 'font-semibold' : 'text-slate-400')}>
              {label}
            </span>
            {i < STEPS.length - 1 && <div className="h-px w-6 bg-slate-200" />}
          </div>
        ))}
      </div>

      {/* Steps */}
      {step === 0 && (
        <WizardStepService
          services={activeServices}
          selectedId={serviceId}
          onSelect={id => { setServiceId(id); setResourceId(null) }}
        />
      )}
      {step === 1 && (
        <WizardStepResource resources={capableResources} selectedId={resourceId} onSelect={setResourceId} />
      )}
      {step === 2 && (
        <WizardStepSlot
          slots={slots}
          loadingSlots={loadingSlots}
          selectedDate={selectedDate}
          selectedSlot={selectedSlot}
          onDateChange={d => setSelectedDate(d)}
          onSlotSelect={setSelectedSlot}
        />
      )}
      {step === 3 && selectedService && selectedResource && selectedSlot && (
        <WizardStepConfirm
          service={selectedService}
          resource={selectedResource}
          slot={selectedSlot}
          notes={notes}
          onNotesChange={setNotes}
          onConfirm={handleConfirm}
          loading={submitting}
        />
      )}

      {/* Navigation */}
      {step < 3 && (
        <div className="flex justify-between mt-8">
          <Button variant="outline" onClick={() => setStep(s => s - 1)} disabled={step === 0}>
            Voltar
          </Button>
          <Button onClick={handleNext} disabled={!canNext}>
            Próximo
          </Button>
        </div>
      )}
      {step === 3 && (
        <div className="mt-4">
          <Button variant="ghost" onClick={() => setStep(s => s - 1)}>← Voltar</Button>
        </div>
      )}
    </div>
  )
}
