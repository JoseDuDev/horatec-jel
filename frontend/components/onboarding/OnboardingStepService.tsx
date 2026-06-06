'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingServiceData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  description: z.string().optional(),
  durationMinutes: z.coerce.number().min(1, 'Duração mínima 1 minuto'),
  price: z.coerce.number().min(0, 'Preço inválido'),
})

type FormData = z.infer<typeof schema>

interface Props {
  onNext: (data: OnboardingServiceData) => void
  onBack: () => void
}

export function OnboardingStepService({ onNext, onBack }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema) as never,
    defaultValues: { durationMinutes: 60, price: 0 },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Primeiro serviço</h2>
        <p className="text-sm text-slate-500 mb-6">Cadastre o principal serviço que você oferece.</p>
      </div>
      <div>
        <Label htmlFor="svc-name">Nome do Serviço</Label>
        <Input id="svc-name" {...register('name')} placeholder="Ex: Corte de Cabelo" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="svc-desc">Descrição (opcional)</Label>
        <Input id="svc-desc" {...register('description')} placeholder="Breve descrição..." />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <Label htmlFor="svc-duration">Duração (min)</Label>
          <Input id="svc-duration" type="number" {...register('durationMinutes')} />
          {errors.durationMinutes && <p className="text-sm text-red-500 mt-1">{errors.durationMinutes.message}</p>}
        </div>
        <div>
          <Label htmlFor="svc-price">Preço (R$)</Label>
          <Input id="svc-price" type="number" step="0.01" {...register('price')} />
          {errors.price && <p className="text-sm text-red-500 mt-1">{errors.price.message}</p>}
        </div>
      </div>
      <div className="flex gap-3">
        <Button type="button" variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button type="submit" className="flex-1">Próximo →</Button>
      </div>
    </form>
  )
}
