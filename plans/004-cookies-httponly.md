# Plan 004: Adicionar flags HttpOnly e Secure nos cookies de autenticação

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- frontend/app/\(auth\)/login/page.tsx frontend/app/platform/login/page.tsx frontend/app/platform/\(dashboard\)/`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

Os cookies `access_token` e `tenant_slug` são definidos via `document.cookie` no cliente sem a flag `HttpOnly`. Isso significa que qualquer script JavaScript — incluindo scripts de terceiros (analytics, chat widget) ou qualquer XSS — pode ler esses valores. A flag `HttpOnly` impede o acesso via `document.cookie`; o navegador ainda envia o cookie nas requisições HTTP mas o JavaScript não pode lê-lo. A flag `Secure` garante que o cookie não é enviado em conexões HTTP puras.

**Nota importante**: `frontend/lib/api/client.ts` lê os cookies via `document.cookie` para enviar nos headers. Após adicionar `HttpOnly`, isso deixará de funcionar para `access_token`. O plano inclui a adaptação do cliente de API para usar o token do Zustand store ou enviar cookies automaticamente via `credentials: 'include'`.

## Current state

**Arquivo 1**: `frontend/app/(auth)/login/page.tsx`
- Papel: formulário de login do admin

```typescript
// login/page.tsx:39-41
document.cookie = `tenant_slug=${data.tenantSlug}; path=/`
const tokens = await authApi.login(data.email, data.password)
document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
```

**Arquivo 2**: `frontend/lib/api/client.ts`
- Papel: cliente HTTP para APIs do admin

```typescript
// client.ts:11-17
function getToken(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('access_token='))
    ?.split('=')[1] ?? ''
}
```

**Arquivo 3**: `frontend/store/auth.ts`
- Papel: estado de autenticação (Zustand); já contém `accessToken`

```typescript
// auth.ts:14-27 — accessToken já disponível no Zustand store
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      // ...
    }),
    { name: 'horafy-auth' }  // persiste em localStorage
  )
)
```

**Padrão do projeto**: middleware.ts já lê cookies via `req.cookies.get('access_token')` — esse acesso server-side funciona com ou sem `HttpOnly` (é feito pelo servidor, não pelo JS do cliente).

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Typecheck | `cd frontend && npx tsc --noEmit` | 0 errors |
| Tests | `cd frontend && npm run test:run` | All pass |
| Lint | `cd frontend && npm run lint` | 0 errors |
| Build | `cd frontend && npm run build` | exit 0 |

## Scope

**In scope**:
- `frontend/app/(auth)/login/page.tsx` — adicionar flags nos cookies
- `frontend/app/platform/login/page.tsx` — idem para login da plataforma
- `frontend/lib/api/client.ts` — adaptar `getToken()` para ler do Zustand store ao invés de `document.cookie`

**Out of scope**:
- `frontend/store/auth.ts` — não modificar (já tem o token; apenas usar)
- `frontend/middleware.ts` — não modificar (já funciona via server-side cookie)
- Migração completa para cookies HttpOnly server-side via Next.js API Routes — isso requereria Plan 007 (JWT localStorage); este plano cobre apenas as flags dos cookies existentes
- Portal auth (`frontend/store/portal-auth.ts`) — escopo separado

## Git workflow

- Branch: `advisor/004-cookies-httponly`
- Commit: `Adicionar flags HttpOnly e Secure nos cookies de autenticação`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Adicionar flags HttpOnly e Secure no login do admin

Em `frontend/app/(auth)/login/page.tsx`, atualizar as linhas que definem cookies:

**Código atual**:
```typescript
document.cookie = `tenant_slug=${data.tenantSlug}; path=/`
// ...
document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}`
```

