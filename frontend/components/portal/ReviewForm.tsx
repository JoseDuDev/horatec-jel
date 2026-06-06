'use client'

import { useState } from 'react'
import { Star } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface ReviewSubmitData {
  bookingId: string
  stars: number
  comment?: string
}

interface Props {
  bookingId: string
  onSubmit: (data: ReviewSubmitData) => void
  onCancel: () => void
}

export function ReviewForm({ bookingId, onSubmit, onCancel }: Props) {
  const [stars, setStars] = useState(0)
  const [hovered, setHovered] = useState(0)
  const [comment, setComment] = useState('')

  const handleSubmit = () => {
    onSubmit({
      bookingId,
      stars,
      comment: comment.trim() || undefined,
    })
  }

  return (
    <div className="space-y-4 p-4 border rounded-lg bg-slate-50">
      <p className="font-medium text-sm">Como foi sua experiência?</p>

      <div className="flex gap-1">
        {[1, 2, 3, 4, 5].map(s => (
          <button
            key={s}
            type="button"
            aria-label={`${s} estrela${s > 1 ? 's' : ''}`}
            onClick={() => setStars(s)}
            onMouseEnter={() => setHovered(s)}
            onMouseLeave={() => setHovered(0)}
            className="focus:outline-none"
          >
            <Star
              className={cn(
                'h-8 w-8 transition-colors',
                s <= (hovered || stars)
                  ? 'fill-amber-400 text-amber-400'
                  : 'text-slate-300'
              )}
            />
          </button>
        ))}
      </div>

      <textarea
        value={comment}
        onChange={e => setComment(e.target.value)}
        placeholder="Conte como foi... (opcional)"
        className="w-full border rounded-lg px-3 py-2 text-sm min-h-[70px]"
      />

      <div className="flex gap-2">
        <Button variant="outline" size="sm" onClick={onCancel}>Cancelar</Button>
        <Button size="sm" onClick={handleSubmit} disabled={stars === 0}>
          Enviar avaliação
        </Button>
      </div>
    </div>
  )
}
