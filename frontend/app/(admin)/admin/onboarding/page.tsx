'use client'

import { useEffect, useMemo, useState } from 'react'
import { useRouter } from 'next/navigation'
import { onboardingApi } from '@/lib/api/onboarding'
import { tenantsApi } from '@/lib/api/tenants'
import type {
  OnboardingTenantData,
  OnboardingThemeData,
  OnboardingServiceData,
  OnboardingResourceData,
  OnboardingHoursData,
} from '@/lib/api/onboarding'
import { OnboardingStepTenant } from '@/components/onboarding/OnboardingStepTenant'
import { OnboardingStepTheme } from '@/components/onboarding/OnboardingStepTheme'
import { OnboardingStepService } from '@/components/onboarding/OnboardingStepService'
import { OnboardingStepResource } from '@/components/onboarding/OnboardingStepResource'
import { OnboardingStepHours } from '@/components/onboarding/OnboardingStepHours'
import { cn } from '@/lib/utils'

type StepKey = 'tenant' | 'theme' | 'service' | 'resource' | 'hours'

const STEP_LABEL: Record<StepKey, string> = {
  tenant:   'Negócio',
  theme:    'Visual',
  service:  'Serviço',
  resource: 'Recurso',
  hours:    'Horários',
}

export default function OnboardingPage() {
  const router = useRouter()
  const [stepIndex, setStepIndex] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tenantData, setTenantData] = useState<OnboardingTenantData | null>(null)

  // null = ainda carregando as capacidades do tenant.
  const [capabilities, setCapabilities] = useState<string | null>(null)

  useEffect(() => {
    tenantsApi.me()
      .then(t => setCapabilities(t.capabilities ?? 'Appointments, Rentals'))
      // Fallback permissivo: se não der p/ resolver, mantém o fluxo completo.
      .catch(() => setCapabilities('Appointments, Rentals'))
  }, [])

  // Passos de agendamento só entram se o tenant tiver a capacidade Appointments.
  const steps = useMemo<StepKey[]>(() => {
    const hasAppointments = (capabilities ?? '').includes('Appointments')
    const s: StepKey[] = ['tenant', 'theme']
    if (hasAppointments) s.push('service', 'resource', 'hours')
    return s
  }, [capabilities])

  const finish = async () => {
    await tenantsApi.completeOnboarding()
    router.push('/admin/dashboard')
  }

  const advance = async () => {
    if (stepIndex < steps.length - 1) setStepIndex(stepIndex + 1)
    else await finish()
  }

  const back = () => setStepIndex(i => Math.max(0, i - 1))

  // Wrapper que executa a ação do passo e avança (ou finaliza).
  const run = async (action: () => Promise<unknown>) => {
    setLoading(true)
    setError(null)
    try {
      await action()
      await advance()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleTenantNext   = (data: OnboardingTenantData) =>
    run(async () => { await onboardingApi.updateTenant(data); setTenantData(data) })
  const handleThemeNext    = (data: OnboardingThemeData)    => run(() => onboardingApi.updateTheme(data))
  const handleServiceNext  = (data: OnboardingServiceData)  => run(() => onboardingApi.createService(data))
  const handleResourceNext = (data: OnboardingResourceData) => run(() => onboardingApi.createResource(data))

  const handleHoursFinish = async (data: OnboardingHoursData) => {
    setLoading(true)
    setError(null)
    try {
      await Promise.all(
        data.schedule.map(d =>
          onboardingApi.setBusinessHours(d.dayOfWeek, d.isOpen, d.openTime, d.closeTime)
        )
      )
      await finish()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
      setLoading(false)
    }
  }

  if (capabilities === null) {
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center">
        <p className="text-slate-400">Carregando...</p>
      </div>
    )
  }

  const current = steps[stepIndex]

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-sm border w-full max-w-lg p-8">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-slate-900">Configurar seu negócio</h1>
          <p className="text-sm text-slate-500 mt-1">Passo {stepIndex + 1} de {steps.length}</p>
        </div>

        <div className="flex justify-center gap-2 mb-8">
          {steps.map((key, i) => (
            <div key={key} className="flex flex-col items-center gap-1">
              <div className={cn(
                'h-8 w-8 rounded-full flex items-center justify-center text-xs font-bold',
                i < stepIndex ? 'bg-indigo-600 text-white' :
                i === stepIndex ? 'bg-indigo-100 text-indigo-700 border-2 border-indigo-600' :
                'bg-slate-100 text-slate-400'
              )}>
                {i < stepIndex ? '✓' : i + 1}
              </div>
              <span className="text-[10px] text-slate-400 hidden sm:block">{STEP_LABEL[key]}</span>
            </div>
          ))}
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
            {error}
          </div>
        )}

        {current === 'tenant' && (
          <OnboardingStepTenant initial={tenantData ?? undefined} onNext={handleTenantNext} />
        )}
        {current === 'theme' && (
          <OnboardingStepTheme onNext={handleThemeNext} onBack={back} />
        )}
        {current === 'service' && (
          <OnboardingStepService onNext={handleServiceNext} onBack={back} />
        )}
        {current === 'resource' && (
          <OnboardingStepResource onNext={handleResourceNext} onBack={back} />
        )}
        {current === 'hours' && (
          <OnboardingStepHours onFinish={handleHoursFinish} onBack={back} loading={loading} />
        )}
      </div>
    </div>
  )
}
