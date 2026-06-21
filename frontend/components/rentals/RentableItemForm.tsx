'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { CreateRentableItemRequest } from '@/lib/types/rental'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  quantity: z.coerce.number().min(1, 'Estoque mínimo 1'),
  dailyRate: z.coerce.number().min(0, 'Diária inválida'),
  securityDeposit: z.coerce.number().min(0, 'Caução inválida'),
  bufferDays: z.coerce.number().min(0, 'Buffer inválido'),
  description: z.string().optional(),
  category: z.string().optional(),
})

type FormData = z.infer<typeof schema>

interface Props {
  onSubmit: (data: CreateRentableItemRequest) => void
  onCancel: () => void
}

export function RentableItemForm({ onSubmit, onCancel }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    defaultValues: {
      name: '', quantity: 1, dailyRate: 0, securityDeposit: 0, bufferDays: 0,
      description: '', category: '',
    },
  })

  return (
    <form onSubmit={handleSubmit((data) => onSubmit(data))} className="space-y-4">
      <div>
        <Label htmlFor="name">Nome</Label>
        <Input id="name" {...register('name')} />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div>
          <Label htmlFor="quantity">Estoque (unidades)</Label>
          <Input id="quantity" type="number" {...register('quantity')} />
          {errors.quantity && <p className="text-sm text-red-500 mt-1">{errors.quantity.message}</p>}
        </div>
        <div>
          <Label htmlFor="bufferDays">Buffer (dias)</Label>
          <Input id="bufferDays" type="number" {...register('bufferDays')} />
          {errors.bufferDays && <p className="text-sm text-red-500 mt-1">{errors.bufferDays.message}</p>}
        </div>
        <div>
          <Label htmlFor="dailyRate">Diária (R$)</Label>
          <Input id="dailyRate" type="number" step="0.01" {...register('dailyRate')} />
          {errors.dailyRate && <p className="text-sm text-red-500 mt-1">{errors.dailyRate.message}</p>}
        </div>
        <div>
          <Label htmlFor="securityDeposit">Caução (R$)</Label>
          <Input id="securityDeposit" type="number" step="0.01" {...register('securityDeposit')} />
          {errors.securityDeposit && <p className="text-sm text-red-500 mt-1">{errors.securityDeposit.message}</p>}
        </div>
      </div>
      <div>
        <Label htmlFor="category">Categoria</Label>
        <Input id="category" {...register('category')} />
      </div>
      <div>
        <Label htmlFor="description">Descrição</Label>
        <Input id="description" {...register('description')} />
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit">Salvar</Button>
      </div>
    </form>
  )
}
