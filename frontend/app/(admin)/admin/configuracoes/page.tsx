'use client'

import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { tenantsApi } from '@/lib/api/tenants'
import type { Tenant } from '@/lib/types/tenant'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { PlanUsageCard } from '@/components/admin/PlanUsageCard'

const identitySchema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  logoUrl: z.string().url('URL inválida').optional().or(z.literal('')),
  primaryColor: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Cor hex inválida').optional().or(z.literal('')),
  timezone: z.string().min(1),
})
type IdentityForm = z.infer<typeof identitySchema>

const loyaltySchema = z.object({
  isEnabled: z.boolean(),
  creditRatePercent: z.number().min(0).max(100),
  minBookingAmount: z.number().min(0),
})
type LoyaltyForm = z.infer<typeof loyaltySchema>

const cancellationSchema = z.object({
  allowCustomerCancellation: z.boolean(),
  minCancellationHours: z.number().int().min(0),
  cancellationFeePercent: z.number().min(0).max(100),
})
type CancellationForm = z.infer<typeof cancellationSchema>

export default function ConfiguracoesPage() {
  const [tenant, setTenant] = useState<Tenant | null>(null)
  const [savedIdentity, setSavedIdentity] = useState(false)
  const [savedLoyalty, setSavedLoyalty] = useState(false)
  const [savedCancel, setSavedCancel] = useState(false)

  const identityForm = useForm<IdentityForm>({ resolver: zodResolver(identitySchema) })
  const loyaltyForm  = useForm<LoyaltyForm>({ resolver: zodResolver(loyaltySchema) })
  const cancelForm   = useForm<CancellationForm>({ resolver: zodResolver(cancellationSchema) })

  useEffect(() => {
    tenantsApi.me().then(t => {
      setTenant(t)
      identityForm.reset({
        name: t.name,
        logoUrl: t.logoUrl ?? '',
        primaryColor: t.primaryColor ?? '',
        timezone: t.timezone,
      })
      loyaltyForm.reset({
        isEnabled: t.loyaltySettings.isEnabled,
        creditRatePercent: t.loyaltySettings.creditRatePercent,
        minBookingAmount: t.loyaltySettings.minBookingAmount,
      })
      cancelForm.reset({
        allowCustomerCancellation: t.cancellationPolicy.allowCustomerCancellation,
        minCancellationHours: t.cancellationPolicy.minCancellationHours,
        cancellationFeePercent: t.cancellationPolicy.cancellationFeePercent,
      })
    })
  }, [identityForm, loyaltyForm, cancelForm])

  const onIdentitySubmit = async (data: IdentityForm) => {
    await tenantsApi.update(data)
    if (data.primaryColor) await tenantsApi.updateTheme(data.primaryColor, data.logoUrl || undefined)
    setSavedIdentity(true)
    setTimeout(() => setSavedIdentity(false), 3000)
  }

  const onLoyaltySubmit = async (data: LoyaltyForm) => {
    await tenantsApi.updateLoyaltySettings(data)
    setSavedLoyalty(true)
    setTimeout(() => setSavedLoyalty(false), 3000)
  }

  const onCancelSubmit = async (data: CancellationForm) => {
    await tenantsApi.updateCancellationPolicy(data)
    setSavedCancel(true)
    setTimeout(() => setSavedCancel(false), 3000)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Configurações</h1>

      <Tabs defaultValue="identidade">
        <TabsList>
          <TabsTrigger value="identidade">Identidade Visual</TabsTrigger>
          <TabsTrigger value="cancelamentos">Cancelamentos</TabsTrigger>
          <TabsTrigger value="fidelidade">Fidelidade</TabsTrigger>
          <TabsTrigger value="plano">Plano</TabsTrigger>
        </TabsList>

        <TabsContent value="identidade">
          <Card>
            <CardHeader><CardTitle>Identidade Visual</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={identityForm.handleSubmit(onIdentitySubmit)} className="space-y-4 max-w-md">
                <div>
                  <Label htmlFor="name">Nome do Negócio</Label>
                  <Input id="name" {...identityForm.register('name')} />
                  {identityForm.formState.errors.name && (
                    <p className="text-sm text-red-500 mt-1">{identityForm.formState.errors.name.message}</p>
                  )}
                </div>
                <div>
                  <Label htmlFor="logoUrl">URL do Logo</Label>
                  <Input id="logoUrl" {...identityForm.register('logoUrl')} placeholder="https://..." />
                </div>
                <div>
                  <Label htmlFor="primaryColor">Cor Principal</Label>
                  <div className="flex gap-2 items-center">
                    <Input id="primaryColor" {...identityForm.register('primaryColor')} placeholder="#6366f1" className="font-mono" />
                    <input type="color" {...identityForm.register('primaryColor')} className="h-10 w-10 rounded border cursor-pointer" />
                  </div>
                </div>
                <div>
                  <Label htmlFor="timezone">Fuso Horário</Label>
                  <Input id="timezone" {...identityForm.register('timezone')} placeholder="America/Sao_Paulo" />
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {savedIdentity && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="cancelamentos">
          <Card>
            <CardHeader><CardTitle>Política de Cancelamento</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={cancelForm.handleSubmit(onCancelSubmit)} className="space-y-4 max-w-md">
                <div className="flex items-center gap-3">
                  <input
                    type="checkbox"
                    id="allowCustomerCancellation"
                    {...cancelForm.register('allowCustomerCancellation')}
                    className="h-4 w-4"
                  />
                  <Label htmlFor="allowCustomerCancellation">Permitir cancelamento pelo cliente</Label>
                </div>
                <div>
                  <Label htmlFor="minCancellationHours">Horas mínimas para cancelamento gratuito</Label>
                  <Input
                    id="minCancellationHours"
                    type="number"
                    min={0}
                    {...cancelForm.register('minCancellationHours', { valueAsNumber: true })}
                    placeholder="Ex: 24"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Cancelamentos com menos de X horas de antecedência estão sujeitos à taxa.
                  </p>
                </div>
                <div>
                  <Label htmlFor="cancellationFeePercent">Taxa de cancelamento fora do prazo (%)</Label>
                  <Input
                    id="cancellationFeePercent"
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    {...cancelForm.register('cancellationFeePercent', { valueAsNumber: true })}
                    placeholder="Ex: 20"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Percentual do valor pago retido como taxa. 0 = reembolso total.
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {savedCancel && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="fidelidade">
          <Card>
            <CardHeader><CardTitle>Programa de Fidelidade</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={loyaltyForm.handleSubmit(onLoyaltySubmit)} className="space-y-4 max-w-md">
                <div className="flex items-center gap-3">
                  <input
                    type="checkbox"
                    id="loyaltyIsEnabled"
                    {...loyaltyForm.register('isEnabled')}
                    className="h-4 w-4"
                  />
                  <Label htmlFor="loyaltyIsEnabled">Ativar programa de fidelidade</Label>
                </div>
                <div>
                  <Label htmlFor="creditRatePercent">Taxa de crédito (% do valor do serviço)</Label>
                  <Input
                    id="creditRatePercent"
                    type="number"
                    min={0}
                    max={100}
                    step="0.1"
                    {...loyaltyForm.register('creditRatePercent', { valueAsNumber: true })}
                    placeholder="Ex: 5"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    Ex: 5% → agendamento de R$ 100 gera R$ 5,00 em créditos.
                  </p>
                </div>
                <div>
                  <Label htmlFor="minBookingAmount">Valor mínimo para ganhar pontos (R$)</Label>
                  <Input
                    id="minBookingAmount"
                    type="number"
                    min={0}
                    step="0.01"
                    {...loyaltyForm.register('minBookingAmount', { valueAsNumber: true })}
                    placeholder="Ex: 0"
                  />
                  <p className="text-xs text-slate-500 mt-1">
                    0 = todos os agendamentos geram créditos.
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {savedLoyalty && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="plano">
          <PlanUsageCard />
        </TabsContent>
      </Tabs>
    </div>
  )
}
