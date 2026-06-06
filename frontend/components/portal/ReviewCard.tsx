import type { PortalReview } from '@/lib/types/portal'
import { Card, CardContent } from '@/components/ui/card'
import { Star } from 'lucide-react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'

interface Props {
  review: PortalReview
}

export function ReviewCard({ review }: Props) {
  return (
    <Card>
      <CardContent className="pt-4">
        <div className="flex items-center gap-1 mb-2">
          {Array.from({ length: 5 }, (_, i) => (
            <Star
              key={i}
              className={`h-4 w-4 ${i < review.stars ? 'fill-amber-400 text-amber-400' : 'text-slate-200'}`}
            />
          ))}
        </div>
        {review.comment && <p className="text-sm text-slate-700">{review.comment}</p>}
        <p className="text-xs text-slate-400 mt-2">
          {format(new Date(review.createdAt), "dd 'de' MMMM", { locale: ptBR })}
        </p>
      </CardContent>
    </Card>
  )
}
