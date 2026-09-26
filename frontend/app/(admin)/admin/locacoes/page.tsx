'use client'

import { useCallback, useEffect, useState } from 'react'
import { addDays, format, subDays } from 'date-fns'
import { Pencil, Plus } from 'lucide-react'

import { MAX_ITEM_IMAGES, rentalsApi } from '@/lib/api/rentals'
import { bookingsApi } from '@/lib/api/bookings'
import { performBookingAction } from '@/lib/bookings/actions'
import { RentableItemForm } from '@/components/rentals/RentableItemForm'
import {
  BookingTable,
  type BookingAction,
  type BookingActionOptions,
} from '@/components/bookings/BookingTable'
import type { RentableItem, UpdateRentableItemRequest } from '@/lib/types/rental'
import type { Booking } from '@/lib/types/booking'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

/**
 * Locações = **Reservas** (o dia a dia: retirar, devolver, estornar a caução) e
 * **Itens** (o cadastro do catálogo).
 *
 * As duas abas moram aqui porque o tenant que contratou só `Rentals` não vê o
 * menu Agendamentos — ele exige a capability `Appointments` (ver `Sidebar.tsx`).
 * Antes disso, uma locadora conseguia cadastrar itens e receber reservas pelo
 * portal, mas não tinha por onde registrar que o item saiu e voltou.
 */
export default function LocacoesPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Locações</h1>

      <Tabs defaultValue="reservas">
        <TabsList>
          <TabsTrigger value="reservas">Reservas</TabsTrigger>
          <TabsTrigger value="itens">Itens</TabsTrigger>
        </TabsList>

        <TabsContent value="reservas" className="mt-4">
          <ReservasTab />
        </TabsContent>

        <TabsContent value="itens" className="mt-4">
          <ItensTab />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function ReservasTab() {
  const [bookings, setBookings] = useState<Booking[]>([])
  const [loading, setLoading]   = useState(true)
  // Locação olha para a frente: o que importa é a reserva do próximo fim de
  // semana e o que ainda não voltou. Por isso a janela padrão vai até +30 dias,
  // ao contrário de /admin/agendamentos, que termina hoje.
  const [from, setFrom] = useState(format(subDays(new Date(), 7), 'yyyy-MM-dd'))
  const [to, setTo]     = useState(format(addDays(new Date(), 30), 'yyyy-MM-dd'))

  const load = useCallback(() => {
    setLoading(true)
    bookingsApi
      .list({ from: `${from}T00:00:00`, to: `${to}T23:59:59` })
      .then(all => setBookings(all.filter(b => b.kind === 'Rental')))
      .catch(() => setBookings([]))
      .finally(() => setLoading(false))
  }, [from, to])

  useEffect(() => { load() }, [load])

  const handleAction = async (
    action: BookingAction,
    id: string,
    opts?: BookingActionOptions
  ) => {
    const message = await performBookingAction(action, id, opts)
    if (message) alert(message)
    load()
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-4 flex-wrap items-center">
        <Input
          type="date"
          value={from}
          onChange={e => setFrom(e.target.value)}
          className="w-40"
        />
        <Input
          type="date"
          value={to}
          onChange={e => setTo(e.target.value)}
          className="w-40"
        />
        <p className="text-sm text-slate-500">
          Reservas do período — retirada, devolução e estorno da caução.
        </p>
      </div>

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <BookingTable bookings={bookings} onAction={handleAction} />
      )}

      <p className="text-xs text-slate-400">
        As reservas chegam pelo seu link público. Para reservar no balcão, abra o
        próprio link e faça a reserva pelo cliente.
      </p>
    </div>
  )
}

