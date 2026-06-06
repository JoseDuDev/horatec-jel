import { portalApi } from '@/lib/api/portal'
import { ServiceCard } from '@/components/portal/ServiceCard'

interface Props {
  params: Promise<{ slug: string }>
  searchParams: Promise<{ q?: string; minPrice?: string; maxPrice?: string }>
}

export default async function CatalogoPage({ params, searchParams }: Props) {
  const { slug } = await params
  const { q: rawQ, minPrice: rawMin, maxPrice: rawMax } = await searchParams

  const services = await portalApi.services(slug).catch(() => [])
  const active = services.filter(s => s.isActive)

  const q = rawQ?.toLowerCase() ?? ''
  const minPrice = rawMin ? Number(rawMin) : 0
  const maxPrice = rawMax ? Number(rawMax) : Infinity

  const filtered = active.filter(s =>
    (s.name.toLowerCase().includes(q) || (s.description ?? '').toLowerCase().includes(q)) &&
    s.price >= minPrice &&
    s.price <= maxPrice
  )

  return (
    <div className="max-w-5xl mx-auto px-4 py-12">
      <h1 className="text-3xl font-bold mb-8">Nossos Serviços</h1>

      <form method="GET" className="flex gap-4 flex-wrap mb-8">
        <input
          name="q"
          defaultValue={rawQ}
          placeholder="Buscar serviço..."
          className="border rounded-md px-3 py-2 text-sm flex-1 min-w-48"
        />
        <input
          name="minPrice"
          type="number"
          defaultValue={rawMin}
          placeholder="Preço mín."
          className="border rounded-md px-3 py-2 text-sm w-32"
        />
        <input
          name="maxPrice"
          type="number"
          defaultValue={rawMax}
          placeholder="Preço máx."
          className="border rounded-md px-3 py-2 text-sm w-32"
        />
        <button type="submit" className="bg-slate-900 text-white px-4 py-2 rounded-md text-sm">
          Filtrar
        </button>
      </form>

      {filtered.length === 0 ? (
        <p className="text-slate-500">Nenhum serviço encontrado.</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map(s => (
            <ServiceCard key={s.id} service={s} slug={slug} />
          ))}
        </div>
      )}
    </div>
  )
}
