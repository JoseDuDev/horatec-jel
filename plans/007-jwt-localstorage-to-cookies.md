# Plan 007: Migrar tokens JWT do localStorage para cookies HttpOnly via Next.js API Routes

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- frontend/store/auth.ts frontend/lib/api/auth.ts frontend/lib/api/client.ts frontend/app/\(auth\)/login/page.tsx`
> Se qualquer arquivo mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED
- **Depends on**: plans/004-cookies-httponly.md
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

O Zustand store com `persist` middleware armazena `accessToken` e `refreshToken` em `localStorage` (chave `horafy-auth`). Qualquer XSS — seja via pacote npm comprometido, script de analytics de terceiro, ou vulnerabilidade em componente — pode ler `localStorage` e exfiltrar todos os tokens de todas as sessões abertas. Cookies `HttpOnly` são inacessíveis ao JavaScript; apenas o navegador os envia automaticamente, e apenas o servidor pode lê-los.

A migração usa **Next.js API Routes** como proxy: o frontend chama `/api/auth/login` (Next.js route), que faz forward ao backend .NET e define os cookies `HttpOnly` na resposta. Assim, o token nunca toca o JavaScript do cliente.

## Current state

**Arquivo 1**: `frontend/store/auth.ts`
```typescript
// auth.ts:14 — accessToken e refreshToken persistidos em localStorage
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      tenantSlug: null,
      setAuth: (user, tokens, tenantSlug) =>
        set({ user, accessToken: tokens.accessToken, refreshToken: tokens.refreshToken, tenantSlug }),
      clearAuth: () =>
        set({ user: null, accessToken: null, refreshToken: null, tenantSlug: null }),
    }),
    { name: 'horafy-auth' }
  )
)
```

**Arquivo 2**: `frontend/lib/api/client.ts`
```typescript
// client.ts:11-17 — lê token de document.cookie
function getToken(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('access_token='))
    ?.split('=')[1] ?? ''
}
```

**Arquivo 3**: `frontend/app/(auth)/login/page.tsx`
```typescript
// login/page.tsx:40-43 — chama diretamente o backend .NET
const tokens = await authApi.login(data.email, data.password)
document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
const [user, tenant] = await Promise.all([authApi.me(), tenantsApi.me()])
setAuth(user, tokens, data.tenantSlug)
```

**Arquivo 4**: `frontend/lib/api/auth.ts`
- Papel: cliente de API de autenticação (chama backend .NET diretamente)
- Ler o arquivo para ver as funções exatas antes de implementar

**Padrão de API Routes no projeto**: Verificar se já existem arquivos em `frontend/app/api/` — se sim, seguir o padrão existente. Se não existir, criar o diretório.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Typecheck | `cd frontend && npx tsc --noEmit` | 0 errors |
| Tests | `cd frontend && npm run test:run` | All pass |
| Lint | `cd frontend && npm run lint` | 0 errors |
| Build | `cd frontend && npm run build` | exit 0 |

## Scope

**In scope**:
- `frontend/app/api/auth/login/route.ts` — criar (Next.js API Route de login)
- `frontend/app/api/auth/logout/route.ts` — criar (limpa cookies)
- `frontend/app/api/auth/refresh/route.ts` — criar (proxy para refresh token)
- `frontend/store/auth.ts` — remover `accessToken` e `refreshToken` do estado persistido
- `frontend/lib/api/client.ts` — remover leitura de `document.cookie` para token; usar `credentials: 'include'`
- `frontend/app/(auth)/login/page.tsx` — chamar `/api/auth/login` ao invés do backend diretamente

**Out of scope**:
- `frontend/store/portal-auth.ts` — escopo separado (portal público tem fluxo diferente)
- `frontend/store/platform-admin.ts` — escopo separado
- Login via Google OAuth e Apple OAuth — esses precisam de revisão separada do redirect flow
- Backend .NET — não modificar endpoints existentes

## Git workflow

- Branch: `advisor/007-jwt-localstorage`
- Commit por step para facilitar revisão
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Ler `frontend/lib/api/auth.ts` para entender as chamadas existentes

Antes de implementar, ler o arquivo completo. Identificar:
- Assinatura de `authApi.login(email, password)` — qual URL do backend chama, qual formato de resposta
- Se há `baseUrl` configurável
- Qual a estrutura de `TokenPair`

Se o arquivo não existir no path esperado, procurar em `frontend/lib/api/`. STOP se não encontrar.

### Step 2: Criar Next.js API Route de login

Criar `frontend/app/api/auth/login/route.ts`:

```typescript
import { NextRequest, NextResponse } from 'next/server'