function ItensTab() {
  const [items, setItems] = useState<RentableItem[]>([])
  // 'new' = cadastro; um item = edição daquele item.
  const [editing, setEditing] = useState<RentableItem | 'new' | null>(null)
  // Erros na tela, não em alert(): o alert nativo é um aviso cru do navegador e
  // congela a página (e a automação do Chrome) até alguém clicar em OK.
  // saveError: o Salvar falhou, aparece DENTRO do diálogo, que fica aberto.
  // notice: o item foi criado mas uma foto não subiu — o diálogo já fechou.
  const [saveError, setSaveError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const openDialog = (target: RentableItem | 'new' | null) => {
    setSaveError(null)
    setEditing(target)
  }

  const load = () => rentalsApi.list().then(setItems).catch(() => setItems([]))
  useEffect(() => { load() }, [])

  const handleSubmit = async (data: UpdateRentableItemRequest, photos: File[]) => {
    setSaveError(null)
    setNotice(null)
    try {
      if (editing !== 'new') {
        if (editing) await rentalsApi.update(editing.id, data)
        setEditing(null)
        load()
        return
      }

      // A API devolve o id do item; só então as fotos têm dono. Em série para não
      // estourar o limite de 6, que é conferido a cada envio.
      // isActive fica de fora: item nasce ativo.
      const itemId = await rentalsApi.create({
        name: data.name,
        quantity: data.quantity,
        dailyRate: data.dailyRate,
        securityDeposit: data.securityDeposit,
        bufferDays: data.bufferDays,
        description: data.description,
        category: data.category,
      })

      try {
        for (const photo of photos) {
          await rentalsApi.addImage(itemId, photo)
        }
      } catch (e) {
        // O item já existe — esconder o diálogo sem avisar faria a foto sumir sem
        // explicação. As que faltarem entram pelo botão Editar do card.
        const reason = e instanceof Error ? e.message : 'erro desconhecido'
        setNotice(`O item foi criado, mas uma das fotos não subiu (${reason}). Envie as que faltaram pelo botão Editar.`)
      }

      setEditing(null)
      load()
    } catch (e) {
      // Falha ao criar ou alterar o item: o diálogo FICA aberto, com o que foi
      // digitado, para corrigir e tentar de novo. Antes disto o Salvar não fazia
      // nada visível quando a API recusava — foi o que aconteceu em 23/09 com um
      // tenant sem o módulo de locação no plano: clique, e silêncio.
      setSaveError(e instanceof Error ? e.message : 'Não foi possível salvar o item.')
    }
  }

  // A galeria salva na hora, então o diálogo aberto precisa enxergar a lista
  // recarregada — senão continuaria mostrando as fotos de antes do envio.
  const reloadKeepingDialog = async () => {
    const fresh = await rentalsApi.list().catch(() => null)
    if (!fresh) return
    setItems(fresh)
    setEditing(current =>
      current && current !== 'new' ? fresh.find(i => i.id === current.id) ?? null : current)
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">
          O catálogo que aparece no seu link público.
        </p>
        <Button onClick={() => openDialog('new')}>
          <Plus className="h-4 w-4 mr-2" /> Novo Item
        </Button>
      </div>

      {notice && (
        <Alert variant="destructive">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      )}

      {items.length === 0 && (
        <p className="text-sm text-slate-500">Nenhum item de locação cadastrado.</p>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {items.map(i => (
          <Card key={i.id} className="overflow-hidden pt-0">
            {i.imageUrl ? (
              /* eslint-disable-next-line @next/next/no-img-element */
              <img
                src={i.imageUrl}
                alt={i.name}
                className="h-32 w-full object-cover"
              />
            ) : (
              <div className="flex h-32 w-full items-center justify-center bg-slate-100 text-xs text-slate-400">
                Sem foto
              </div>
            )}
            <CardHeader className="pb-2">
              <div className="flex items-center justify-between">
                <CardTitle className="text-base">{i.name}</CardTitle>
                <Badge variant={i.isActive ? 'default' : 'secondary'}>
                  {i.isActive ? 'Ativo' : 'Inativo'}
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-slate-500">
                R$ {i.dailyRate.toFixed(2)}/dia · {i.quantity} un. · caução R$ {i.securityDeposit.toFixed(2)}
              </p>
              {i.bufferDays > 0 && (
                <p className="text-xs text-slate-400 mt-1">Buffer de {i.bufferDays} dia(s) entre locações</p>
              )}
              {i.description && <p className="text-xs text-slate-400 mt-1">{i.description}</p>}
              <p className="text-xs text-slate-400 mt-1">
                {i.images.length} de {MAX_ITEM_IMAGES} fotos
              </p>
              <Button
                variant="outline"
                size="sm"
                className="mt-3 w-full"
                onClick={() => openDialog(i)}
              >
                <Pencil className="h-3 w-3 mr-1" /> Editar
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>

      <Dialog open={editing !== null} onOpenChange={open => !open && openDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {editing === 'new' ? 'Novo Item de Locação' : `Editar ${editing?.name ?? ''}`}
            </DialogTitle>
          </DialogHeader>
          {saveError && (
            <Alert variant="destructive">
              <AlertDescription>{saveError}</AlertDescription>
            </Alert>
          )}
          <RentableItemForm
            // Troca de item = formulário novo: sem a key, os campos do item anterior
            // sobreviveriam. Recarregar a galeria não remonta, porque o id não muda.
            key={editing === 'new' ? 'new' : editing?.id}
            initial={editing !== 'new' && editing !== null ? editing : undefined}
            onSubmit={handleSubmit}
            onCancel={() => openDialog(null)}
            onGalleryChange={reloadKeepingDialog}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}
