import { apiFetch, apiDownload } from './client'
import type { CustomerReportItem } from '../types/report'

// Nota: o ReportsController herda a rota-base `api/v1/reports` ([controller]) e
// cada action acrescenta `reports/...`, então o caminho real é DUPLICADO:
// `/api/v1/reports/reports/customers`. Manter em sincronia com o backend.
export const reportsApi = {
  customers: () =>
    apiFetch<CustomerReportItem[]>('/api/v1/reports/reports/customers'),

  exportCustomersCsv: () =>
    apiDownload('/api/v1/reports/reports/customers/export', 'clientes.csv'),
}
