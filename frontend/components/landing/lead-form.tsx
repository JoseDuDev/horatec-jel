'use client'

import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { apiFetch } from '@/lib/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

const schema = z.object({
  name: z.string().min(1, 'Conta pra gente seu nome').max(200),
  phone: z
    .string()
    .min(1, 'Precisamos do seu WhatsApp pra te chamar')
    .max(30)
    .refine(v => (v.match(/\d/g) ?? []).length >= 10, 'Coloque o DDD junto, ex.: (11) 98888-7777')
    .refine(v => /^[\d\s()+.-]+$/.test(v), 'Só números e separadores, por favor'),
  businessType: z.string().min(1, 'Escolha o que mais parece com seu negócio'),
  message: z.string().max(1000).optional(),
  // Honeypot: humanos não veem este campo; bots preenchem e o envio é ignorado.
  website: z.string().max(0).optional().or(z.literal('')),
})

type FormData = z.infer<typeof schema>

interface LeadFormProps {
  brandId: string
  businessTypes: string[]
}

export function LeadForm({ brandId, businessTypes }: LeadFormProps) {
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { businessType: '' },
  })

  const onSubmit = async (data: FormData) => {
    if (data.website) return // honeypot preenchido: finge sucesso, não envia
    setError(null)
    setLoading(true)
    try {
      await apiFetch('/api/v1/platform/leads', {
        method: 'POST',
        body: JSON.stringify({
          name: data.name,
          phone: data.phone,
          email: null,
          businessType: data.businessType,
          message: data.message || null,
          brand: brandId,
        }),
      })
      setSent(true)
    } catch {
      setError('Não conseguimos enviar agora. Tenta de novo em instantes, ou chama a gente direto no WhatsApp.')
    } finally {
      setLoading(false)
    }
  }

  if (sent) {
    return (
      <div
        role="status"
        className="rounded-2xl bg-background px-6 py-10 text-center shadow-sm"
      >
        <p className="text-2xl font-semibold text-foreground">Recebemos! 🎉</p>
        <p className="mx-auto mt-3 max-w-md text-base leading-7 text-foreground/70">
          Em breve a gente te chama no WhatsApp pra conhecer seu negócio e montar
          seu portal com você. Fica de olho no celular!
        </p>
      </div>
    )
  }

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="space-y-5 rounded-2xl bg-background p-6 shadow-sm sm:p-8"
    >
      <div className="space-y-2">
        <Label htmlFor="lead-name" className="text-base">Seu nome</Label>
        <Input
          id="lead-name"
          autoComplete="name"
          placeholder="Como podemos te chamar?"
          className="h-11 text-base"
          aria-invalid={!!errors.name}
          {...register('name')}
        />
        {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="lead-phone" className="text-base">Seu WhatsApp</Label>
        <Input
          id="lead-phone"
          type="tel"
          inputMode="tel"
          autoComplete="tel-national"
          placeholder="(11) 98888-7777"
          className="h-11 text-base"
          aria-invalid={!!errors.phone}
          {...register('phone')}
        />
        {errors.phone && <p className="text-sm text-destructive">{errors.phone.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="lead-business" className="text-base">Seu negócio</Label>
        <select
          id="lead-business"
          aria-invalid={!!errors.businessType}
          className="border-input h-11 w-full appearance-none rounded-lg border bg-background bg-[url('data:image/svg+xml;charset=utf-8,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20width%3D%2216%22%20height%3D%2216%22%20fill%3D%22none%22%20stroke%3D%22%23737373%22%20stroke-width%3D%222%22%20stroke-linecap%3D%22round%22%20stroke-linejoin%3D%22round%22%3E%3Cpath%20d%3D%22m4%206%204%204%204-4%22%2F%3E%3C%2Fsvg%3E')] bg-[position:right_0.75rem_center] bg-no-repeat px-3 pr-10 text-base outline-none transition-colors focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive"
          defaultValue=""
          {...register('businessType')}
        >
          <option value="" disabled>O que mais parece com o seu?</option>
          {businessTypes.map(t => (
            <option key={t} value={t}>{t}</option>
          ))}
          <option value="Outro">Outro</option>
        </select>
        {errors.businessType && (
          <p className="text-sm text-destructive">{errors.businessType.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="lead-message" className="text-base">
          Quer contar mais? <span className="font-normal text-muted-foreground">(opcional)</span>
        </Label>
        <textarea
          id="lead-message"
          rows={3}
          placeholder="Ex.: hoje marco tudo pelo caderno e vivo perdendo horário…"
          className="border-input w-full resize-y rounded-lg border bg-background px-3 py-2.5 text-base outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
          {...register('message')}
        />
      </div>

      <div aria-hidden="true" className="absolute -left-[9999px] top-auto h-px w-px overflow-hidden">
        <label htmlFor="lead-website">Não preencha este campo</label>
        <input id="lead-website" type="text" tabIndex={-1} autoComplete="off" {...register('website')} />
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <Button
        type="submit"
        disabled={loading}
        className="h-12 w-full bg-(--brand-ink) text-base font-semibold text-white hover:bg-(--brand-ink) hover:opacity-90"
      >
        {loading ? 'Enviando…' : 'Quero meu portal'}
      </Button>
      <p className="text-center text-sm leading-6 text-muted-foreground">
        Sem compromisso e sem cartão. A gente só te chama pra conversar.
      </p>
    </form>
  )
}
