'use client'

import { useEffect, useState, useCallback } from 'react'
import { format, subDays } from 'date-fns'
import { financeiroApi } from '@/lib/api/financeiro'
import { RevenueChart } from '@/components/financeiro/RevenueChart'
import type { FinancialTransaction, FinancialSummary, RentalFinancialSummary } from '@/lib/types/financeiro'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'

export default function FinanceiroPage() {
  const [transactions, setTransactions] = useState<FinancialTransaction[]>([])
  const [summary, setSummary] = useState<FinancialSummary | null>(null)
  const [rental, setRental] = useState<RentalFinancialSummary | null>(null)
  const [from, setFrom] = useState(format(subDays(new Date(), 30), 'yyyy-MM-dd'))
  const [to, setTo] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    setLoading(true)
    const params = { from: `${from}T00:00:00`, to: `${to}T23:59:59` }
    Promise.all([
      financeiroApi.list(params),
      financeiroApi.summary(params),
      financeiroApi.rentalSummary(params).catch(() => null),
    ])
      .then(([t, s, r]) => { setTransactions(t); setSummary(s); setRental(r) })
      .finally(() => setLoading(false))
  }, [from, to])

  useEffect(() => { load() }, [load])

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Financeiro</h1>

      <div className="flex gap-4">
        <Input type="date" value={from} onChange={e => setFrom(e.target.value)} className="w-40" />
        <Input type="date" value={to} onChange={e => setTo(e.target.value)} className="w-40" />
      </div>

      {summary && (
        <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
          {[
            { title: 'Receita Bruta', value: `R$ ${summary.totalRevenue.toFixed(2)}` },
            { title: 'Reembolsos', value: `R$ ${summary.totalRefunded.toFixed(2)}` },
            { title: 'Receita Líquida', value: `R$ ${summary.netRevenue.toFixed(2)}` },
          ].map(m => (
            <Card key={m.title}>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs text-slate-500">{m.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-xl font-bold">{m.value}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {rental && rental.rentalCount > 0 && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Locações ({rental.rentalCount})</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
              {[
                { title: 'Receita de Diárias', value: rental.rentalRevenue },
                { title: 'Multas por Atraso', value: rental.lateFeesCollected },
                { title: 'Caução Retida', value: rental.depositsHeld },
                { title: 'Caução Estornada', value: rental.depositsRefunded },
              ].map(m => (
                <div key={m.title}>
                  <p className="text-xs text-slate-500">{m.title}</p>
                  <p className="text-lg font-bold">R$ {m.value.toFixed(2)}</p>
                </div>
              ))}
            </div>
            <p className="mt-3 text-xs text-slate-400">
              Receita líquida de locação (diárias + multas): R$ {rental.netRevenue.toFixed(2)}.
              A caução é valor de passagem (cobrada e estornada), não entra na receita.
            </p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle>Receita por Dia</CardTitle></CardHeader>
        <CardContent>
          <RevenueChart transactions={transactions} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Transações</CardTitle></CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Data</TableHead>
                <TableHead>Cliente</TableHead>
                <TableHead>Serviço</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Valor</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {loading ? (
                <TableRow><TableCell colSpan={6} className="text-center py-8">Carregando...</TableCell></TableRow>
              ) : transactions.map(t => (
                <TableRow key={t.id}>
                  <TableCell>{format(new Date(t.createdAt), 'dd/MM/yyyy')}</TableCell>
                  <TableCell>{t.customerName}</TableCell>
                  <TableCell>{t.serviceName}</TableCell>
                  <TableCell>
                    <Badge variant={t.type === 'Refund' ? 'destructive' : 'default'}>
                      {t.type === 'Refund' ? 'Reembolso' : 'Pagamento'}
                    </Badge>
                  </TableCell>
                  <TableCell>{t.status}</TableCell>
                  <TableCell>R$ {t.amount.toFixed(2)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}
