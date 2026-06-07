'use client'

import { useState, useEffect } from 'react'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import type { VoucherValidationResult } from '@/lib/types/wallet'
import { portalWalletApi } from '@/lib/api/wallet'
import { usePortalAuthStore } from '@/store/portal-auth'
import { Button } from '@/components/ui/button'

export interface CheckoutOptions {
  voucherCode?: string
  useWalletCredits: boolean
}

interface Props {
  service: Service
  resource: Resource
  slot: string
  notes: string
  onNotesChange: (v: string) => void
  onConfirm: (opts: CheckoutOptions) => void
  loading: boolean
}

export function WizardStepConfirm({
  service, resource, slot, notes, onNotesChange, onConfirm, loading,
}: Props) {
  const { accessToken } = usePortalAuthStore()

  const [walletBalance, setWalletBalance] = useState(0)
  const [useWallet, setUseWallet] = useState(false)
  const [voucherInput, setVoucherInput] = useState('')
  const [appliedVoucher, setAppliedVoucher] = useState<VoucherValidationResult | null>(null)
  const [voucherError, setVoucherError] = useState<string | null>(null)
  const [applyingVoucher, setApplyingVoucher] = useState(false)

  useEffect(() => {
    if (!accessToken) return
    portalWalletApi.getWallet(accessToken)
      .then(w => setWalletBalance(w.balance))
      .catch(() => {})
  }, [accessToken])

  const handleApplyVoucher = async () => {
    if (!voucherInput.trim() || !accessToken) return
    setApplyingVoucher(true)
    setVoucherError(null)
    try {
      const result = await portalWalletApi.validateVoucher(
        accessToken, voucherInput.trim(), service.price)
      setAppliedVoucher(result)
    } catch (err) {
      setVoucherError(err instanceof Error ? err.message : 'Voucher inválido')
      setAppliedVoucher(null)
    } finally {
      setApplyingVoucher(false)
    }
  }

  const handleRemoveVoucher = () => {
    setAppliedVoucher(null)
    setVoucherInput('')
    setVoucherError(null)
  }

  const priceAfterVoucher = service.price - (appliedVoucher?.discountAmount ?? 0)
  const walletDebit = useWallet ? Math.min(walletBalance, priceAfterVoucher) : 0
  const totalToPay = Math.max(0, priceAfterVoucher - walletDebit)

  return (
    <div>
      <h2 className="text-xl font-bold mb-6">Confirme seu agendamento</h2>

      {/* Resumo */}
      <div className="border rounded-lg p-6 space-y-3 mb-6">
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Serviço</span>
          <span className="font-medium">{service.name}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Profissional</span>
          <span className="font-medium">{resource.name}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Data e hora</span>
          <span className="font-medium">
            {format(new Date(slot), "dd 'de' MMMM 'às' HH:mm", { locale: ptBR })}
          </span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-slate-500">Duração</span>
          <span className="font-medium">{service.durationMinutes} min</span>
        </div>

        <div className="border-t pt-3 space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-slate-500">Subtotal</span>
            <span>R$ {service.price.toFixed(2).replace('.', ',')}</span>
          </div>
          {appliedVoucher && (
            <div className="flex justify-between text-sm text-green-600">
              <span>Desconto ({appliedVoucher.code})</span>
              <span>− R$ {appliedVoucher.discountAmount.toFixed(2).replace('.', ',')}</span>
            </div>
          )}
          {useWallet && walletDebit > 0 && (
            <div className="flex justify-between text-sm text-indigo-600">
              <span>Créditos da carteira</span>
              <span>− R$ {walletDebit.toFixed(2).replace('.', ',')}</span>
            </div>
          )}
          <div className="flex justify-between font-bold text-base border-t pt-2">
            <span>Total a pagar</span>
            <span>R$ {totalToPay.toFixed(2).replace('.', ',')}</span>
          </div>
          {totalToPay === 0 && (
            <p className="text-xs text-green-600 text-center">
              Agendamento coberto integralmente pelos seus créditos.
            </p>
          )}
        </div>
      </div>

      {/* Voucher */}
      <div className="mb-4">
        <p className="text-sm font-medium text-slate-700 mb-2">Cupom de desconto</p>
        {appliedVoucher ? (
          <div className="flex items-center gap-2 p-3 bg-green-50 border border-green-200 rounded-lg">
            <span className="text-sm text-green-700 font-mono font-bold">{appliedVoucher.code}</span>
            <span className="text-xs text-green-600">
              {appliedVoucher.discountType === 'Percentage'
                ? `${appliedVoucher.discountValue}% de desconto`
                : `R$ ${appliedVoucher.discountValue.toFixed(2).replace('.', ',')} de desconto`}
            </span>
            <button
              onClick={handleRemoveVoucher}
              className="ml-auto text-xs text-slate-400 hover:text-slate-600"
            >
              Remover
            </button>
          </div>
        ) : (
          <div className="flex gap-2">
            <input
              value={voucherInput}
              onChange={e => setVoucherInput(e.target.value.toUpperCase())}
              placeholder="CÓDIGO DO VOUCHER"
              className="flex-1 border rounded-lg px-3 py-2 text-sm font-mono uppercase"
            />
            <Button
              variant="outline"
              size="sm"
              onClick={handleApplyVoucher}
              disabled={!voucherInput.trim() || applyingVoucher}
            >
              {applyingVoucher ? '...' : 'Aplicar'}
            </Button>
          </div>
        )}
        {voucherError && <p className="text-xs text-red-500 mt-1">{voucherError}</p>}
      </div>

      {/* Carteira */}
      {walletBalance > 0 && (
        <div className="mb-6">
          <label className="flex items-center gap-3 cursor-pointer p-3 border rounded-lg hover:bg-slate-50">
            <input
              type="checkbox"
              checked={useWallet}
              onChange={e => setUseWallet(e.target.checked)}
              className="h-4 w-4"
            />
            <div>
              <p className="text-sm font-medium">Usar créditos da carteira</p>
              <p className="text-xs text-slate-500">
                Saldo disponível: R$ {walletBalance.toFixed(2).replace('.', ',')}
              </p>
            </div>
          </label>
        </div>
      )}

      {/* Observações */}
      <div className="mb-6">
        <label className="block text-sm font-medium text-slate-700 mb-1">
          Observações (opcional)
        </label>
        <textarea
          value={notes}
          onChange={e => onNotesChange(e.target.value)}
          placeholder="Alguma preferência ou observação?"
          className="w-full border rounded-lg px-3 py-2 text-sm min-h-[80px]"
        />
      </div>

      <Button
        onClick={() => onConfirm({
          voucherCode: appliedVoucher?.code,
          useWalletCredits: useWallet,
        })}
        disabled={loading}
        size="lg"
        className="w-full"
      >
        {loading
          ? 'Processando...'
          : totalToPay === 0
            ? 'Confirmar agendamento'
            : `Confirmar e pagar R$ ${totalToPay.toFixed(2).replace('.', ',')}`}
      </Button>
    </div>
  )
}
