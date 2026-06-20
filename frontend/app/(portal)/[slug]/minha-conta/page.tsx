'use client'

import { use, useEffect, useState } from 'react'
import Link from 'next/link'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { usePortalAuthStore } from '@/store/portal-auth'
import { portalApi } from '@/lib/api/portal'
import type { CustomerBooking, FavoriteService } from '@/lib/types/portal'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { GoogleSignInButton } from '@/components/portal/GoogleSignInButton'
import { cn } from '@/lib/utils'
import { HeartOff } from 'lucide-react'
import { ReviewForm } from '@/components/portal/ReviewForm'
import { WalletWidget } from '@/components/portal/WalletWidget'

const STATUS_LABEL: Record<string, string> = {
  Pending: 'Pendente', Confirmed: 'Confirmado', Completed: 'Concluído',
  Cancelled: 'Cancelado', NoShow: 'Não compareceu',
}

interface Props {
  params: Promise<{ slug: string }>
}

export default function MinhaContaPage({ params }: Props) {
  const { slug } = use(params)
  const { customer, accessToken } = usePortalAuthStore()
  const [bookings, setBookings] = useState<CustomerBooking[]>([])
  const [favorites, setFavorites] = useState<FavoriteService[]>([])
  const [loading, setLoading] = useState(true)
  const [reviewingBookingId, setReviewingBookingId] = useState<string | null>(null)
  const [cancellingId, setCancellingId] = useState<string | null>(null)
  const [cancelError, setCancelError] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) { setLoading(false); return }
    Promise.all([
      portalApi.myBookings(slug, accessToken).catch(() => []),
      portalApi.myFavorites(slug, accessToken).catch(() => []),
    ]).then(([b, f]) => { setBookings(b); setFavorites(f) })
      .finally(() => setLoading(false))
  }, [slug, accessToken])

  const handleReviewSubmit = async (data: { bookingId: string; stars: number; comment?: string }) => {
    if (!accessToken) return
    try {
      await portalApi.createReview(slug, accessToken, data.bookingId, data.stars, data.comment)
      setReviewingBookingId(null)
    } catch {
      alert('Erro ao enviar avaliação.')
    }
  }

  const handleCancel = async (bookingId: string) => {
    if (!accessToken) return
    setCancelError(null)
    try {
      await portalApi.cancelBooking(slug, accessToken, bookingId)
      setBookings(prev => prev.filter(b => b.id !== bookingId))
      setCancellingId(null)
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Erro ao cancelar'
      setCancelError(
        msg.includes('CancellationNotAllowed')
          ? 'Cancelamentos não são permitidos por este estabelecimento.'
          : msg.includes('CancellationWindowClosed')
            ? 'O prazo para cancelamento gratuito já encerrou.'
            : msg
      )
    }
  }

  const handleRemoveFavorite = async (serviceId: string) => {
    if (!accessToken) return
    await portalApi.removeFavorite(slug, accessToken, serviceId)
    setFavorites(f => f.filter(fav => fav.serviceId !== serviceId))
  }

  if (!customer) {
    return (
      <div className="max-w-lg mx-auto px-4 py-20 text-center">
        <h1 className="text-2xl font-bold mb-4">Minha Conta</h1>
        <p className="text-slate-500 mb-6">Entre com sua conta Google para ver seus agendamentos.</p>
        <GoogleSignInButton slug={slug} />
      </div>
    )
  }

  const upcoming = bookings.filter(b => b.status === 'Pending' || b.status === 'Confirmed')
  const past = bookings.filter(b => b.status === 'Completed' || b.status === 'Cancelled' || b.status === 'NoShow')

  return (
    <div className="max-w-3xl mx-auto px-4 py-12">
      <div className="flex items-center gap-4 mb-8">
        <div className="h-16 w-16 rounded-full bg-slate-200 flex items-center justify-center text-2xl font-bold text-slate-600">
          {customer.name[0]}
        </div>
        <div>
          <h1 className="text-2xl font-bold">{customer.name}</h1>
          <p className="text-slate-500">{customer.email}</p>
        </div>
      </div>

      <Tabs defaultValue="agendamentos">
        <TabsList className="mb-6">
          <TabsTrigger value="agendamentos">Agendamentos</TabsTrigger>
          <TabsTrigger value="favoritos">Favoritos</TabsTrigger>
          <TabsTrigger value="carteira">Carteira</TabsTrigger>
        </TabsList>

        <TabsContent value="agendamentos">
          {loading ? <p className="text-slate-500">Carregando...</p> : (
            <div className="space-y-6">
              {upcoming.length > 0 && (
                <div>
                  <h2 className="font-semibold mb-3">Próximos</h2>
                  <div className="space-y-3">
                    {upcoming.map(b => (
                      <Card key={b.id}>
                        <CardContent className="pt-4 flex items-center justify-between">
                          <div>
                            <p className="font-medium">{b.serviceName}</p>
                            <p className="text-sm text-slate-500">{b.resourceName}</p>
                            <p className="text-sm text-slate-500">
                              {format(new Date(b.scheduledAt), "dd/MM/yyyy 'às' HH:mm", { locale: ptBR })}
                            </p>
                          </div>
                          <div className="flex flex-col items-end gap-2">
                            <Badge>{STATUS_LABEL[b.status]}</Badge>
                            <Link href={`/${slug}/agendar/${b.id}/status`} className="text-xs text-indigo-600 hover:underline">
                              Ver detalhes
                            </Link>
                            {cancellingId === b.id ? (
                              <div className="flex flex-col items-end gap-1">
                                {cancelError && (
                                  <p className="text-xs text-red-500 text-right max-w-[180px]">{cancelError}</p>
                                )}
                                <div className="flex gap-2">
                                  <Button
                                    size="sm"
                                    variant="destructive"
                                    onClick={() => handleCancel(b.id)}
                                  >
                                    Confirmar cancelamento
                                  </Button>
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    onClick={() => { setCancellingId(null); setCancelError(null) }}
                                  >
                                    Voltar
                                  </Button>
                                </div>
                              </div>
                            ) : (
                              <Button
                                size="sm"
                                variant="ghost"
                                className="text-xs text-red-500 hover:text-red-700 h-auto p-0"
                                onClick={() => { setCancellingId(b.id); setCancelError(null) }}
                              >
                                Cancelar
                              </Button>
                            )}
                          </div>
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                </div>
              )}

              {past.length > 0 && (
                <div>
                  <h2 className="font-semibold mb-3">Histórico</h2>
                  <div className="space-y-3">
                    {past.map(b => (
                      <Card key={b.id} className="opacity-80">
                        <CardContent className="pt-4">
                          <div className="flex items-center justify-between mb-2">
                            <div>
                              <p className="font-medium">{b.serviceName}</p>
                              <p className="text-sm text-slate-500">
                                {format(new Date(b.scheduledAt), "dd/MM/yyyy", { locale: ptBR })}
                              </p>
                            </div>
                            <div className="flex flex-col items-end gap-2">
                              <Badge variant="outline">{STATUS_LABEL[b.status]}</Badge>
                              {b.status === 'Completed' && reviewingBookingId !== b.id && (
                                <Button size="sm" variant="outline" onClick={() => setReviewingBookingId(b.id)}>
                                  Avaliar
                                </Button>
                              )}
                            </div>
                          </div>
                          {b.status === 'Completed' && reviewingBookingId === b.id && (
                            <ReviewForm
                              bookingId={b.id}
                              onSubmit={handleReviewSubmit}
                              onCancel={() => setReviewingBookingId(null)}
                            />
                          )}
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                </div>
              )}

              {bookings.length === 0 && (
                <div className="text-center py-12">
                  <p className="text-slate-500 mb-4">Você ainda não tem agendamentos.</p>
                  <Link href={`/${slug}/agendar`} className={cn(buttonVariants())}>Agendar agora</Link>
                </div>
              )}
            </div>
          )}
        </TabsContent>

        <TabsContent value="favoritos">
          {loading ? <p className="text-slate-500">Carregando...</p> : favorites.length === 0 ? (
            <div className="text-center py-12">
              <p className="text-slate-500 mb-4">Nenhum serviço favoritado.</p>
              <Link href={`/${slug}/servicos`} className={cn(buttonVariants({ variant: 'outline' }))}>Ver serviços</Link>
            </div>
          ) : (
            <div className="space-y-3">
              {favorites.map(fav => (
                <Card key={fav.id}>
                  <CardContent className="pt-4 flex items-center justify-between">
                    <div>
                      <p className="text-sm text-slate-500">Serviço favorito</p>
                      <Link
                        href={`/${slug}/servicos/${fav.serviceId}`}
                        className="font-medium hover:underline text-indigo-700"
                      >
                        Ver serviço →
                      </Link>
                    </div>
                    <div className="flex gap-2">
                      <Link href={`/${slug}/agendar?serviceId=${fav.serviceId}`} className={cn(buttonVariants({ size: 'sm', variant: 'outline' }))}>Agendar</Link>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => handleRemoveFavorite(fav.serviceId)}
                      >
                        <HeartOff className="h-4 w-4" />
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="carteira">
          <WalletWidget token={accessToken ?? ''} slug={slug} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
