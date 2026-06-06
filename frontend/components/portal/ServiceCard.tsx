import Link from 'next/link'
import type { Service } from '@/lib/types/service'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { Clock, DollarSign } from 'lucide-react'

interface Props {
  service: Service
  slug: string
}

export function ServiceCard({ service, slug }: Props) {
  return (
    <Card className="flex flex-col">
      <CardHeader>
        <CardTitle className="text-lg">{service.name}</CardTitle>
        {service.description && (
          <p className="text-sm text-slate-500">{service.description}</p>
        )}
      </CardHeader>
      <CardContent className="flex-1 flex flex-col justify-end gap-4">
        <div className="flex items-center gap-4 text-sm text-slate-600">
          <span className="flex items-center gap-1">
            <Clock className="h-4 w-4" /> {service.durationMinutes} min
          </span>
          <span className="flex items-center gap-1">
            <DollarSign className="h-4 w-4" /> R$ {service.price.toFixed(2)}
          </span>
        </div>
        <Link href={`/${slug}/agendar?serviceId=${service.id}`} className={cn(buttonVariants(), 'w-full justify-center')}>Agendar</Link>
      </CardContent>
    </Card>
  )
}
