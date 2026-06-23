'use client'

import { useEffect, useState } from 'react'
import { walletApi } from '@/lib/api/wallet'
import { useAuthStore } from '@/store/auth'
import type { VoucherSummary, VoucherDiscountType, CreateVoucherRequest } from '@/lib/types/wallet'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

export default function CarteiraPage() {
  const token = useAuthStore(s => s.accessToken)
  const slug = useAuthStore(s => s.tenantSlug)

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-slate-900">Carteira & Vouchers</h1>
      <Tabs defaultValue="vouchers">
        <TabsList>
          <TabsTrigger value="vouchers">Vouchers</TabsTrigger>
          <TabsTrigger value="creditos">Créditos</TabsTrigger>
        </TabsList>
        <TabsContent value="vouchers" className="mt-4">
          <VouchersTab token={token ?? ''} slug={slug ?? ''} />
        </TabsContent>
        <TabsContent value="creditos" className="mt-4">
          <CreditosTab token={token ?? ''} slug={slug ?? ''} />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function VouchersTab({ token, slug }: { token: string; slug: string }) {
  const [vouchers, setVouchers] = useState<VoucherSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [form, setForm] = useState<CreateVoucherRequest>({
    code: '',
    discountType: 'Percentage',
    discountValue: 10,
  })
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = () => {
    setLoading(true)
    walletApi.getVouchers(slug, token).then(setVouchers).finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [token, slug])

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    setCreating(true)
    setError(null)
    try {
      await walletApi.createVoucher(slug, token, form)
      setForm({ code: '', discountType: 'Percentage', discountValue: 10 })
      load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao criar voucher')
    } finally {
      setCreating(false)
    }
  }

  const handleDeactivate = async (id: string) => {
    await walletApi.deactivateVoucher(slug, token, id)
    load()
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle>Novo Voucher</CardTitle></CardHeader>
        <CardContent>
          <form onSubmit={handleCreate} className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <div className="space-y-1">
              <Label>Código</Label>
              <Input
                value={form.code}
                onChange={e => setForm(f => ({ ...f, code: e.target.value.toUpperCase() }))}
                placeholder="PROMO20"
                required
              />
            </div>
            <div className="space-y-1">
              <Label>Tipo</Label>
              <Select
                value={form.discountType}
                onValueChange={(v) => setForm(f => ({ ...f, discountType: v as VoucherDiscountType }))}
              >
                <SelectTrigger>
                  {/* base-ui exibe o valor cru por padrão; mapeamos para o rótulo. */}
                  <SelectValue>
                    {(value) => (value === 'Fixed' ? 'Fixo (R$)' : 'Percentual')}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Percentage">Percentual</SelectItem>
                  <SelectItem value="Fixed">Fixo (R$)</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Valor</Label>
              <Input
                type="number"
                min={0.01}
                step={0.01}
                value={form.discountValue}
                onChange={e => setForm(f => ({ ...f, discountValue: parseFloat(e.target.value) }))}
                required
              />
            </div>
            <div className="flex items-end">
              <Button type="submit" disabled={creating} className="w-full">
                {creating ? 'Criando…' : 'Criar'}
              </Button>
            </div>
          </form>
          {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Vouchers Ativos</CardTitle></CardHeader>
        <CardContent>
          {loading ? (
            <p className="text-sm text-slate-500">Carregando…</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Código</TableHead>
                  <TableHead>Tipo</TableHead>
                  <TableHead>Valor</TableHead>
                  <TableHead>Usos</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {vouchers.map(v => (
                  <TableRow key={v.id}>
                    <TableCell className="font-mono font-medium">{v.code}</TableCell>
                    <TableCell>{v.discountType === 'Percentage' ? 'Percentual' : 'Fixo'}</TableCell>
                    <TableCell>
                      {v.discountType === 'Percentage'
                        ? `${v.discountValue}%`
                        : `R$ ${v.discountValue.toFixed(2)}`}
                    </TableCell>
                    <TableCell>{v.usedCount}{v.maxUses ? ` / ${v.maxUses}` : ''}</TableCell>
                    <TableCell>
                      <Badge variant={v.isActive ? 'default' : 'secondary'}>
                        {v.isActive ? 'Ativo' : 'Inativo'}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {v.isActive && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600 hover:text-red-700"
                          onClick={() => handleDeactivate(v.id)}
                        >
                          Desativar
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
                {vouchers.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} className="text-center text-slate-500 py-8">
                      Nenhum voucher cadastrado
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function CreditosTab({ token, slug }: { token: string; slug: string }) {
  const [userId, setUserId] = useState('')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    setMessage(null)
    try {
      await walletApi.addCredits(slug, token, userId, parseFloat(amount), description)
      setMessage({ type: 'success', text: `R$ ${amount} adicionados com sucesso.` })
      setAmount('')
      setDescription('')
    } catch (err) {
      setMessage({ type: 'error', text: err instanceof Error ? err.message : 'Erro ao adicionar créditos' })
    } finally {
      setLoading(false)
    }
  }

  return (
    <Card className="max-w-md">
      <CardHeader><CardTitle>Adicionar Créditos</CardTitle></CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <Label>ID do usuário</Label>
            <Input
              value={userId}
              onChange={e => setUserId(e.target.value)}
              placeholder="UUID do usuário"
              required
            />
          </div>
          <div className="space-y-1">
            <Label>Valor (R$)</Label>
            <Input
              type="number"
              min={0.01}
              step={0.01}
              value={amount}
              onChange={e => setAmount(e.target.value)}
              required
            />
          </div>
          <div className="space-y-1">
            <Label>Descrição</Label>
            <Input
              value={description}
              onChange={e => setDescription(e.target.value)}
              placeholder="Crédito promocional"
              required
            />
          </div>
          <Button type="submit" disabled={loading} className="w-full">
            {loading ? 'Adicionando…' : 'Adicionar Créditos'}
          </Button>
          {message && (
            <p className={`text-sm ${message.type === 'success' ? 'text-green-600' : 'text-red-600'}`}>
              {message.text}
            </p>
          )}
        </form>
      </CardContent>
    </Card>
  )
}
