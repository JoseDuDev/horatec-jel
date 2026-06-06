import Link from 'next/link'
import { notFound } from 'next/navigation'
import { portalApi } from '@/lib/api/portal'
import { ReviewCard } from '@/components/portal/ReviewCard'
import { ServiceCard } from '@/components/portal/ServiceCard'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { Clock, DollarSign } from 'lucide-react'

interface Props {
  params: Promise<{ slug: string; id: string }>
}

export default async function ServiceDetailPage({ params }: Props) {
  const { slug, id } = await params

  const services = await portalApi.services(slug).catch(() => [])
  const service = services.find(s => s.id === id)
  if (!service) notFound()

  const resources = await portalApi.resources(slug).catch(() => [])
  const capableResources = resources.filter(
    r => r.isActive && r.serviceIds.includes(id)
  )

  const allReviews = await Promise.all(
    capableResources.slice(0, 3).map(r =>
      portalApi.reviews(slug, r.id).catch(() => [])
    )
  )
  const reviews = allReviews.flat().slice(0, 6)
  const avgStars = reviews.length
    ? (reviews.reduce((sum, r) => sum + r.stars, 0) / reviews.length).toFixed(1)
    : null

  return (
    <div className="max-w-3xl mx-auto px-4 py-12">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">{service.name}</h1>
        {service.description && <p className="text-slate-600">{service.description}</p>}
        <div className="flex items-center gap-6 mt-4 text-slate-700">
          <span className="flex items-center gap-2">
            <Clock className="h-5 w-5" /> {service.durationMinutes} minutos
          </span>
          <span className="flex items-center gap-2">
            <DollarSign className="h-5 w-5" /> R$ {service.price.toFixed(2)}
          </span>
          {avgStars && <span>⭐ {avgStars}</span>}
        </div>
      </div>

      <Link href={`/${slug}/agendar?serviceId=${id}`} className={cn(buttonVariants({ size: 'lg' }), 'mb-12')}>Agendar este serviço</Link>

      {capableResources.length > 0 && (
        <section className="mb-12">
          <h2 className="text-xl font-bold mb-4">Quem realiza</h2>
          <div className="flex gap-4 flex-wrap">
            {capableResources.map(r => (
              <div key={r.id} className="flex items-center gap-3 border rounded-lg p-3">
                <div className="h-10 w-10 rounded-full bg-slate-200 flex items-center justify-center font-bold text-slate-600">
                  {r.name[0]}
                </div>
                <div>
                  <p className="font-medium">{r.name}</p>
                  <p className="text-xs text-slate-500">{r.type}</p>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {reviews.length > 0 && (
        <section className="mb-12">
          <h2 className="text-xl font-bold mb-4">Avaliações</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            {reviews.map(r => <ReviewCard key={r.id} review={r} />)}
          </div>
        </section>
      )}

      {(() => {
        const related = services
          .filter(s => s.isActive && s.id !== id)
          .filter(s => service.categoryId ? s.categoryId === service.categoryId : true)
          .slice(0, 3)

        if (related.length === 0) return null

        return (
          <section className="mt-4">
            <h2 className="text-xl font-bold mb-4">Você também pode gostar</h2>
            <div className="grid gap-4 sm:grid-cols-3">
              {related.map(s => (
                <ServiceCard key={s.id} service={s} slug={slug} />
              ))}
            </div>
          </section>
        )
      })()}
    </div>
  )
}
