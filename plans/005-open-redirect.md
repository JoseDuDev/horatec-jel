# Plan 005: Validar parâmetro `redirect` para evitar open redirect pós-login

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- frontend/middleware.ts frontend/app/\(auth\)/login/page.tsx`
> Se qualquer arquivo in-scope mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

`middleware.ts:12` passa o `pathname` atual como parâmetro `redirect` na URL de login sem validação. `login/page.tsx:52` usa esse valor diretamente em `router.replace(redirect)`. Um atacante pode construir um link como `https://horafy.com.br/login?redirect=https://evil.com/phishing` — o usuário vê um domínio legítimo, faz login, e é redirecionado para o site malicioso. Esse vetor é usado em ataques de phishing de credenciais e roubo de tokens pós-autenticação.

## Current state

**Arquivo 1**: `frontend/middleware.ts`
- Papel: guard de rotas no Edge Runtime do Next.js

```typescript
// middleware.ts:10-14
if (pathname.startsWith('/admin')) {
  const token = req.cookies.get('access_token')?.value
  if (!token) {
    const loginUrl = new URL('/login', req.url)
    loginUrl.searchParams.set('redirect', pathname)  // ← pathname é relativo (seguro aqui)
    return NextResponse.redirect(loginUrl)
  }
}
```

**Arquivo 2**: `frontend/app/(auth)/login/page.tsx`
- Papel: formulário de login

```typescript
// login/page.tsx:52-53
const redirect = searchParams.get('redirect') ?? '/admin/dashboard'
router.replace(redirect)  // ← usa valor do query param sem validar
```

O middleware passa apenas `pathname` (ex.: `/admin/dashboard`) que é relativo — aparentemente seguro. O problema é que `searchParams.get('redirect')` pode ler qualquer valor da URL, incluindo valores injetados manualmente por um atacante (ex.: `https://evil.com`).

**Padrão de validação existente no projeto**: O projeto usa Zod para validação de formulários em componentes. Para validação de URL, o mesmo padrão pode ser aplicado.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Typecheck | `cd frontend && npx tsc --noEmit` | 0 errors |
| Tests | `cd frontend && npm run test:run` | All pass |
| Lint | `cd frontend && npm run lint` | 0 errors |

## Scope

**In scope**:
- `frontend/app/(auth)/login/page.tsx` — adicionar validação do parâmetro `redirect`
- `frontend/app/platform/login/page.tsx` — aplicar o mesmo padrão se usar redirect

**Out of scope**:
- `frontend/middleware.ts` — não modificar (o middleware já passa apenas pathnames relativos; o fix é no consumidor)
- Outros usos de `router.push/replace` no projeto — escopo separado

## Git workflow

- Branch: `advisor/005-open-redirect`
- Commit: `Validar parâmetro redirect para evitar open redirect pós-login`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Criar função de validação de redirect seguro

Em `frontend/app/(auth)/login/page.tsx`, adicionar a função `safeRedirect` logo acima do componente `LoginForm`:

```typescript
function safeRedirect(redirect: string | null): string {
  const DEFAULT = '/admin/dashboard'
  if (!redirect) return DEFAULT
  try {
    // Aceitar apenas caminhos relativos começando com /admin ou /platform
    const url = new URL(redirect, 'http://localhost')
    const allowed = ['/admin', '/platform']
    if (url.hostname === 'localhost' && allowed.some(p => url.pathname.startsWith(p))) {
      return url.pathname + url.search + url.hash
    }
  } catch {
    // URL inválida — usar default
  }
  return DEFAULT
}
```

A função usa `new URL(redirect, 'http://localhost')` para normalizar o valor. Se o `redirect` for `https://evil.com`, o `hostname` será `evil.com` (diferente de `localhost`) e é rejeitado. Se for `/admin/dashboard`, o `hostname` será `localhost` e o caminho começa com `/admin` — aceito.

**Verify**: `npx tsc --noEmit` → 0 errors (a função tem tipos corretos)

### Step 2: Usar `safeRedirect` no fluxo de login

Em `frontend/app/(auth)/login/page.tsx`, na função `onSubmit`, substituir:

**Código atual**:
```typescript
const redirect = searchParams.get('redirect') ?? '/admin/dashboard'
router.replace(redirect)
```

**Código novo**:
```typescript
const redirect = safeRedirect(searchParams.get('redirect'))
router.replace(redirect)
```

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 3: Aplicar o mesmo padrão no login da plataforma

Ler `frontend/app/platform/login/page.tsx`. Se houver uso de parâmetro `redirect` de `searchParams`, aplicar a mesma função `safeRedirect` (com a lista `allowed` incluindo `/platform`):

```typescript
function safeRedirect(redirect: string | null): string {
  const DEFAULT = '/platform/dashboard'
  if (!redirect) return DEFAULT
  try {
    const url = new URL(redirect, 'http://localhost')
    if (url.hostname === 'localhost' && url.pathname.startsWith('/platform')) {
      return url.pathname + url.search + url.hash
    }
  } catch { /* fall through */ }
  return DEFAULT
}
```

Se o arquivo não usar `redirect` de searchParams, não modificar.

**Verify**: `npx tsc --noEmit` → 0 errors

### Step 4: Verificação final

**Verify**: `cd frontend && npm run test:run` → todos os testes passam
**Verify**: `cd frontend && npm run lint` → 0 errors

## Test plan

Adicionar testes em `frontend/__tests__/login.test.tsx` (ou criar `__tests__/safe-redirect.test.ts`):

```typescript
// Casos para safeRedirect:
// 1. null → '/admin/dashboard'
// 2. '' → '/admin/dashboard'
// 3. '/admin/clientes' → '/admin/clientes'
// 4. 'https://evil.com' → '/admin/dashboard'
// 5. '//evil.com' → '/admin/dashboard'
// 6. 'javascript:alert(1)' → '/admin/dashboard'
// 7. '/platform/dashboard' → '/admin/dashboard' (não permitido no admin login)
```

Pattern: seguir testes existentes em `frontend/__tests__/` (Vitest + @testing-library/react).

**Verify**: `cd frontend && npm run test:run -- --filter safe-redirect` → todos os novos testes passam

## Done criteria

- [ ] `cd frontend && npx tsc --noEmit` exits 0
- [ ] `cd frontend && npm run test:run` exits 0; testes de `safeRedirect` existem e passam
- [ ] `grep -n "searchParams.get('redirect')" frontend/app/\(auth\)/login/page.tsx` mostra uso de `safeRedirect`
- [ ] `grep -n "router.replace(redirect)" frontend/app/\(auth\)/login/page.tsx` retorna 0 matches (substituído por safeRedirect)
- [ ] `plans/README.md` atualizado

## STOP conditions

- Função `safeRedirect` causa TypeScript error que não resolve rapidamente — reporte
- Testes de login existentes quebram (o redirect foi hard-wired para `/admin/dashboard`) — investigue qual path o teste espera e ajuste sem mudar a lógica de segurança
- `platform/login/page.tsx` usa uma biblioteca de routing diferente (não `useRouter`) — adapte mas documente a divergência

## Maintenance notes

- Se novas áreas protegidas forem adicionadas (ex.: `/billing`), atualizar a lista `allowed` em `safeRedirect`
- A função `safeRedirect` pode ser extraída para `frontend/lib/utils/safe-redirect.ts` se usada em múltiplos lugares — não fazer agora (YAGNI), extrair quando o segundo uso aparecer
- Revisor: verificar que `safeRedirect` não quebra o fluxo de OAuth (Google/Apple login): esses podem redirecionar para `/admin/dashboard` diretamente sem parâmetro `redirect`, o que é seguro
