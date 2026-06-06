'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { Service, UpsertServiceRequest } from '@/lib/types/service'
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
  initial?: Service
  onSubmit: (data: UpsertServiceRequest) => void
  onCancel: () => void
}

export function ServiceForm({ initial, onSubmit, onCancel }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      description: initial?.description ?? '',
      durationMinutes: initial?.durationMinutes ?? 60,
      price: initial?.price ?? 0,
    },
  })

  return (
    <form onSubmit={handleSubmit((data) => onSubmit(data))} className="space-y-4">
      <div>
        <Label htmlFor="name">Nome</Label>
        <Input id="name" {...register('name')} />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="description">Descrição</Label>
        <Input id="description" {...register('description')} />
      </div>
      <div>
        <Label htmlFor="durationMinutes">Duração (minutos)</Label>
        <Input id="durationMinutes" type="number" {...register('durationMinutes')} />
        {errors.durationMinutes && <p className="text-sm text-red-500 mt-1">{errors.durationMinutes.message}</p>}
      </div>
      <div>
        <Label htmlFor="price">Preço (R$)</Label>
        <Input id="price" type="number" step="0.01" {...register('price')} />
        {errors.price && <p className="text-sm text-red-500 mt-1">{errors.price.message}</p>}
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit">Salvar</Button>
      </div>
    </form>
  )
}