const API_URL = process.env.API_URL ?? 'http://localhost:5000'  // server-side, não NEXT_PUBLIC_

export async function POST(req: NextRequest) {
  const body = await req.json()
  
  const backendRes = await fetch(`${API_URL}/api/v1/auth/email`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  
  if (!backendRes.ok) {
    const error = await backendRes.json().catch(() => ({ title: 'Erro de autenticação' }))
    return NextResponse.json(error, { status: backendRes.status })
  }
  
  const tokens: { accessToken: string; refreshToken: string } = await backendRes.json()
  
  const res = NextResponse.json({ ok: true })
  
  const cookieOpts = {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax' as const,
    path: '/',
    maxAge: 60 * 60 * 24,  // 24h
  }
  
  res.cookies.set('access_token', tokens.accessToken, cookieOpts)
  res.cookies.set('refresh_token', tokens.refreshToken, { ...cookieOpts, maxAge: 60 * 60 * 24 * 30 })
  
  return res
}
```

**Nota**: Use `sameSite: 'lax'` (não `strict`) para que o cookie seja enviado em redirects de OAuth.

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 3: Criar API Route de logout

Criar `frontend/app/api/auth/logout/route.ts`:

```typescript
import { NextResponse } from 'next/server'

export async function POST() {
  const res = NextResponse.json({ ok: true })
  res.cookies.delete('access_token')
  res.cookies.delete('refresh_token')
  res.cookies.delete('tenant_slug')
  return res
}
```

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 4: Criar API Route de refresh token

Criar `frontend/app/api/auth/refresh/route.ts`:

```typescript
import { NextRequest, NextResponse } from 'next/server'

const API_URL = process.env.API_URL ?? 'http://localhost:5000'

export async function POST(req: NextRequest) {
  const refreshToken = req.cookies.get('refresh_token')?.value
  if (!refreshToken) {
    return NextResponse.json({ title: 'Refresh token ausente' }, { status: 401 })
  }
  
  const backendRes = await fetch(`${API_URL}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
  
  if (!backendRes.ok) {
    const res = NextResponse.json({ title: 'Token inválido' }, { status: 401 })
    res.cookies.delete('access_token')
    res.cookies.delete('refresh_token')
    return res
  }
  
  const tokens: { accessToken: string; refreshToken: string } = await backendRes.json()
  const res = NextResponse.json({ ok: true })
  const cookieOpts = {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax' as const,
    path: '/',
    maxAge: 60 * 60 * 24,
  }
  res.cookies.set('access_token', tokens.accessToken, cookieOpts)
  res.cookies.set('refresh_token', tokens.refreshToken, { ...cookieOpts, maxAge: 60 * 60 * 24 * 30 })
  return res
}
```

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 5: Atualizar o formulário de login para chamar a API Route

Em `frontend/app/(auth)/login/page.tsx`, substituir a chamada direta ao backend por chamada à API Route:

**Código atual**:
```typescript
document.cookie = `tenant_slug=${data.tenantSlug}; path=/`
const tokens = await authApi.login(data.email, data.password)
document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
const [user, tenant] = await Promise.all([authApi.me(), tenantsApi.me()])
setAuth(user, tokens, data.tenantSlug)
```

**Código novo**:
```typescript
// Definir tenant_slug via cookie (não precisa ser HttpOnly — usado pelo middleware)
document.cookie = `tenant_slug=${data.tenantSlug}; path=/; SameSite=Lax${window.location.protocol === 'https:' ? '; Secure' : ''}`

// Login via API Route — define access_token e refresh_token como HttpOnly cookies
const loginRes = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: data.email, password: data.password, tenantSlug: data.tenantSlug }),
  credentials: 'include',
})
if (!loginRes.ok) {
  const err = await loginRes.json().catch(() => ({}))
  throw new Error(err.title ?? 'Erro ao fazer login')
}

// Buscar dados do usuário (access_token agora é enviado automaticamente via cookie)
const [user, tenant] = await Promise.all([authApi.me(), tenantsApi.me()])
setAuth(user, { accessToken: '', refreshToken: '' }, data.tenantSlug)  // tokens não mais no store
```

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 6: Remover tokens do Zustand store

Em `frontend/store/auth.ts`, remover `accessToken` e `refreshToken` do estado:

```typescript
interface AuthState {
  user: AdminUser | null
  tenantSlug: string | null
  setAuth: (user: AdminUser, tenantSlug: string) => void
  clearAuth: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      tenantSlug: null,
      setAuth: (user, tenantSlug) => set({ user, tenantSlug }),
      clearAuth: () => set({ user: null, tenantSlug: null }),
    }),
    { name: 'horafy-auth' }
  )
)
```

Atualizar todas as chamadas a `setAuth` para remover o parâmetro `tokens`.

**Verify**: `npx tsc --noEmit` → 0 errors (pode haver erros de tipo em callers — corrija todos antes de continuar)

### Step 7: Adaptar client.ts para não precisar de token manual

Em `frontend/lib/api/client.ts`, o `apiFetch` já envia `credentials: 'include'`. Com cookies `HttpOnly`, o navegador enviará `access_token` automaticamente. Remover o header manual `Authorization`:

```typescript
export async function apiFetch<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const slug = getSlug()  // tenant_slug ainda é lido de cookie (não é HttpOnly)

  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(slug ? { 'X-Tenant-Slug': slug } : {}),
      ...options.headers,
    },
  })
  // ... resto igual
}
```

**Nota**: O backend .NET deve aceitar o JWT tanto de `Authorization: Bearer` quanto de cookies. Verificar se o backend tem configuração de leitura de cookie — se não tiver, manter o header `Authorization` sendo definido a partir do cookie não-HttpOnly `access_token` (Plan 004 garante `SameSite` mas não `HttpOnly` para esse cookie — compatível).

Se o backend não suportar cookies como fonte de JWT, manter `Authorization` header e ler de um cookie separado não-HttpOnly gerado pela API Route.

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 8: Verificação completa

**Verify**: `cd frontend && npm run test:run` → todos os testes passam
**Verify**: `cd frontend && npm run lint` → 0 errors
**Verify**: `cd frontend && npm run build` → exit 0

## Test plan

- Atualizar `frontend/__tests__/login.test.tsx` para mockar `/api/auth/login` ao invés de `authApi.login`
- Verificar que `useAuthStore` não contém mais `accessToken` nem `refreshToken`
- Verificar manualmente: após login, DevTools → Application → Cookies → `access_token` tem flag `HttpOnly` = true
- Verificar que `localStorage['horafy-auth']` não contém `accessToken`

## Done criteria

- [ ] `cd frontend && npx tsc --noEmit` exits 0
- [ ] `cd frontend && npm run test:run` exits 0
- [ ] `cd frontend && npm run build` exits 0
- [ ] `grep -rn "accessToken" frontend/store/auth.ts` retorna 0 matches
- [ ] `grep -rn "persist" frontend/store/auth.ts` — se ainda presente, verificar que não persiste tokens
- [ ] `frontend/app/api/auth/login/route.ts` existe e define cookies com `httpOnly: true`
- [ ] `plans/README.md` atualizado

## STOP conditions

- Backend .NET não aceita JWT via cookie (apenas via `Authorization` header) — não migrar client.ts no Step 7; manter token em cookie não-HttpOnly separado e documentar a limitação
- `setAuth` é chamada com tipos incompatíveis em mais de 3 places — listar todos os callers antes de modificar a interface e reporte se > 5 arquivos precisam mudar
- Testes de login quebram com `fetch is not defined` no ambiente de teste — configurar `msw` ou mock de `fetch` no Vitest setup

## Maintenance notes

- O `API_URL` server-side (sem `NEXT_PUBLIC_`) deve ser configurado como env var no Docker/Vercel; o padrão `localhost:5000` funciona apenas em dev local onde Next.js e .NET rodam juntos
- O refresh token expirar requer que o cliente chame `/api/auth/refresh` — sem implementar um interceptor de 401, o usuário precisará fazer re-login. Implementar um interceptor em `apiFetch` é a próxima melhoria natural
- Se Google OAuth for ajustado, o redirect do Google volta para Next.js (não para o backend) — o `/api/auth/google/callback` precisará seguir o mesmo padrão de definir cookies HttpOnly
