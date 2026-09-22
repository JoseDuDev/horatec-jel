'use client'

import { useRef, useState, type DragEvent } from 'react'
import { ImagePlus, Loader2 } from 'lucide-react'

import { cn } from '@/lib/utils'
import {
  ACCEPTED_IMAGE_TYPES,
  IMAGE_ACCEPT_ATTR,
  MAX_IMAGE_BYTES,
  prepareImageForUpload,
} from '@/lib/images'

interface Props {
  /** Recebe os arquivos já reduzidos e validados. */
  onSelect: (files: File[]) => void | Promise<void>
  multiple?: boolean
  disabled?: boolean
  /** Texto da área de seleção (ex.: "Adicionar fotos"). */
  label?: string
  helpText?: string
  className?: string
}

/**
 * Seleção de imagem por clique ou arrastar-e-soltar.
 *
 * Só escolhe e prepara o arquivo — quem decide o que fazer com ele (subir agora,
 * guardar até o cadastro existir) é quem usa o componente. É o que permite o mesmo
 * seletor servir a galeria de um item já criado e o formulário de um item que
 * ainda nem tem id.
 */
export function ImagePicker({
  onSelect,
  multiple = false,
  disabled = false,
  label = 'Adicionar foto',
  helpText,
  className,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [dragging, setDragging] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleFiles = async (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return

    const picked = multiple ? Array.from(fileList) : [fileList[0]]

    const invalidType = picked.find(f => !ACCEPTED_IMAGE_TYPES.includes(f.type))
    if (invalidType) {
      setError('Formato não suportado. Envie JPG, PNG ou WebP.')
      return
    }

    setError(null)
    setBusy(true)
    try {
      const prepared = await Promise.all(picked.map(prepareImageForUpload))

      // O servidor também recusa, mas avisar aqui evita subir 8 MB para nada.
      const tooBig = prepared.find(f => f.size > MAX_IMAGE_BYTES)
      if (tooBig) {
        setError('Imagem muito grande, mesmo depois de reduzida. Tente outra foto.')
        return
      }

      await onSelect(prepared)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível enviar a imagem.')
    } finally {
      setBusy(false)
      // Zera o input para que escolher o MESMO arquivo de novo dispare onChange.
      if (inputRef.current) inputRef.current.value = ''
    }
  }

  const onDrop = (e: DragEvent<HTMLButtonElement>) => {
    e.preventDefault()
    setDragging(false)
    if (!disabled && !busy) void handleFiles(e.dataTransfer.files)
  }

  return (
    <div className={className}>
      <button
        type="button"
        disabled={disabled || busy}
        onClick={() => inputRef.current?.click()}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={cn(
          'flex w-full flex-col items-center justify-center gap-1 rounded-lg border border-dashed px-4 py-6 text-sm transition-colors',
          'text-slate-500 hover:border-slate-400 hover:bg-slate-50',
          'disabled:cursor-not-allowed disabled:opacity-50',
          dragging && 'border-slate-400 bg-slate-50'
        )}
      >
        {busy
          ? <Loader2 className="h-5 w-5 animate-spin" />
          : <ImagePlus className="h-5 w-5" />}
        <span>{busy ? 'Enviando...' : label}</span>
        <span className="text-xs text-slate-400">
          {helpText ?? 'Arraste aqui ou clique para escolher · JPG, PNG ou WebP'}
        </span>
      </button>

      <input
        ref={inputRef}
        type="file"
        accept={IMAGE_ACCEPT_ATTR}
        multiple={multiple}
        className="hidden"
        onChange={e => void handleFiles(e.target.files)}
      />

      {error && <p className="mt-1 text-sm text-red-500">{error}</p>}
    </div>
  )
}