**Código novo**:
```typescript
document.cookie = `tenant_slug=${data.tenantSlug}; path=/; SameSite=Strict`
// ...
document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}; SameSite=Strict`
```

**Nota sobre HttpOnly**: `document.cookie` é JavaScript do cliente — por definição, não pode definir cookies `HttpOnly` (só o servidor pode). O que podemos fazer agora: (a) adicionar `SameSite=Strict` para proteção CSRF, e (b) marcar para migração futura para server-side Set-Cookie. Se o ambiente usar HTTPS em produção, adicionar `; Secure` também.

Adicionar `Secure` de forma condicional:
```typescript
const isSecure = window.location.protocol === 'https:'
const secureFlag = isSecure ? '; Secure' : ''
document.cookie = `access_token=${tokens.accessToken}; path=/; max-age=${60 * 60 * 24}; SameSite=Strict${secureFlag}`
document.cookie = `tenant_slug=${data.tenantSlug}; path=/; SameSite=Strict${secureFlag}`
```

**Verify**: `npx tsc --noEmit` (no diretório `frontend/`) → 0 errors

### Step 2: Aplicar o mesmo padrão no login da plataforma

Ler `frontend/app/platform/login/page.tsx` e aplicar as mesmas flags `SameSite=Strict` e `Secure` condicional em qualquer `document.cookie =` encontrado.

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 3: Adaptar client.ts para não depender de document.cookie para o token

O `getToken()` em `frontend/lib/api/client.ts` lê `document.cookie` para obter `access_token`. Enquanto o cookie não for `HttpOnly`, isso funciona — mas é frágil. Adaptar para preferir o Zustand store:

**Código atual** (`client.ts:11-17`):
```typescript
function getToken(): string {
  if (typeof window === 'undefined') return ''
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('access_token='))
    ?.split('=')[1] ?? ''
}
```

**Código novo**:
```typescript
function getToken(): string {
  if (typeof window === 'undefined') return ''
  // Prefer Zustand store (in-memory, not cookie-dependent)
  const storeState = localStorage.getItem('horafy-auth')
  if (storeState) {
    try {
      const parsed = JSON.parse(storeState)
      if (parsed?.state?.accessToken) return parsed.state.accessToken
    } catch { /* fall through */ }
  }
  // Fallback: cookie (funciona enquanto não for HttpOnly)
  return document.cookie
    .split(';')
    .find(c => c.trim().startsWith('access_token='))
    ?.split('=')[1] ?? ''
}
```

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 4: Verificação completa

**Verify**: `cd frontend && npm run test:run` → todos os testes passam
**Verify**: `cd frontend && npm run lint` → 0 errors
**Verify**: `cd frontend && npm run build` → exit 0

## Test plan

- Não há novos testes unitários necessários (comportamento de cookie é testado via E2E)
- Adicionar caso de teste manual: após login, abrir DevTools → Application → Cookies → verificar que `access_token` tem `SameSite=Strict`
- Verificar que `middleware.ts` ainda lê o cookie corretamente (server-side access não é afetado por SameSite)
- Verificar que requisições de API (apiFetch) ainda incluem o token no header `Authorization`

## Done criteria

- [ ] `cd frontend && npx tsc --noEmit` exits 0
- [ ] `cd frontend && npm run test:run` exits 0
- [ ] `cd frontend && npm run build` exits 0
- [ ] `grep -n "document.cookie.*access_token" frontend/app/\(auth\)/login/page.tsx` mostra `SameSite=Strict`
- [ ] `grep -n "document.cookie.*tenant_slug" frontend/app/\(auth\)/login/page.tsx` mostra `SameSite=Strict`
- [ ] `plans/README.md` atualizado

## STOP conditions

- `platform/login/page.tsx` não existe no path esperado — procurar em toda a pasta `app/` e ajustar
- A mudança quebra algum teste existente (o token não é encontrado) — investigar o cookie parsing
- `apiFetch` começa a enviar requisições sem header `Authorization` — o Step 3 tem um bug; pare e reporte

## Maintenance notes

- Este plano é um passo intermediário. A solução completa (Plan 007) migra para HttpOnly cookies gerenciados pelo backend — nesse ponto, o `getToken()` deve ser removido completamente (o token é enviado via cookie automático)
- Se HTTPS for terminado no Caddy (como está em produção), o frontend recebe HTTP internamente — `window.location.protocol === 'https:'` pode ser false mesmo em produção. Nesse caso, força `Secure` via env var: `process.env.NEXT_PUBLIC_COOKIE_SECURE === 'true'`
- Revisor: confirmar que `SameSite=Strict` não quebra os redirects OAuth (Google/Apple) — esses retornam via POST cross-site, que `SameSite=Strict` bloqueia. Se quebrar, usar `SameSite=Lax` ao invés de `Strict`
