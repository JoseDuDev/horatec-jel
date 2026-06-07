'use client'

import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import { portalWalletApi } from '@/lib/api/wallet'
import type { WalletResult, WalletTransactionType } from '@/lib/types/wallet'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'

const TYPE_LABEL: Record<WalletTransactionType, string> = {
  CreditAdded: 'Crédito adicionado',
  BookingPayment: 'Pagamento de agendamento',
  BookingRefund: 'Reembolso',
  LoyaltyBonus: 'Bônus de fidelidade',
}

export function WalletWidget({ token }: { token: string }) {
  const [wallet, setWallet] = useState<WalletResult | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    portalWalletApi.getWallet(token)
      .then(setWallet)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [token])

  if (loading) return <p className="text-sm text-slate-500">Carregando carteira…</p>

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="pt-6 flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-500">Saldo disponível</p>
            <p className="text-3xl font-bold text-slate-900">
              R$ {(wallet?.balance ?? 0).toFixed(2).replace('.', ',')}
            </p>
          </div>
        </CardContent>
      </Card>

      {wallet && wallet.transactions.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-base">Últimas movimentações</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            {wallet.transactions.slice(0, 5).map(t => (
              <div key={t.id} className="flex items-center justify-between text-sm">
                <div>
                  <p className="font-medium">{TYPE_LABEL[t.type] ?? t.type}</p>
                  <p className="text-xs text-slate-500">{t.description}</p>
                  <p className="text-xs text-slate-400">
                    {format(new Date(t.createdAt), "dd/MM/yyyy 'às' HH:mm", { locale: ptBR })}
                  </p>
                </div>
                <Badge
                  variant={t.type === 'BookingPayment' ? 'destructive' : 'default'}
                  className="font-mono"
                >
                  {t.type === 'BookingPayment' ? '-' : '+'}
                  R$ {t.amount.toFixed(2).replace('.', ',')}
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {wallet && wallet.transactions.length === 0 && (
        <p className="text-sm text-slate-500 text-center py-4">Nenhuma movimentação ainda.</p>
      )}
    </div>
  )
}
