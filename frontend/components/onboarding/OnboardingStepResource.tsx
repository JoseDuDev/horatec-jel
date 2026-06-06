'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingResourceData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  type: z.string().min(1, 'Tipo obrigatório'),
})

type FormData = z.infer<typeof schema>

const RESOURCE_TYPES = ['Professional', 'PhysicalSpace', 'Equipment', 'Court']
const RESOURCE_TYPE_LABELS: Record<string, string> = {
  Professional: 'Profissional',
  PhysicalSpace: 'Espaço Físico',
  Equipment: 'Equipamento',
  Court: 'Quadra',
}

interface Props {
  onNext: (data: OnboardingResourceData) => void
  onBack: () => void
}

export function OnboardingStepResource({ onNext, onBack }: Props) {
  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { type: 'Professional' },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Primeiro recurso</h2>
        <p className="text-sm text-slate-500 mb-6">
          Quem ou o que executa o serviço? (profissional, sala, equipamento...)
        </p>
      </div>
      <div>
        <Label htmlFor="res-type">Tipo de Recurso</Label>
        <Select defaultValue="Professional" onValueChange={v => setValue('type', v)}>
          <SelectTrigger id="res-type">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {RESOURCE_TYPES.map(t => (
              <SelectItem key={t} value={t}>{RESOURCE_TYPE_LABELS[t]}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {errors.type && <p className="text-sm text-red-500 mt-1">{errors.type.message}</p>}
      </div>
      <div>
        <Label htmlFor="res-name">Nome</Label>
        <Input id="res-name" {...register('name')} placeholder="Ex: João Silva" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div className="flex gap-3">
        <Button type="button" variant="outline" onClick={onBack} className="flex-1">← Voltar</Button>
        <Button type="submit" className="flex-1">Próximo →</Button>
      </div>
    </form>
  )
}
