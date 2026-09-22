'use client'

import { useState } from 'react'
import { Star, Trash2 } from 'lucide-react'

import { MAX_ITEM_IMAGES, rentalsApi } from '@/lib/api/rentals'
import type { RentableItemImage } from '@/lib/types/rental'
import { Button } from '@/components/ui/button'
import { ImagePicker } from '@/components/ui/image-picker'

interface Props {
  itemId: string
  images: RentableItemImage[]
  /** Recarrega o item depois de cada mudança — a API é a fonte da ordem. */
  onChange: () => void | Promise<void>
}

/**
 * Galeria de um item já cadastrado: envia, escolhe a capa e remove fotos.
 *
 * A capa é a primeira da lista e é o que aparece no card do catálogo; as demais
 * abrem no detalhe do item.
 */
export function RentableItemGallery({ itemId, images, onChange }: Props) {
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const full = images.length >= MAX_ITEM_IMAGES

  const run = async (id: string, action: () => Promise<unknown>) => {
    setBusyId(id)
    setError(null)
    try {
      await action()
      await onChange()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível concluir a ação.')
    } finally {
      setBusyId(null)
    }
  }

  const upload = async (files: File[]) => {
    setError(null)
    try {
      // Em série, não em paralelo: o limite de 6 é conferido no servidor a cada
      // envio, e disparar tudo de uma vez deixaria passar do teto.
      for (const file of files.slice(0, MAX_ITEM_IMAGES - images.length)) {
        await rentalsApi.addImage(itemId, file)
      }
    } finally {
      // Mesmo que uma das fotos falhe, as que subiram antes precisam aparecer.
      await onChange()
    }
  }

  return (
    <div className="space-y-3">
      {images.length > 0 && (
        <div className="grid grid-cols-3 gap-2">
          {images.map((image, index) => (
            <div key={image.id} className="group relative overflow-hidden rounded-md border">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={image.url}
                alt={index === 0 ? 'Capa do item' : `Foto ${index + 1}`}
                className="aspect-square w-full object-cover"
              />

              {index === 0 && (
                <span className="absolute left-1 top-1 rounded bg-slate-900/80 px-1.5 py-0.5 text-[10px] font-medium text-white">
                  Capa
                </span>
              )}

              <div className="absolute inset-x-0 bottom-0 flex justify-end gap-1 bg-gradient-to-t from-slate-900/70 to-transparent p-1">
                {index !== 0 && (
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="secondary"
                    title="Tornar capa"
                    disabled={busyId === image.id}
                    onClick={() => run(image.id, () => rentalsApi.setCoverImage(itemId, image.id))}
                  >
                    <Star className="h-3 w-3" />
                  </Button>
                )}
                <Button
                  type="button"
                  size="icon-sm"
                  variant="destructive"
                  title="Remover foto"
                  disabled={busyId === image.id}
                  onClick={() => run(image.id, () => rentalsApi.removeImage(itemId, image.id))}
                >
                  <Trash2 className="h-3 w-3" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {full ? (
        <p className="text-xs text-slate-400">
          Limite de {MAX_ITEM_IMAGES} fotos atingido. Remova uma para enviar outra.
        </p>
      ) : (
        <ImagePicker
          multiple
          onSelect={upload}
          label="Adicionar fotos"
          helpText={`Até ${MAX_ITEM_IMAGES} fotos · a primeira é a capa do catálogo`}
        />
      )}

      {error && <p className="text-sm text-red-500">{error}</p>}
    </div>
  )
}
