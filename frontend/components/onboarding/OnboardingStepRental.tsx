'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingRentalData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  description: z.string().optional(),
  quantity: z.coerce.number().min(1, 'Quantidade mínima 1'),
  dailyRate: z.coerce.number().min(0, 'Valor inválido'),
  securityDeposit: z.coerce.number().min(0, 'Valor inválido'),
})

type FormData = z.infer<typeof schema>

interface Props {
  onNext: (data: OnboardingRentalData) => void
  onBack: () => void
  /** Quando é o último passo do fluxo, o botão finaliza o onboarding. */
  isLast?: boolean
}

export function OnboardingStepRental({ onNext, onBack, isLast }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema) as never,
    defaultValues: { quantity: 1, dailyRate: 0, securityDeposit: 0 },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Primeiro item de locação</h2>
        <p className="text-sm text-slate-500 mb-6">Cadastre um item que você disponibiliza para aluguel.</p>
      </div>
      <div>
        <Label htmlFor="rent-name">Nome do item</Label>
        <Input id="rent-name" {...register('name')} placeholder="Ex: Furadeira" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="rent-desc">Descrição (opcional)</Label>
        <Input id="rent-desc" {...register('description')} placeholder="Breve descrição..." />
      </div>
      <div className="grid grid-cols-3 gap-4">
        <div>
          <Label htmlFor="rent-qty">Unidades</Label>
          <Input id="rent-qty" type="number" {...register('quantity')} />
          {errors.quantity && <p className="text-sm text-red-500 mt-1">{errors.quantity.message}</p>}
        </div>
        <div>
          <Label htmlFor="rent-rate">Diária (R$)</Label>
          <Input id="rent-rate" type="number" step="0.01" {...register('dailyRate')} />
          {errors.dailyRate && <p className="text-sm text-red-500 mt-1">{errors.dailyRate.message}</p>}
        </div>
        <div>
          <Label htmlFor="rent-deposit">Caução (R$)</Label>
          <Input id="rent-deposit" type="number" step="0.01" {...register('securityDeposit')} />
          {errors.securityDeposit && <p className="text-sm text-red-500 mt-1">{errors.securityDeposit.message}</p>}
        </div>
      </div>
      <div className="flex gap-3">
        <Button type="button" variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button type="submit" className="flex-1">{isLast ? 'Concluir ✓' : 'Próximo →'}</Button>
      </div>
    </form>
  )
}
