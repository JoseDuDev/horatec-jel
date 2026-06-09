'use client'

import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { Resource, UpsertResourceRequest } from '@/lib/types/resource'
import type { Service } from '@/lib/types/service'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const RESOURCE_TYPES = [
  { value: 'Professional', label: 'Profissional' },
  { value: 'PhysicalSpace', label: 'Espaço Físico' },
  { value: 'Equipment', label: 'Equipamento' },
  { value: 'Court', label: 'Quadra' },
] as const

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  type: z.enum(['Professional', 'PhysicalSpace', 'Equipment', 'Court']),
  serviceIds: z.array(z.string()),
})

type FormData = z.infer<typeof schema>

interface Props {
  initial?: Resource
  services: Service[]
  onSubmit: (data: UpsertResourceRequest) => void
  onCancel: () => void
}

export function ResourceForm({ initial, services, onSubmit, onCancel }: Props) {
  const { register, handleSubmit, control, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      type: initial?.type ?? 'Professional',
      serviceIds: initial?.serviceIds ?? [],
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
        <Label>Tipo</Label>
        <Controller
          name="type"
          control={control}
          render={({ field }) => (
            <Select value={field.value} onValueChange={field.onChange}>
              <SelectTrigger className="mt-1">
                <SelectValue placeholder="Selecione o tipo" />
              </SelectTrigger>
              <SelectContent>
                {RESOURCE_TYPES.map(t => (
                  <SelectItem key={t.value} value={t.value}>{t.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
        {errors.type && <p className="text-sm text-red-500 mt-1">{errors.type.message}</p>}
      </div>
      <div>
        <Label>Serviços Vinculados</Label>
        <Controller
          name="serviceIds"
          control={control}
          render={({ field }) => (
            <div className="space-y-2 mt-1">
              {services.length === 0 && (
                <p className="text-sm text-slate-400">Nenhum serviço cadastrado.</p>
              )}
              {services.map(s => (
                <label key={s.id} className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="checkbox"
                    checked={field.value.includes(s.id)}
                    onChange={e => {
                      if (e.target.checked) field.onChange([...field.value, s.id])
                      else field.onChange(field.value.filter((id: string) => id !== s.id))
                    }}
                  />
                  {s.name}
                </label>
              ))}
            </div>
          )}
        />
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit">Salvar</Button>
      </div>
    </form>
  )
}
