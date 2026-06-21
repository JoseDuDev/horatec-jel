import { portalApi } from '@/lib/api/portal'
import { RentalCatalog } from '@/components/portal/RentalCatalog'

interface Props {
  params: Promise<{ slug: string }>
}

export default async function AlugarPage({ params }: Props) {
  const { slug } = await params
  const items = await portalApi.rentalItems(slug).catch(() => [])

  return (
    <div className="max-w-5xl mx-auto px-4 py-12">
      <h1 className="text-3xl font-bold mb-8">Alugar</h1>
      <RentalCatalog slug={slug} items={items} />
    </div>
  )
}
