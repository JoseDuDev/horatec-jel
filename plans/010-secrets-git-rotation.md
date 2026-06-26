# Plan 010: Remover secrets do git e rotacionar credenciais expostas

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.API/appsettings.Development.json .gitignore`
> Se qualquer arquivo mudou, compare os excerpts antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

`src/Horafy.API/appsettings.Development.json` está rastreado pelo git e contém:
- `Jwt:Secret` — se reutilizado em produção ou staging, permite forjar tokens JWT para qualquer usuário
- `MercadoPago:AccessToken` — valor `TEST-your-test-token` (token de teste, mas estabelece padrão inseguro)
- `EvolutionApi:ApiKey` — `dev-key`
- `RabbitMq:Username/Password` — `guest/guest`

Secretos no histórico git são considerados comprometidos permanentemente — mesmo deletar o arquivo não remove do histórico. A solução correta é: (1) mover para mecanismo fora do git, (2) rotacionar as credenciais afetadas se foram usadas em produção, e (3) adicionar ao `.gitignore` para prevenir reintrodução.

## Current state

**Arquivo rastreado no git**: `src/Horafy.API/appsettings.Development.json`
- Papel: configuração de desenvolvimento local — NUNCA deveria estar no git

```json
{
  "Jwt": {
    "Secret": "[valor em arquivo:linha 12 — não reproduzir]"
  },
  "MercadoPago": {
    "AccessToken": "[valor em arquivo:linha 15]",
    "WebhookSecret": ""
  },
  "RabbitMq": {
    "Username": "guest",
    "Password": "guest"
  },
  "EvolutionApi": {
    "ApiKey": "[valor em arquivo:linha 27]"
  }
}
```

**Padrão correto do .NET**: User Secrets (`dotnet user-secrets`) armazena secrets em `%APPDATA%\Microsoft\UserSecrets\<guid>\secrets.json` (Windows) ou `~/.microsoft/usersecrets/<guid>/secrets.json` (Linux/Mac) — nunca versionados.

**`.gitignore` atual**: verificar se já há entrada para `appsettings.Development.json`. Se não houver, adicionar.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Inicializar User Secrets | `dotnet user-secrets init --project src/Horafy.API` | exit 0, `UserSecretsId` adicionado ao .csproj |
| Definir um secret | `dotnet user-secrets set "Jwt:Secret" "valor" --project src/Horafy.API` | exit 0 |
| Listar secrets | `dotnet user-secrets list --project src/Horafy.API` | mostra os secrets definidos |
| Remover do git (untrack) | `git rm --cached src/Horafy.API/appsettings.Development.json` | exit 0 |
| Build | `dotnet build src/Horafy.API` | exit 0 |

## Scope

**In scope**:
- `src/Horafy.API/appsettings.Development.json` — remover do git tracking
- `.gitignore` — adicionar entrada para `appsettings.Development.json`
- `src/Horafy.API/Horafy.API.csproj` — inicializar UserSecretsId

**Out of scope**:
- Rotação do JWT secret em produção — isso requer acesso ao ambiente de produção; o executor deve documentar quais credenciais precisam de rotação e o responsável do projeto deve executar a rotação
- `docker-compose.yml` fallback passwords — aceitável em dev; já documentado em Plan 003 (não é o mesmo arquivo)
- Reescrita do histórico git (`git filter-branch` ou `BFG`) — **não fazer** sem autorização explícita do responsável do projeto (reescrita de histórico é destrutiva)

## Git workflow

- Branch: `advisor/010-secrets-rotation`
- Commits separados para cada step para auditoria clara
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Verificar se UserSecretsId já existe no projeto

Ler `src/Horafy.API/Horafy.API.csproj` e verificar se já há `<UserSecretsId>` definido. Se já existir, pular para Step 2.

Se não existir:
```bash
dotnet user-secrets init --project src/Horafy.API
```

**Verify**: `grep -n "UserSecretsId" src/Horafy.API/Horafy.API.csproj` → retorna um GUID

### Step 2: Criar um arquivo de exemplo para desenvolvimento

Criar `src/Horafy.API/appsettings.Development.json.example` (com `.example` no nome — não é rastreado como secret):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.EntityFrameworkCore.Database.Command": "Information",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    }
  },
  "Jwt": {
    "Secret": "PREENCHER: string de pelo menos 64 caracteres, ex: use 'openssl rand -base64 48'"
  },
  "MercadoPago": {
    "AccessToken": "PREENCHER: token TEST-... do painel MP (apenas para testes)",
    "WebhookSecret": "",
    "NotificationUrl": "https://localhost:7000/api/v1/webhooks/mercadopago"
  },
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  },
  "EvolutionApi": {
    "BaseUrl": "http://localhost:8080",
    "ApiKey": "PREENCHER: chave da Evolution API local",
    "InstanceName": "horafy-dev"
  },
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "Username": "",
    "Password": "",
    "FromAddress": "no-reply@horafy.com.br",
    "FromName": "Horafy",
    "UseSsl": false
  }
}
```

