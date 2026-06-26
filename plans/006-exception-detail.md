# Plan 006: Sanitizar exception.Message no middleware de tratamento de erros

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat 8f8cad2..HEAD -- src/Horafy.API/Middleware/ExceptionHandlingMiddleware.cs`
> Se o arquivo mudou, compare o excerpt antes de prosseguir.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `8f8cad2`, 2026-06-26

## Why this matters

`ExceptionHandlingMiddleware.cs:50` define `Detail = exception.Message` diretamente no `ProblemDetails` retornado ao cliente. Mensagens de exceção em .NET frequentemente incluem detalhes de infraestrutura: nomes de tabelas/colunas de banco de dados (`column "user_id" of relation "bookings" violates not-null constraint`), paths de arquivo (`System.IO.FileNotFoundException: Could not find file '/app/templates/booking.html'`), connection strings parciais, e nomes de classes internas. Essa informação facilita a enumeração de vulnerabilidades e ajuda atacantes a mapear a arquitetura do sistema.

## Current state

**Arquivo**: `src/Horafy.API/Middleware/ExceptionHandlingMiddleware.cs`
- Papel: captura exceções não tratadas, formata resposta ProblemDetails (RFC 7807)

```csharp
// ExceptionHandlingMiddleware.cs:44-53
var problem = new ProblemDetails
{
    Status = (int)statusCode,
    Title = title,
    Detail = exception.Message,  // ← PROBLEMA: message vaza para o cliente
    Instance = context.Request.Path,
    Extensions = { ["traceId"] = context.TraceIdentifier }
};
```

```csharp
// ExceptionHandlingMiddleware.cs:23-27 — logging já está correto
logger.LogError(ex,
    "Exceção não tratada: {Message} | Path: {Path} | TraceId: {TraceId}",
    ex.Message,
    context.Request.Path,
    context.TraceIdentifier);
```

O log já captura `ex.Message` com o TraceId — o cliente tem o TraceId para reportar ao suporte, então não perde rastreabilidade.

**Convenção de mensagens de erro do projeto**: O projeto usa `Error.Description` nos casos onde quer comunicar algo específico ao cliente (padrão `Result<T>`). O middleware é apenas para exceções não tratadas — deve retornar mensagens genéricas.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build src/Horafy.API` | exit 0 |
| Tests | `dotnet test tests/` | All pass |

## Scope

**In scope**:
- `src/Horafy.API/Middleware/ExceptionHandlingMiddleware.cs` — apenas a linha `Detail = exception.Message`

**Out of scope**:
- Logging — não alterar (o log já é adequado)
- Exceções de domínio (BookingErrors, PaymentErrors) — essas chegam via `Result<T>` e não passam por este middleware
- Respostas de validação (FluentValidation) — passam por pipeline do MediatR, não por este middleware

## Git workflow

- Branch: `advisor/006-exception-detail`
- Commit: `Sanitizar exception.Message no middleware de erros`
- Não fazer push nem abrir PR a menos que instruído

## Steps

### Step 1: Substituir `Detail = exception.Message` por mensagem genérica

Em `src/Horafy.API/Middleware/ExceptionHandlingMiddleware.cs`, localizar a construção de `ProblemDetails` (linha ~46) e modificar:

**Código atual**:
```csharp
var problem = new ProblemDetails
{
    Status = (int)statusCode,
    Title = title,
    Detail = exception.Message,
    Instance = context.Request.Path,
    Extensions = { ["traceId"] = context.TraceIdentifier }
};
```

**Código novo**:
```csharp
var problem = new ProblemDetails
{
    Status = (int)statusCode,
    Title = title,
    Detail = GetSafeDetail(statusCode, exception),
    Instance = context.Request.Path,
    Extensions = { ["traceId"] = context.TraceIdentifier }
};
```

### Step 2: Adicionar método `GetSafeDetail`

Adicionar o método privado estático `GetSafeDetail` dentro da classe `ExceptionHandlingMiddleware`:

```csharp
private static string GetSafeDetail(HttpStatusCode statusCode, Exception exception) =>
    statusCode switch
    {
        HttpStatusCode.BadRequest => exception is ArgumentException or ArgumentNullException
            ? exception.Message  // mensagens de ArgumentException são geradas pelo código interno, geralmente seguras
            : "Requisição inválida.",
        HttpStatusCode.NotFound => "Recurso não encontrado.",
        HttpStatusCode.Unauthorized => "Não autorizado.",
        HttpStatusCode.UnprocessableEntity => "Operação não pôde ser concluída.",
        HttpStatusCode.GatewayTimeout => "Serviço externo não respondeu a tempo. Tente novamente.",
        _ => "Ocorreu um erro interno. Use o traceId para suporte."
    };
```

**Nota sobre `ArgumentException`**: O código de domínio do projeto gera `ArgumentException` com mensagens que são seguras para exibir ao cliente (ex.: validações de negócio). Se houver dúvida, mudar para `"Requisição inválida."` também para `ArgumentException` — é mais conservador.

**Verify**: `dotnet build src/Horafy.API` → exit 0

### Step 3: Verificação final

**Verify**: `dotnet test tests/` → todos os testes passam

## Test plan

Verificar que os testes existentes de ExceptionHandlingMiddleware (se existirem) ainda passam. Se não existirem, não é necessário criar agora.

Teste manual:
1. Lançar exceção `InvalidOperationException` em algum endpoint de teste
2. Chamar o endpoint → resposta deve ter `Detail: "Operação não pôde ser concluída."` e **não** a mensagem da exceção
3. Verificar nos logs Seq que `ex.Message` ainda aparece com o mesmo TraceId

## Done criteria

- [ ] `dotnet build src/` exits 0
- [ ] `dotnet test tests/` exits 0
- [ ] `grep -n "exception.Message" src/Horafy.API/Middleware/ExceptionHandlingMiddleware.cs` retorna 0 matches na construção de ProblemDetails (pode aparecer no logging, que é correto)
- [ ] `plans/README.md` atualizado

## STOP conditions

- Testes existentes que verificavam `Detail = exception.Message` quebram — ajustar os testes para o novo comportamento (o teste estava testando comportamento inseguro)
- `ArgumentException` no Step 2 causa TypeScript error — adaptar o switch

## Maintenance notes

- Se no futuro quiser retornar detalhes ricos de erro para ambientes de desenvolvimento, adicionar condição: `if (env.IsDevelopment()) Detail = exception.ToString()` — mas nunca em produção
- Manter `TraceId` na resposta: é a forma correta de rastreamento sem vazar detalhes internos
- Revisor: conferir que nenhum componente do frontend parseia o campo `Detail` das respostas de erro — se o frontend depende de mensagens específicas de exceção para lógica de negócio, isso é um anti-padrão que precisa ser corrigido separadamente
