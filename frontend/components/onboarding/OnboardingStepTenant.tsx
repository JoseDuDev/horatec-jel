'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { OnboardingTenantData } from '@/lib/api/onboarding'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  timezone: z.string().min(1, 'Fuso obrigatório'),
})

type FormData = z.infer<typeof schema>

const TIMEZONES = [
  'America/Sao_Paulo',
  'America/Manaus',
  'America/Belem',
  'America/Fortaleza',
  'America/Recife',
  'America/Bahia',
  'America/Cuiaba',
  'America/Porto_Velho',
  'America/Boa_Vista',
  'America/Rio_Branco',
  'America/Noronha',
]

interface Props {
  initial?: Partial<OnboardingTenantData>
  onNext: (data: OnboardingTenantData) => void
}

export function OnboardingStepTenant({ initial, onNext }: Props) {
  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial?.name ?? '',
      timezone: initial?.timezone ?? 'America/Sao_Paulo',
    },
  })

  return (
    <form onSubmit={handleSubmit((data) => onNext(data))} className="space-y-6">
      <div>
        <h2 className="text-xl font-bold mb-1">Informações do negócio</h2>
        <p className="text-sm text-slate-500 mb-6">Como seu negócio se chama e onde está localizado?</p>
      </div>
      <div>
        <Label htmlFor="name">Nome do Negócio</Label>
        <Input id="name" {...register('name')} placeholder="Ex: Barbearia do João" />
        {errors.name && <p className="text-sm text-red-500 mt-1">{errors.name.message}</p>}
      </div>
      <div>
        <Label htmlFor="timezone">Fuso Horário</Label>
        <Select
          defaultValue={initial?.timezone ?? 'America/Sao_Paulo'}
          onValueChange={v => setValue('timezone', v)}
        >
          <SelectTrigger id="timezone">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TIMEZONES.map(tz => (
              <SelectItem key={tz} value={tz}>{tz.replace('America/', '')}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        {errors.timezone && <p className="text-sm text-red-500 mt-1">{errors.timezone.message}</p>}
      </div>
      <Button type="submit" className="w-full">Próximo →</Button>
    </form>
  )
}
