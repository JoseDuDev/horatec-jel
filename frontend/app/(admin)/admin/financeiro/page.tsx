'use client'

import { useEffect, useState, useCallback } from 'react'
import { format, subDays } from 'date-fns'
import { financeiroApi } from '@/lib/api/financeiro'
import { RevenueChart } from '@/components/financeiro/RevenueChart'
import type { FinancialTransaction, FinancialSummary } from '@/lib/types/financeiro'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'

export default function FinanceiroPage() {
  const [transactions, setTransactions] = useState<FinancialTransaction[]>([])
  const [summary, setSummary] = useState<FinancialSummary | null>(null)
  const [from, setFrom] = useState(format(subDays(new Date(), 30), 'yyyy-MM-dd'))
  const [to, setTo] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [loading, setLoading] = useState(true)

  const load = useCallback(() => {
    setLoading(true)
    const params = { from: `${from}T00:00:00`, to: `${to}T23:59:59` }
    Promise.all([financeiroApi.list(params), financeiroApi.summary(params)])
      .then(([t, s]) => { setTransactions(t); setSummary(s) })
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
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {[
            { title: 'Receita Bruta', value: `R$ ${summary.totalRevenue.toFixed(2)}` },
            { title: 'Reembolsos', value: `R$ ${summary.totalRefunds.toFixed(2)}` },
            { title: 'Receita Líquida', value: `R$ ${summary.netRevenue.toFixed(2)}` },
            { title: 'Agendamentos Pagos', value: `${summary.paidBookings}/${summary.totalBookings}` },
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