**Nota**: RabbitMq com `guest/guest` é aceitável aqui — é a senha padrão do container Docker de desenvolvimento, não um secret de produção.

### Step 3: Adicionar ao .gitignore

Ler o `.gitignore` raiz atual. Adicionar as seguintes entradas se não existirem:

```
# Secrets de desenvolvimento — nunca versionar
src/Horafy.API/appsettings.Development.json
src/**/appsettings.*.json
!src/**/appsettings.json
!src/**/appsettings.*.json.example
```

A regra `!src/**/appsettings.json` garante que `appsettings.json` (sem secrets) continue rastreado.

**Verify**: `git check-ignore -v src/Horafy.API/appsettings.Development.json` → mostra que o arquivo é ignorado

### Step 4: Remover arquivo do tracking do git

```bash
git rm --cached src/Horafy.API/appsettings.Development.json
```

Esse comando remove o arquivo do índice git mas **mantém o arquivo no sistema de arquivos** — o desenvolvedor ainda tem o arquivo localmente.

**Verify**: `git status` → `src/Horafy.API/appsettings.Development.json` aparece como "untracked" (não mais rastreado)

### Step 5: Definir os secrets via User Secrets (para desenvolvimento local)

Transferir os valores do `appsettings.Development.json` local para User Secrets:

```bash
# JWT Secret — usar um valor diferente e mais forte que o atual
dotnet user-secrets set "Jwt:Secret" "NOVO_SECRET_64_CHARS_AQUI" --project src/Horafy.API
dotnet user-secrets set "MercadoPago:AccessToken" "TEST-VALOR" --project src/Horafy.API
dotnet user-secrets set "EvolutionApi:ApiKey" "dev-key" --project src/Horafy.API
```

**IMPORTANTE**: o `Jwt:Secret` deve ser **diferente** do valor que estava no `appsettings.Development.json` — se o mesmo secret foi usado em produção/staging, ele deve ser rotacionado lá também (ver nota de manutenção).

**Verify**: `dotnet user-secrets list --project src/Horafy.API` → mostra os secrets definidos

### Step 6: Verificar que a aplicação ainda inicia

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 7: Commitar as mudanças

Commitar os seguintes arquivos (e apenas esses):
- `.gitignore` (atualizado)
- `src/Horafy.API/appsettings.Development.json.example` (novo)
- `src/Horafy.API/Horafy.API.csproj` (atualizado com UserSecretsId)
- Remoção de `src/Horafy.API/appsettings.Development.json` do tracking

**NÃO commitar** `src/Horafy.API/appsettings.Development.json` — confirmar com `git status` antes de commitar.

**Verify**: `git status` → `appsettings.Development.json` NÃO aparece nos arquivos staged

## Test plan

- `dotnet build src/` → exit 0 (app ainda compila sem o arquivo)
- `dotnet run --project src/Horafy.API` → inicia sem erro de configuração (User Secrets são carregados automaticamente em Development)
- `git status` → `appsettings.Development.json` não aparece como tracked nem staged

## Done criteria

- [ ] `git ls-files src/Horafy.API/appsettings.Development.json` retorna vazio (não rastreado)
- [ ] `grep -n "appsettings.Development.json" .gitignore` retorna match
- [ ] `dotnet build src/` exits 0
- [ ] `src/Horafy.API/appsettings.Development.json.example` existe no git
- [ ] `dotnet user-secrets list --project src/Horafy.API` lista pelo menos `Jwt:Secret`
- [ ] `plans/README.md` atualizado

## STOP conditions

- O `appsettings.Development.json` contém secrets que são os mesmos usados em produção — parar e notificar o responsável imediatamente para rotação antes de prosseguir
- `dotnet user-secrets init` falha porque o projeto usa `.NET` diferente de `Microsoft.NET.Sdk.Web` — investigar formato alternativo de secret storage
- `git rm --cached` falha — verificar se o arquivo está com lock por outro processo

## Maintenance notes

- **Rotação obrigatória**: Se o `Jwt:Secret` do `appsettings.Development.json` (linha 12) foi **copiado** para qualquer ambiente de produção ou staging, ele deve ser rotacionado imediatamente após este plano: gerar novo secret de 64+ chars, atualizar a env var de produção, e fazer rolling restart da API. Tokens existentes assinados com o secret antigo serão invalidados — usuários precisarão fazer login novamente.
- O histórico git ainda contém o arquivo com os secrets — se o repositório for público ou compartilhado com terceiros, considerar `BFG Repo Cleaner` ou `git filter-repo` para reescrever o histórico (operação destrutiva que requer coordenação com todo o time)
- Para CI/CD, configurar os secrets como variáveis de ambiente do GitHub Actions (Settings → Secrets) e injetá-los via `--build-arg` ou env vars no container
- Adicionar ao CLAUDE.md/README um passo de onboarding: "Copie `.example` e preencha via `dotnet user-secrets`"
