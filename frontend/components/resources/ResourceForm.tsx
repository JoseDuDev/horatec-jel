'use client'

import { useEffect, useMemo, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Trash2 } from 'lucide-react'
import type { Resource, UpsertResourceRequest } from '@/lib/types/resource'
import type { Service } from '@/lib/types/service'
import { resourcesApi } from '@/lib/api/resources'
import { ImagePicker } from '@/components/ui/image-picker'
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
  /**
   * `photo` só vem preenchido no cadastro: o recurso ainda não tem id, então quem
   * cria é que sobe a foto depois. Na edição a troca já foi feita aqui mesmo.
   */
  onSubmit: (data: UpsertResourceRequest, photo: File | null) => void
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

  // A URL fica em estado próprio porque na edição a foto é trocada antes do Salvar —
  // e é este valor que volta no payload, para que salvar não apague a imagem nova.
  const [avatarUrl, setAvatarUrl] = useState<string | undefined>(initial?.avatarUrl)
  const [photo, setPhoto] = useState<File | null>(null)
  const [photoError, setPhotoError] = useState<string | null>(null)

  const preview = useMemo(() => (photo ? URL.createObjectURL(photo) : null), [photo])
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview) }, [preview])

  const select = async ([file]: File[]) => {
    setPhotoError(null)
    if (!initial) { setPhoto(file); return }

    try {
      const { url } = await resourcesApi.setImage(initial.id, file)
      setAvatarUrl(url)
      setPhoto(null)
    } catch (e) {
      setPhotoError(e instanceof Error ? e.message : 'Não foi possível enviar a foto.')
    }
  }

  const removePhoto = async () => {
    setPhotoError(null)
    if (photo) { setPhoto(null); return }
    if (!initial || !avatarUrl) return

    try {
      await resourcesApi.removeImage(initial.id)
      setAvatarUrl(undefined)
    } catch (e) {
      setPhotoError(e instanceof Error ? e.message : 'Não foi possível remover a foto.')
    }
  }

  const shown = preview ?? avatarUrl

  return (
    <form
      onSubmit={handleSubmit((data) => onSubmit({
        ...data,
        email: initial?.email,
        phone: initial?.phone,
        specialty: initial?.specialty,
        bio: initial?.bio,
        avatarUrl,
      }, photo))}
      className="space-y-4"
    >
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
                {/* base-ui exibe o valor cru por padrão; mapeamos para o rótulo PT-BR. */}
                <SelectValue placeholder="Selecione o tipo">
                  {(value) => RESOURCE_TYPES.find(t => t.value === value)?.label ?? 'Selecione o tipo'}
                </SelectValue>
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
      <div>
        <Label>Foto</Label>
        {shown ? (
          <div className="relative mt-2 w-40 overflow-hidden rounded-md border">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={shown} alt="Foto do recurso" className="aspect-square w-full object-cover" />
            <Button
              type="button"
              size="icon-sm"
              variant="destructive"
              title="Remover foto"
              className="absolute bottom-1 right-1"
              onClick={removePhoto}
            >
              <Trash2 className="h-3 w-3" />
            </Button>
          </div>
        ) : (
          <ImagePicker
            className="mt-2"
            onSelect={select}
            label="Adicionar foto"
            helpText="Aparece na escolha do profissional/recurso"
          />
        )}
        {photoError && <p className="mt-1 text-sm text-red-500">{photoError}</p>}
      </div>
      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit">Salvar</Button>
      </div>
    </form>
  )
}
