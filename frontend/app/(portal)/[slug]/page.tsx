import Link from 'next/link'
import { portalApi } from '@/lib/api/portal'
import { ServiceCard } from '@/components/portal/ServiceCard'
import { ReviewCard } from '@/components/portal/ReviewCard'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface Props {
  params: Promise<{ slug: string }>
}

export default async function PortalHomePage({ params }: Props) {
  const { slug } = await params

  const [services, resources] = await Promise.all([
    portalApi.services(slug).catch(() => []),
    portalApi.resources(slug).catch(() => []),
  ])

  const activeServices = services.filter(s => s.isActive).slice(0, 6)

  const firstResourceId = resources[0]?.id
  const reviews = firstResourceId
    ? await portalApi.reviews(slug, firstResourceId).catch(() => [])
    : []

  const featuredReviews = reviews.slice(0, 3)

  return (
    <div>
      {/* Hero */}
      <section className="bg-gradient-to-br from-slate-900 to-slate-700 text-white py-20">
        <div className="max-w-5xl mx-auto px-4 text-center">
          <h1 className="text-4xl font-bold mb-4">Agende agora</h1>
          <p className="text-slate-300 mb-8 text-lg">
            Serviços de qualidade, agendamento fácil e rápido.
          </p>
          <Link href={`/${slug}/agendar`} className={cn(buttonVariants({ size: 'lg' }), 'bg-white text-slate-900 hover:bg-slate-100')}>Agendar agora</Link>
        </div>
      </section>

      {/* Serviços em destaque */}
      {activeServices.length > 0 && (
        <section className="max-w-5xl mx-auto px-4 py-16">
          <div className="flex items-center justify-between mb-8">
            <h2 className="text-2xl font-bold">Nossos serviços</h2>
            <Link href={`/${slug}/servicos`} className="text-sm text-indigo-600 hover:underline">
              Ver todos →
            </Link>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {activeServices.map(s => (
              <ServiceCard key={s.id} service={s} slug={slug} />
            ))}
          </div>
        </section>
      )}

      {/* Equipe */}
      {resources.length > 0 && (
        <section className="bg-slate-50 py-16">
          <div className="max-w-5xl mx-auto px-4">
            <h2 className="text-2xl font-bold mb-8">Nossa equipe</h2>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {resources.filter(r => r.isActive).map(r => (
                <div key={r.id} className="text-center">
                  <div className="h-20 w-20 rounded-full bg-slate-200 mx-auto mb-3 flex items-center justify-center text-2xl font-bold text-slate-500">
                    {r.name[0]}
                  </div>
                  <p className="font-medium">{r.name}</p>
                  <p className="text-sm text-slate-500">{r.type}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* Avaliações */}
      {featuredReviews.length > 0 && (
        <section className="max-w-5xl mx-auto px-4 py-16">
          <h2 className="text-2xl font-bold mb-8">O que nossos clientes dizem</h2>
          <div className="grid gap-4 sm:grid-cols-3">
            {featuredReviews.map(r => (
              <ReviewCard key={r.id} review={r} />
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
