'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { onboardingApi } from '@/lib/api/onboarding'
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

const STEP_LABELS = ['Negócio', 'Visual', 'Serviço', 'Recurso', 'Horários']

export default function OnboardingPage() {
  const router = useRouter()
  const [step, setStep] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [tenantData, setTenantData] = useState<OnboardingTenantData | null>(null)

  const handleTenantNext = async (data: OnboardingTenantData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.updateTenant(data)
      setTenantData(data)
      setStep(1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleThemeNext = async (data: OnboardingThemeData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.updateTheme(data)
      setStep(2)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleServiceNext = async (data: OnboardingServiceData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.createService(data)
      setStep(3)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleResourceNext = async (data: OnboardingResourceData) => {
    setLoading(true)
    setError(null)
    try {
      await onboardingApi.createResource(data)
      setStep(4)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
    } finally {
      setLoading(false)
    }
  }

  const handleHoursFinish = async (data: OnboardingHoursData) => {
    setLoading(true)
    setError(null)
    try {
      await Promise.all(
        data.schedule.map(d =>
          onboardingApi.setBusinessHours(d.dayOfWeek, d.isOpen, d.openTime, d.closeTime)
        )
      )
      router.push('/admin/dashboard')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Erro ao salvar')
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-sm border w-full max-w-lg p-8">
        <div className="text-center mb-8">
          <h1 className="text-2xl font-bold text-slate-900">Configurar seu negócio</h1>
          <p className="text-sm text-slate-500 mt-1">Passo {step + 1} de {STEP_LABELS.length}</p>
        </div>

        <div className="flex justify-center gap-2 mb-8">
          {STEP_LABELS.map((label, i) => (
            <div key={label} className="flex flex-col items-center gap-1">
              <div className={cn(
                'h-8 w-8 rounded-full flex items-center justify-center text-xs font-bold',
                i < step ? 'bg-indigo-600 text-white' :
                i === step ? 'bg-indigo-100 text-indigo-700 border-2 border-indigo-600' :
                'bg-slate-100 text-slate-400'
              )}>
                {i < step ? '✓' : i + 1}
              </div>
              <span className="text-[10px] text-slate-400 hidden sm:block">{label}</span>
            </div>
          ))}
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
            {error}
          </div>
        )}

        {step === 0 && (
          <OnboardingStepTenant initial={tenantData ?? undefined} onNext={handleTenantNext} />
        )}
        {step === 1 && (
          <OnboardingStepTheme onNext={handleThemeNext} onBack={() => setStep(0)} />
        )}
        {step === 2 && (
          <OnboardingStepService onNext={handleServiceNext} onBack={() => setStep(1)} />
        )}
        {step === 3 && (
          <OnboardingStepResource onNext={handleResourceNext} onBack={() => setStep(2)} />
        )}
        {step === 4 && (
          <OnboardingStepHours onFinish={handleHoursFinish} onBack={() => setStep(3)} loading={loading} />
        )}
      </div>
    </div>
  )
}
