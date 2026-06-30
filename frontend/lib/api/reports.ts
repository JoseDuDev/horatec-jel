import { apiFetch, apiDownload } from './client'
import type { CustomerReportItem } from '../types/report'

export const reportsApi = {
  customers: () =>
    apiFetch<CustomerReportItem[]>('/api/v1/reports/customers'),

  exportCustomersCsv: () =>
    apiDownload('/api/v1/reports/customers/export', 'clientes.csv'),
}
