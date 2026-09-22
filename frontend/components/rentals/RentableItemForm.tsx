'use client'

import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Trash2 } from 'lucide-react'

import type { RentableItem, UpdateRentableItemRequest } from '@/lib/types/rental'
import { MAX_ITEM_IMAGES } from '@/lib/api/rentals'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ImagePicker } from '@/components/ui/image-picker'
import { RentableItemGallery } from '@/components/rentals/RentableItemGallery'

const schema = z.object({
  name: z.string().min(1, 'Nome obrigatório'),
  quantity: z.coerce.number().min(1, 'Estoque mínimo 1'),
  dailyRate: z.coerce.number().min(0, 'Diária inválida'),
  securityDeposit: z.coerce.number().min(0, 'Caução inválida'),
  bufferDays: z.coerce.number().min(0, 'Buffer inválido'),
  description: z.string().optional(),
  category: z.string().optional(),
  isActive: z.boolean(),
})

type FormData = z.infer<typeof schema>

interface Props {
  /** Ausente = cadastro. Presente = edição do item já existente. */
  initial?: RentableItem
  /**
   * No cadastro as fotos vêm separadas dos campos: só depois que a API devolve o
   * id do item é que elas têm para onde ir. Na edição a galeria salva sozinha e
   * `photos` volta vazio.
   */
  onSubmit: (data: UpdateRentableItemRequest, photos: File[]) => void
  onCancel: () => void
  /** Recarrega o item depois de mexer na galeria (só na edição). */
  onGalleryChange?: () => void | Promise<void>
}

export function RentableItemForm({ initial, onSubmit, onCancel, onGalleryChange }: Props) {
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormData>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    defaultValues: {
      name: initial?.name ?? '',
      quantity: initial?.quantity ?? 1,
      dailyRate: initial?.dailyRate ?? 0,
      securityDeposit: initial?.securityDeposit ?? 0,
      bufferDays: initial?.bufferDays ?? 0,
      description: initial?.description ?? '',
      category: initial?.category ?? '',
      isActive: initial?.isActive ?? true,
    },
  })

  const [photos, setPhotos] = useState<File[]>([])

  const previews = useMemo(() => photos.map(f => URL.createObjectURL(f)), [photos])

  // Cada preview é um object URL; sem revogar, os bytes da foto ficam presos na
  // memória da aba até um reload.
  useEffect(() => () => previews.forEach(URL.revokeObjectURL), [previews])

  const addPhotos = (files: File[]) =>
    setPhotos(current => [...current, ...files].slice(0, MAX_ITEM_IMAGES))

  const removePhoto = (index: number) =>
    setPhotos(current => current.filter((_, i) => i !== index))

  return (
    <form onSubmit={handleSubmit(data => onSubmit(data, photos))} className="space-y-4">
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

      <div>
        <Label>Fotos</Label>

        {initial ? (
          // Item já existe: cada foto sobe na hora, sem esperar o Salvar.
          <div className="mt-2">
            <RentableItemGallery
              itemId={initial.id}
              images={initial.images}
              onChange={onGalleryChange ?? (() => {})}
            />
          </div>
        ) : (
          <>
            {previews.length > 0 && (
              <div className="mt-2 grid grid-cols-3 gap-2">
                {previews.map((url, index) => (
                  <div key={url} className="relative overflow-hidden rounded-md border">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img src={url} alt={`Foto ${index + 1}`} className="aspect-square w-full object-cover" />
                    {index === 0 && (
                      <span className="absolute left-1 top-1 rounded bg-slate-900/80 px-1.5 py-0.5 text-[10px] font-medium text-white">
                        Capa
                      </span>
                    )}
                    <Button
                      type="button"
                      size="icon-sm"
                      variant="destructive"
                      title="Remover foto"
                      className="absolute bottom-1 right-1"
                      onClick={() => removePhoto(index)}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </div>
                ))}
              </div>
            )}

            {photos.length < MAX_ITEM_IMAGES && (
              <ImagePicker
                multiple
                className="mt-2"
                onSelect={addPhotos}
                label="Adicionar fotos"
                helpText={`Até ${MAX_ITEM_IMAGES} fotos · a primeira é a capa do catálogo`}
              />
            )}
          </>
        )}
      </div>

      {initial && (
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox" className="rounded" {...register('isActive')} />
          <span className="text-sm">
            Ativo <span className="text-slate-400">— desmarcado, some da vitrine</span>
          </span>
        </label>
      )}

      <div className="flex gap-2 justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Salvando...' : 'Salvar'}
        </Button>
      </div>
    </form>
  )
}
