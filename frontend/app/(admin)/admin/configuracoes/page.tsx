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

const identitySchema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  logoUrl: z.string().url('URL inválida').optional().or(z.literal('')),
  primaryColor: z.string().regex(/^#[0-9a-fA-F]{6}$/, 'Cor hex inválida').optional().or(z.literal('')),
  timezone: z.string().min(1),
})

type IdentityForm = z.infer<typeof identitySchema>

export default function ConfiguracoesPage() {
  const [tenant, setTenant] = useState<Tenant | null>(null)
  const [saved, setSaved] = useState(false)

  const { register, handleSubmit, reset, formState: { errors } } = useForm<IdentityForm>({
    resolver: zodResolver(identitySchema),
  })

  useEffect(() => {
    tenantsApi.me().then(t => {
      setTenant(t)
      reset({
        name: t.name,
        logoUrl: t.logoUrl ?? '',
        primaryColor: t.primaryColor ?? '',
        timezone: t.timezone,
      })
    })
  }, [reset])

  const onSubmit = async (data: IdentityForm) => {
    await tenantsApi.update(data)
    if (data.primaryColor) {
      await tenantsApi.updateTheme(data.primaryColor, data.logoUrl || undefined)
    }
    setSaved(true)
    setTimeout(() => setSaved(false), 3000)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Configurações</h1>

      <Tabs defaultValue="identidade">
        <TabsList>
          <TabsTrigger value="identidade">Identidade Visual</TabsTrigger>
          <TabsTrigger value="plano">Plano</TabsTrigger>
        </TabsList>

        <TabsContent value="identidade">
          <Card>
            <CardHeader><CardTitle>Identidade Visual</CardTitle></CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 max-w-md">
                <div>
                  <Label htmlFor="name">Nome do Negócio</Label>
                  <Input id="name" {...register('name')} />
                  {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
                </div>
                <div>
                  <Label htmlFor="logoUrl">URL do Logo</Label>
                  <Input id="logoUrl" {...register('logoUrl')} placeholder="https://..." />
                  {errors.logoUrl && <p className="text-sm text-red-500 mt-1">{errors.logoUrl.message}</p>}
                </div>
                <div>
                  <Label htmlFor="primaryColor">Cor Principal</Label>
                  <div className="flex gap-2 items-center">
                    <Input id="primaryColor" {...register('primaryColor')} placeholder="#6366f1" className="font-mono" />
                    <input type="color" {...register('primaryColor')} className="h-10 w-10 rounded border cursor-pointer" />
                  </div>
                  {errors.primaryColor && <p className="text-sm text-red-500 mt-1">{errors.primaryColor.message}</p>}
                </div>
                <div>
                  <Label htmlFor="timezone">Fuso Horário</Label>
                  <Input id="timezone" {...register('timezone')} placeholder="America/Sao_Paulo" />
                </div>
                <div className="flex items-center gap-4">
                  <Button type="submit">Salvar</Button>
                  {saved && <span className="text-sm text-green-600">Salvo com sucesso!</span>}
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="plano">
          <Card>
            <CardHeader><CardTitle>Plano Atual</CardTitle></CardHeader>
            <CardContent>
              <p className="text-slate-600">Plano: <span className="font-semibold">{tenant?.plan ?? '...'}</span></p>
              <p className="text-sm text-slate-400 mt-2">Gerenciamento de plano será disponibilizado em breve.</p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}
