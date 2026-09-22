/**
 * Preparo das imagens antes do upload.
 *
 * A foto sai do celular com 4 MB e 4000px de largura; a vitrine mostra um card de
 * 400px. Reduzir aqui, no browser, evita subir esse peso pela rede, guardar no
 * disco do servidor e devolvê-lo a cada visita do catálogo. O servidor continua
 * conferindo tamanho e formato — isto é conveniência, não é a validação.
 */

/** Teto que o servidor aplica (ImageUploadPolicy.MaxBytes). */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024

export const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp']

/** Aceita no seletor de arquivo do sistema. */
export const IMAGE_ACCEPT_ATTR = ACCEPTED_IMAGE_TYPES.join(',')

/** Maior lado depois da redução — dá conta de card, galeria e zoom no celular. */
const MAX_EDGE = 1600

/** Abaixo disso não vale reencodar: só perderia qualidade. */
const SKIP_BELOW_BYTES = 400 * 1024

const JPEG_QUALITY = 0.82

export async function prepareImageForUpload(file: File): Promise<File> {
  if (!file.type.startsWith('image/')) return file
  if (typeof createImageBitmap !== 'function') return file

  try {
    const bitmap = await createImageBitmap(file)
    const longestEdge = Math.max(bitmap.width, bitmap.height)
    const scale = Math.min(1, MAX_EDGE / longestEdge)

    if (scale === 1 && file.size <= SKIP_BELOW_BYTES) {
      bitmap.close()
      return file
    }

    const canvas = document.createElement('canvas')
    canvas.width  = Math.round(bitmap.width * scale)
    canvas.height = Math.round(bitmap.height * scale)

    const ctx = canvas.getContext('2d')
    if (!ctx) {
      bitmap.close()
      return file
    }

    // PNG com transparência vira JPEG aqui; sem este fundo, o que era transparente
    // sairia preto.
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
    bitmap.close()

    const blob = await new Promise<Blob | null>(resolve =>
      canvas.toBlob(resolve, 'image/jpeg', JPEG_QUALITY)
    )

    if (!blob || blob.size >= file.size) return file

    return new File([blob], toJpgName(file.name), {
      type: 'image/jpeg',
      lastModified: Date.now(),
    })
  } catch {
    // Formato que o browser não decodifica (HEIC, por exemplo): manda como veio e
    // deixa o servidor recusar com a mensagem certa.
    return file
  }
}

function toJpgName(name: string): string {
  const base = name.replace(/\.[^.]+$/, '') || 'foto'
  return `${base}.jpg`
}
