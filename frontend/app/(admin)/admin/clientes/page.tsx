'use client'

import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { Download } from 'lucide-react'
import { reportsApi } from '@/lib/api/reports'
import type { CustomerReportItem } from '@/lib/types/report'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow
} from '@/components/ui/table'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'

export default function ClientesPage() {
  const [customers, setCustomers] = useState<CustomerReportItem[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)

  useEffect(() => {
    reportsApi.customers()
      .then(setCustomers)
      .finally(() => setLoading(false))
  }, [])

  const handleExport = async () => {
    setExportError(null)
    setExporting(true)
    try {
      await reportsApi.exportCustomersCsv()
    } catch (e) {
      setExportError(e instanceof Error ? e.message : 'Falha ao exportar')
    } finally {
      setExporting(false)
    }
  }

  const filtered = customers.filter(
    c => c.name.toLowerCase().includes(search.toLowerCase()) ||
         c.email.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold text-slate-900">Clientes</h1>
        <Button onClick={handleExport} disabled={exporting || customers.length === 0} variant="outline">
          <Download className="h-4 w-4" />
          {exporting ? 'Exportando...' : 'Exportar CSV'}
        </Button>
      </div>

      {exportError && <p className="text-sm text-red-500">{exportError}</p>}

      <Input
        placeholder="Buscar por nome ou email..."
        value={search}
        onChange={e => setSearch(e.target.value)}
        className="max-w-xs"
      />
      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Cliente</TableHead>
              <TableHead>Telefone</TableHead>
              <TableHead>Agendamentos</TableHead>
              <TableHead>Valor Gasto</TableHead>
              <TableHead>Último Agendamento</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.map(c => (
              <TableRow key={c.customerId}>
                <TableCell>
                  <div className="font-medium">{c.name}</div>
                  <div className="text-xs text-slate-500">{c.email}</div>
                </TableCell>
                <TableCell>{c.phone || '—'}</TableCell>
                <TableCell>{c.bookingCount}</TableCell>
                <TableCell>R$ {c.totalSpent.toFixed(2)}</TableCell>
                <TableCell>
                  {c.lastBookingAt ? format(new Date(c.lastBookingAt), 'dd/MM/yyyy HH:mm') : '—'}
                </TableCell>
              </TableRow>
            ))}
            {filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center py-8 text-slate-500">
                  Nenhum cliente encontrado.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
