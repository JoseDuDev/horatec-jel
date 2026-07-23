import Link from 'next/link'
import { portalApi } from '@/lib/api/portal'
import { ServiceCard } from '@/components/portal/ServiceCard'
import { ReviewCard } from '@/components/portal/ReviewCard'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'
import { getActiveBrand } from '@/lib/brand.server'
import { hasCapability } from '@/lib/types/platform'

interface Props {
  params: Promise<{ slug: string }>
}

const brl = (v: number) => `R$ ${v.toFixed(2).replace('.', ',')}`

export default async function PortalHomePage({ params }: Props) {
  const { slug } = await params
  const brand = await getActiveBrand()

  // Capabilities do tenant definem quais módulos o portal exibe.
  const tenant = await portalApi.tenant(slug).catch(() => null)
  const caps = tenant?.capabilities ?? ''
  const known = caps.length > 0
  const showAppointments = known ? hasCapability(caps, 'Appointments') : true
  const showRentals = hasCapability(caps, 'Rentals')

  const [services, resources, rentalItems] = await Promise.all([
    showAppointments ? portalApi.services(slug).catch(() => []) : Promise.resolve([]),
    showAppointments ? portalApi.resources(slug).catch(() => []) : Promise.resolve([]),
    showRentals ? portalApi.rentalItems(slug).catch(() => []) : Promise.resolve([]),
  ])

  const activeServices = services.filter(s => s.isActive).slice(0, 6)
  const activeRentals = rentalItems.filter(i => i.isActive).slice(0, 6)

  const firstResourceId = resources[0]?.id
  const reviews = firstResourceId
    ? await portalApi.reviews(slug, firstResourceId).catch(() => [])
    : []
  const featuredReviews = reviews.slice(0, 3)

  // Hero prioriza o módulo da marca quando ambos estão disponíveis.
  const rentalPrimary = showRentals && (brand.primaryModule === 'Rentals' || !showAppointments)

  return (
    <div>
      {/* Hero */}
      <section className="bg-gradient-to-br from-slate-900 to-slate-700 text-white py-20">
        <div className="max-w-5xl mx-auto px-4 text-center">
          {rentalPrimary ? (
            <>
              <h1 className="text-4xl font-bold mb-4">Alugue com facilidade</h1>
              <p className="text-slate-300 mb-8 text-lg">
                Itens disponíveis para locação, reserva rápida e segura.
              </p>
              <div className="flex gap-3 justify-center flex-wrap">
                <Link href={`/${slug}/alugar`} className={cn(buttonVariants({ size: 'lg' }), 'bg-white text-slate-900 hover:bg-slate-100')}>Ver itens para alugar</Link>
                {showAppointments && (
                  <Link href={`/${slug}/agendar`} className={cn(buttonVariants({ size: 'lg', variant: 'outline' }), 'border-white/40 bg-transparent text-white hover:bg-white/10')}>Agendar</Link>
                )}
              </div>
            </>
          ) : (
            <>
              <h1 className="text-4xl font-bold mb-4">Agende agora</h1>
              <p className="text-slate-300 mb-8 text-lg">
                Serviços de qualidade, agendamento fácil e rápido.
              </p>
              <div className="flex gap-3 justify-center flex-wrap">
                <Link href={`/${slug}/agendar`} className={cn(buttonVariants({ size: 'lg' }), 'bg-white text-slate-900 hover:bg-slate-100')}>Agendar agora</Link>
                {showRentals && (
                  <Link href={`/${slug}/alugar`} className={cn(buttonVariants({ size: 'lg', variant: 'outline' }), 'border-white/40 bg-transparent text-white hover:bg-white/10')}>Alugar</Link>
                )}
              </div>
            </>
          )}
        </div>
      </section>

      {/* Serviços em destaque */}
      {showAppointments && activeServices.length > 0 && (
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

      {/* Itens para locação */}
      {showRentals && activeRentals.length > 0 && (
        <section className="max-w-5xl mx-auto px-4 py-16">
          <div className="flex items-center justify-between mb-8">
            <h2 className="text-2xl font-bold">Itens para locação</h2>
            <Link href={`/${slug}/alugar`} className="text-sm text-indigo-600 hover:underline">
              Ver todos →
            </Link>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {activeRentals.map(item => (
              <Card key={item.id} className="flex flex-col">
                <CardHeader>
                  <CardTitle className="text-lg">{item.name}</CardTitle>
                  {item.description && (
                    <p className="text-sm text-slate-500">{item.description}</p>
                  )}
                </CardHeader>
                <CardContent className="flex-1 flex flex-col justify-end gap-4">
                  <div className="text-sm text-slate-600">
                    <span className="font-medium">{brl(item.dailyRate)}</span>/dia
                    {item.securityDeposit > 0 && (
                      <span className="block text-xs text-slate-400 mt-1">Caução {brl(item.securityDeposit)}</span>
                    )}
                  </div>
                  <Link href={`/${slug}/alugar`} className={cn(buttonVariants(), 'w-full justify-center')}>Alugar</Link>
                </CardContent>
              </Card>
            ))}
          </div>
        </section>
      )}

      {/* Equipe */}
      {showAppointments && resources.length > 0 && (
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
      {showAppointments && featuredReviews.length > 0 && (
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
