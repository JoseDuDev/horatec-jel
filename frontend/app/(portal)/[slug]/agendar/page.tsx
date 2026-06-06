import { portalApi } from '@/lib/api/portal'
import { BookingWizard } from '@/components/portal/BookingWizard'

interface Props {
  params: Promise<{ slug: string }>
  searchParams: Promise<{ serviceId?: string }>
}

export default async function AgendarPage({ params, searchParams }: Props) {
  const { slug } = await params
  const { serviceId } = await searchParams

  const [services, resources] = await Promise.all([
    portalApi.services(slug).catch(() => []),
    portalApi.resources(slug).catch(() => []),
  ])

  return (
    <div className="max-w-5xl mx-auto px-4 py-12">
      <h1 className="text-3xl font-bold mb-8">Agendar</h1>
      <BookingWizard
        slug={slug}
        services={services}
        resources={resources}
        initialServiceId={serviceId}
      />
    </div>
  )
}
