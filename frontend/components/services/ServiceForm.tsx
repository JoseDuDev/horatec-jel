'use client'

import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Trash2 } from 'lucide-react'

import type { Service, UpsertServiceRequest } from '@/lib/types/service'
import { servicesApi } from '@/lib/api/services'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ImagePicker } from '@/components/ui/image-picker'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  description: z.string().optional(),
  durationMinutes: z.coerce.number().min(1, 'Duração mínima 1 minuto'),
  price: z.coerce.number().min(0, 'Preço inválido'),
})

type FormData = z.infer<typeof schema>

interface Props {
  initial?: Service
  /**
   * `photo` só vem preenchido no cadastro: o serviço ainda não tem id, então quem
   * cria é que sobe a foto depois. Na edição a troca já foi feita aqui mesmo.
   */
  onSubmit: (data: UpsertServiceRequest, photo: File | null) => void
  onCancel: () => void
}

export function ServiceForm({ initial, onSubmit, onCancel }: Props) {
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    defaultValues: {
      name: initial?.name ?? '',
      description: initial?.description ?? '',
      durationMinutes: initial?.durationMinutes ?? 60,
      price: initial?.price ?? 0,
    },
  })

  // A URL fica em estado próprio (e não só em `initial`) porque na edição a foto
  // é trocada antes do Salvar — e é este valor que volta no payload, para que
  // salvar os outros campos não apague a imagem recém-enviada.
  const [imageUrl, setImageUrl] = useState<string | undefined>(initial?.imageUrl)
  const [photo, setPhoto] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)

  const preview = useMemo(() => (photo ? URL.createObjectURL(photo) : null), [photo])

  // O object URL só é liberado quando a foto muda ou o formulário sai de cena.
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview) }, [preview])

  const select = async ([file]: File[]) => {
    setError(null)
    if (!initial) { setPhoto(file); return }

    try {
      const { url } = await servicesApi.setImage(initial.id, file)
      setImageUrl(url)
      setPhoto(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível enviar a foto.')
    }
  }

  const remove = async () => {
    setError(null)
    if (photo) { setPhoto(null); return }
    if (!initial || !imageUrl) return

    try {
      await servicesApi.removeImage(initial.id)
      setImageUrl(undefined)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível remover a foto.')
    }
  }

  const shown = preview ?? imageUrl

  return (
    <form
      onSubmit={handleSubmit(data => onSubmit({ ...data, imageUrl }, photo))}
      className="space-y-4"
    >
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

      <div>
        <Label>Foto</Label>
        {shown ? (
          <div className="relative mt-2 w-40 overflow-hidden rounded-md border">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={shown} alt="Foto do serviço" className="aspect-square w-full object-cover" />
            <Button
              type="button"
              size="icon-sm"
              variant="destructive"
              title="Remover foto"
              className="absolute bottom-1 right-1"
              onClick={remove}
            >
              <Trash2 className="h-3 w-3" />
            </Button>
          </div>
        ) : (
          <ImagePicker
            className="mt-2"
            onSelect={select}
            label="Adicionar foto"
            helpText="Aparece na vitrine e na escolha do serviço"
          />
        )}
        {error && <p className="mt-1 text-sm text-red-500">{error}</p>}
      </div>

      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Salvando...' : 'Salvar'}
        </Button>
      </div>
    </form>
  )
}
