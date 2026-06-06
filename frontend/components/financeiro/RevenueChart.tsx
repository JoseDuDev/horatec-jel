'use client'

import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import type { FinancialTransaction } from '@/lib/types/financeiro'
import { format } from 'date-fns'

interface Props {
  transactions: FinancialTransaction[]
}

export function RevenueChart({ transactions }: Props) {
  const byDay = transactions
    .filter(t => t.type === 'Payment' && t.status === 'Paid')
    .reduce<Record<string, number>>((acc, t) => {
      const day = format(new Date(t.createdAt), 'dd/MM')
      acc[day] = (acc[day] ?? 0) + t.amount
      return acc
    }, {})

  const data = Object.entries(byDay).map(([date, total]) => ({ date, total }))

  return (
    <ResponsiveContainer width="100%" height={300}>
      <BarChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="date" />
        <YAxis tickFormatter={v => `R$${v}`} />
        <Tooltip formatter={(v: number) => [`R$ ${v.toFixed(2)}`, 'Receita']} />
        <Bar dataKey="total" fill="#6366f1" radius={[4, 4, 0, 0]} />
      </BarChart>
    </ResponsiveContainer>
  )
}
