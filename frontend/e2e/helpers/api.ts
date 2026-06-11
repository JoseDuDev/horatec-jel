// Aponta para a API do docker-compose.e2e.yml (porta 8084)
const API = 'http://localhost:8084/api/v1'

// ── Tipos ────────────────────────────────────────────────────────────────────

export interface TenantSetup {
  tenantId: string
  slug: string
  ownerToken: string
  ownerEmail: string
  ownerName: string
}

export interface CustomerSetup {
  customerId: string
  customerToken: string
  customerEmail: string
  customerName: string
}

// ── Helpers internos ─────────────────────────────────────────────────────────

async function post(url: string, body: unknown, token?: string, slug?: string) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (slug) headers['X-Tenant-Slug'] = slug
  const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body) })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`POST ${url} → ${res.status}: ${text}`)
  }
  // 204 No Content não tem body
  if (res.status === 204) return null
  return res.json()
}

async function put(url: string, body: unknown, token: string, slug: string) {
  const res = await fetch(url, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
      'X-Tenant-Slug': slug,
    },
    body: JSON.stringify(body),
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`PUT ${url} → ${res.status}: ${text}`)
  }
}

// ── API pública ───────────────────────────────────────────────────────────────

/** Cria tenant + TenantOwner. Retorna credenciais do owner. */
export async function setupTenant(slug: string): Promise<TenantSetup> {
  const ownerEmail = `owner-${slug}@e2e.test`
  const ownerName = `Owner ${slug}`
  const data = await post(`${API}/platform/tenants`, {
    name: `Tenant ${slug}`,
    slug,
    vertical: 'Barbershop',
    ownerName,
    ownerEmail,
    ownerPassword: 'E2eTest1',
  })
  return {
    tenantId: data.tenantId,
    slug: data.slug,
    ownerToken: data.tokens.accessToken,
    ownerEmail,
    ownerName,
  }
}

/**
 * Cria serviço no tenant. Retorna o id criado.
 * O endpoint retorna o Guid diretamente (não um objeto): `"550e8400-..."`
 */
export async function createService(
  token: string,
  slug: string,
  opts: { name: string; durationMinutes: number; price: number }
): Promise<string> {
  const id = await post(`${API}/services`, {
    name: opts.name,
    durationMinutes: opts.durationMinutes,
    price: opts.price,
    description: null,
    category: null,
  }, token, slug)
  return id as string
}

/**
 * Cria recurso (profissional) no tenant. Retorna o id criado.
 * O endpoint retorna o Guid diretamente (não um objeto): `"550e8400-..."`
 */
export async function createResource(
  token: string,
  slug: string,
  name: string
): Promise<string> {
  const id = await post(`${API}/resources`, {
    name,
    type: 'Professional',
    email: null,
    phone: null,
    specialty: null,
    bio: null,
    avatarUrl: null,
    userId: null,
  }, token, slug)
  return id as string
}

/** Vincula serviço a recurso. */
export async function linkServiceToResource(
  token: string,
  slug: string,
  resourceId: string,
  serviceId: string
): Promise<void> {
  await post(`${API}/resources/${resourceId}/services/${serviceId}`, {}, token, slug)
}

/**
 * Define horários de funcionamento seg–sex 08:00–18:00.
 * Sábado e domingo ficam fechados (isOpen: false).
 */
export async function setBusinessHoursWeekdays(token: string, slug: string): Promise<void> {
  const days = [
    { day: 1, open: true },   // Monday
    { day: 2, open: true },   // Tuesday
    { day: 3, open: true },   // Wednesday
    { day: 4, open: true },   // Thursday
    { day: 5, open: true },   // Friday
    { day: 6, open: false },  // Saturday
    { day: 0, open: false },  // Sunday
  ]
  for (const { day, open } of days) {
    await put(`${API}/availability/business-hours`, {
      dayOfWeek: day,
      openTime: '08:00:00',
      closeTime: '18:00:00',
      isOpen: open,
    }, token, slug)
  }
}

/**
 * Cria/recupera cliente de teste via endpoint exclusivo de E2ETest.
 * Retorna credenciais do cliente.
 */
export async function customerTestLogin(
  email: string,
  slug: string
): Promise<CustomerSetup> {
  const data = await post(`${API}/customers/auth/test-login`, { email, tenantSlug: slug })
  // Decodifica o JWT para pegar o id (payload é base64url, campo "sub")
  const payload = JSON.parse(
    Buffer.from(data.accessToken.split('.')[1], 'base64url').toString('utf-8')
  )
  const name = email.split('@')[0]
  return {
    customerId: payload.sub,
    customerToken: data.accessToken,
    customerEmail: email,
    customerName: name,
  }
}

/** Ativa fidelidade no tenant com taxa percentual. */
export async function setLoyalty(
  token: string,
  slug: string,
  creditRatePercent: number
): Promise<void> {
  await put(`${API}/tenants/loyalty-settings`, {
    isEnabled: true,
    creditRatePercent,
    minBookingAmount: 0,
  }, token, slug)
}

/** Confirma agendamento (ação de admin/staff). */
export async function confirmBooking(
  token: string,
  slug: string,
  bookingId: string
): Promise<void> {
  const res = await fetch(`${API}/bookings/${bookingId}/confirm`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'X-Tenant-Slug': slug },
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`confirmBooking → ${res.status}: ${text}`)
  }
}

/** Marca agendamento como Concluído (ação de admin/staff). */
export async function completeBooking(
  token: string,
  slug: string,
  bookingId: string
): Promise<void> {
  const res = await fetch(`${API}/bookings/${bookingId}/complete`, {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}`, 'X-Tenant-Slug': slug },
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`completeBooking → ${res.status}: ${text}`)
  }
}

// ── StorageState helpers ──────────────────────────────────────────────────────

/**
 * Retorna o storageState do Playwright para autenticar o admin.
 * Injeta o Zustand store `horafy-auth` no localStorage.
 */
export function adminStorageState(setup: TenantSetup): object {
  // Decode JWT to get real owner ID (same approach as customerTestLogin)
  const payload = JSON.parse(
    Buffer.from(setup.ownerToken.split('.')[1], 'base64url').toString('utf-8')
  )
  const state = {
    user: { id: payload.sub, name: setup.ownerName, email: setup.ownerEmail, role: 'TenantOwner' },
    accessToken: setup.ownerToken,
    refreshToken: '',
    tenantSlug: setup.slug,
  }
  return {
    cookies: [],
    origins: [{
      origin: 'http://localhost:3001',
      localStorage: [
        { name: 'horafy-auth', value: JSON.stringify({ state, version: 0 }) },
      ],
    }],
  }
}

/**
 * Retorna o storageState do Playwright para autenticar o cliente portal.
 * Injeta o Zustand store `horafy-portal-auth` no localStorage.
 */
export function customerStorageState(setup: CustomerSetup): object {
  const state = {
    customer: { id: setup.customerId, name: setup.customerName, email: setup.customerEmail },
    accessToken: setup.customerToken,
  }
  return {
    cookies: [],
    origins: [{
      origin: 'http://localhost:3001',
      localStorage: [
        { name: 'horafy-portal-auth', value: JSON.stringify({ state, version: 0 }) },
      ],
    }],
  }
}
