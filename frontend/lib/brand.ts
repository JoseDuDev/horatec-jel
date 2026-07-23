/**
 * Marca de produto — camada ACIMA do tema por-tenant (`Tenant.Theme`).
 *
 * A separação "Agendamento vs Aluguel" é publicada como marcas distintas sobre
 * o MESMO backend: a marca é resolvida pelo host da requisição. As capabilities
 * continuam por-tenant (ver `TenantCapability` / `hasCapability`) e controlam
 * QUAIS módulos aparecem para aquele tenant, independente da marca de origem.
 *
 * Este módulo é isomórfico (sem `next/headers`) — pode ser importado tanto por
 * Server quanto por Client Components. A resolução server-side fica em
 * `brand.server.ts`.
 */

export type BrandModule = 'Appointments' | 'Rentals'

export interface Brand {
  /** Chave estável da marca. */
  id: string
  /** Nome exibido (topo do admin, títulos, "Powered by ..."). */
  name: string
  /** Frase curta usada em metadata e no hero do portal. */
  tagline: string
  /** Módulo que a marca prioriza na comunicação e no portal público. */
  primaryModule: BrandModule
  /**
   * Capabilities padrão de um tenant criado por esta marca, no formato CSV do
   * backend (ex.: "Appointments" | "Rentals" | "Appointments, Rentals").
   */
  defaultCapabilities: string
  /** Cor base da marca (usada em `<meta name="theme-color">`). */
  themeColor: string
}

/** Marca-mãe / fallback quando o host não casa com nenhuma marca configurada. */
export const DEFAULT_BRAND: Brand = {
  id: 'horafy',
  name: 'Horafy',
  tagline: 'Agendamentos e locações online, fáceis e rápidos',
  primaryModule: 'Appointments',
  defaultCapabilities: 'Appointments, Rentals',
  themeColor: '#6366f1',
}

/**
 * Marcas conhecidas embutidas. Os nomes abaixo são exemplos e podem ser
 * trocados/estendidos em runtime via a env `BRAND_CONFIG` (ver abaixo).
 */
const BUILTIN_BRANDS: Brand[] = [
  {
    id: 'agendafy',
    name: 'Agendafy',
    tagline: 'Agende serviços com facilidade',
    primaryModule: 'Appointments',
    defaultCapabilities: 'Appointments',
    themeColor: '#6366f1',
  },
  {
    id: 'alugafy',
    name: 'Alugafy',
    tagline: 'Alugue itens de forma simples e segura',
    primaryModule: 'Rentals',
    defaultCapabilities: 'Rentals',
    themeColor: '#0f766e',
  },
]

interface HostRule {
  /** Substring do host que ativa a regra (ex.: "alugafy"). */
  match: string
  /** Id de uma marca embutida. */
  brandId?: string
  /** Sobrescrita/definição inline de marca. */
  brand?: Partial<Brand>
}

/**
 * `BRAND_CONFIG` (env, só no servidor) permite sobrescrever o mapeamento
 * host→marca sem rebuild. Formato JSON, valor por host pode ser:
 *   - string: id de uma marca embutida — `{ "meudominio.com": "alugafy" }`
 *   - objeto: marca inline/parcial — `{ "x.com": { "name": "X", "primaryModule": "Rentals", ... } }`
 */
function parseConfigRules(raw: string | undefined): HostRule[] {
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw) as Record<string, string | Partial<Brand>>
    return Object.entries(parsed).map(([match, value]) =>
      typeof value === 'string' ? { match, brandId: value } : { match, brand: value }
    )
  } catch {
    return []
  }
}

/** Regras default (aplicadas após as de `BRAND_CONFIG`). */
const DEFAULT_HOST_RULES: HostRule[] = [
  { match: 'agendafy', brandId: 'agendafy' },
  { match: 'alugafy', brandId: 'alugafy' },
]

function resolveById(id: string | undefined): Brand | undefined {
  if (!id) return undefined
  if (id === DEFAULT_BRAND.id) return DEFAULT_BRAND
  return BUILTIN_BRANDS.find(b => b.id === id)
}

/**
 * Resolve a marca ativa a partir do host. `configRaw` normalmente vem de
 * `process.env.BRAND_CONFIG`. A primeira regra cujo `match` é substring do host
 * vence; sem correspondência, retorna a marca-mãe.
 */
export function resolveBrandFromHost(host: string | null | undefined, configRaw?: string): Brand {
  const normalizedHost = (host ?? '').toLowerCase()
  const rules = [...parseConfigRules(configRaw), ...DEFAULT_HOST_RULES]

  for (const rule of rules) {
    if (!rule.match || !normalizedHost.includes(rule.match.toLowerCase())) continue
    const base = resolveById(rule.brandId) ?? DEFAULT_BRAND
    return rule.brand ? { ...base, ...rule.brand, id: rule.brand.id ?? base.id } : base
  }
  return DEFAULT_BRAND
}
