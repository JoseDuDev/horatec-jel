import { apiFetch } from './client'
import type { FinancialTransaction, FinancialSummary } from '../types/financeiro'

export const financeiroApi = {
  list: (params: { from: string; to: string; serviceId?: string; resourceId?: string }) => {
    const qs = new URLSearchParams(
      Object.entries(params).filter(([, v]) => v != null) as [string, string][]
    ).toString()
    return apiFetch<FinancialTransaction[]>(`/api/v1/financeiro?${qs}`)
  },

  summary: (params: { from: string; to: string }) => {
    const qs = new URLSearchParams(params).toString()
    return apiFetch<FinancialSummary>(`/api/v1/financeiro/summary?${qs}`)
  },
}
