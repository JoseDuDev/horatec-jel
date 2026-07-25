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
  id: 'mjml',
  name: 'MJML',
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
    id: 'agenda',
    name: 'Agenda',
    tagline: 'Agende serviços com facilidade',
    primaryModule: 'Appointments',
    defaultCapabilities: 'Appointments',
    themeColor: '#6366f1',
  },
  {
    id: 'alugue',
    name: 'Alugue',
    tagline: 'Alugue itens de forma simples e segura',
    primaryModule: 'Rentals',
    defaultCapabilities: 'Rentals',
    themeColor: '#0f766e',
  },
]

interface HostRule {
  /** Host que ativa a regra (ex.: "alugue.mjml.com.br"). */
  match: string
  /**
   * Como comparar `match` com o host:
   *   - 'domain'    → host igual a `match` ou terminado em `.match`.
   *   - 'substring' → `match` aparece em qualquer posição do host.
   *
   * As marcas embutidas usam 'domain' porque os nomes viraram palavras comuns:
   * com 'substring', o tenant `agenda-da-maria.alugue.mjml.com.br` casaria com
   * a regra "agenda" e receberia a marca errada. 'substring' segue sendo o
   * padrão de `BRAND_CONFIG` por compatibilidade.
   */
  mode?: 'domain' | 'substring'
  /** Id de uma marca embutida. */
  brandId?: string
  /** Sobrescrita/definição inline de marca. */
  brand?: Partial<Brand>
}

function hostMatches(host: string, rule: HostRule): boolean {
  const match = rule.match.toLowerCase()
  if (!match) return false

  return rule.mode === 'domain'
    ? host === match || host.endsWith(`.${match}`)
    : host.includes(match)
}

/**
 * `BRAND_CONFIG` (env, só no servidor) permite sobrescrever o mapeamento
 * host→marca sem rebuild. Formato JSON, valor por host pode ser:
 *   - string: id de uma marca embutida — `{ "meudominio.com": "alugue" }`
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

/**
 * Regras default (aplicadas após as de `BRAND_CONFIG`).
 *
 * O domínio da plataforma vem de `NEXT_PUBLIC_PLATFORM_DOMAIN` para o mesmo
 * bundle servir produção e ambientes de teste. Precisa ser NEXT_PUBLIC_ porque
 * a marca também é resolvida no cliente.
 */
const PLATFORM_DOMAIN = process.env.NEXT_PUBLIC_PLATFORM_DOMAIN ?? 'mjml.com.br'

const DEFAULT_HOST_RULES: HostRule[] = [
  { match: `agenda.${PLATFORM_DOMAIN}`, mode: 'domain', brandId: 'agenda' },
  { match: `alugue.${PLATFORM_DOMAIN}`, mode: 'domain', brandId: 'alugue' },
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
    if (!hostMatches(normalizedHost, rule)) continue
    const base = resolveById(rule.brandId) ?? DEFAULT_BRAND
    return rule.brand ? { ...base, ...rule.brand, id: rule.brand.id ?? base.id } : base
  }
  return DEFAULT_BRAND
}
