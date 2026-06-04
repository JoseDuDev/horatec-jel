# Horafy — Spec Definitivo (v1.0)

> **Gerado em:** 2026-06-04  
> **Status:** Aprovado — todas as decisões de arquitetura tomadas  
> **Baseado em:** `prompt-saas-agendamento-multitenant.md` (rascunho original) + sessão de brainstorming

---

## 1. Visão Geral

Plataforma SaaS B2B de agendamento multi-tenant voltada para o mercado brasileiro. Atende múltiplos verticais de negócio (barbearias, salões de festa, quadras esportivas, aluguel de brinquedos/ferramentas, consultórios médicos e estéticos). Cada tenant possui espaço isolado com domínio/subdomínio configurável, identidade visual própria e configurações independentes.

**Nome do produto:** Horafy  
**Mercado:** Brasil (pt-BR, BRL)  
**Modelo de negócio:** Mensalidade por plano (Free / Starter / Professional / Enterprise)

---

## 2. Stack Tecnológica Definitiva

### Backend
| Componente | Tecnologia |
|---|---|
| Runtime | .NET 8 |
| Linguagem | C# |
| Arquitetura | Clean Architecture + CQRS (MediatR) |
| ORM | Entity Framework Core 8 |
| Banco | PostgreSQL 16 — schema por tenant |
| Cache | Redis |
| Mensageria | RabbitMQ + MassTransit |
| Auth | JWT + Google OAuth2 + Apple Sign-In |
| Logs | Serilog → Seq |
| Testes | xUnit + Moq + FluentAssertions + Testcontainers |
| Documentação API | Scalar |

### Frontend
| Componente | Tecnologia |
|---|---|
| Framework | Next.js 14 (App Router) |
| Estilização | Tailwind CSS + shadcn/ui |
| Estado global | Zustand |
| Formulários | React Hook Form + Zod |
| PWA | next-pwa (Sprint 11) |
| Idioma | pt-BR apenas (sem i18n library) |
| Testes | Vitest + Testing Library |

### Integrações Externas
| Componente | Tecnologia | Observação |
|---|---|---|
| Pagamento | Mercado Pago | PIX + cartão + boleto; interface `IPaymentGateway` abstrai troca futura |
| WhatsApp MVP | Evolution API (self-hosted) | Docker no próprio VPS |
| WhatsApp produção | Twilio WhatsApp Business API | Migração antes do go-live com clientes reais |
| OAuth clientes | Google OAuth2 + Apple Sign-In | Clientes finais: apenas OAuth, sem senha |
| OAuth owners | Google OAuth2 + e-mail/senha | Owners/admins do tenant: ambos permitidos |

### Infraestrutura
| Ambiente | Stack |
|---|---|
| Desenvolvimento | Docker Compose (PostgreSQL, Redis, RabbitMQ, Seq, Evolution API) |
| MVP / produção inicial | VPS (Hetzner/DigitalOcean) + Docker Compose + Caddy (SSL + roteamento) |
| Escala | Azure Container Apps + Azure Database for PostgreSQL |
| CI/CD | GitHub Actions |
| Observabilidade | OpenTelemetry + Grafana + Prometheus |
| Storage | MinIO (self-hosted) ou Azure Blob |

---

## 3. Multi-Tenancy

### Estratégia de Isolamento
- Schema por tenant no PostgreSQL: `tenant_{slug}` (ex: `tenant_barbearia-joao`)
- Schema `public` para tabelas globais: `tenants`, `users`, `plans`
- Todas as queries tenant-scoped filtradas por Global Query Filter no EF Core

### Resolução de Tenant (ordem de prioridade)
1. Domínio próprio do cliente: `barbeariadojoao.com.br` (CNAME → plataforma)
2. Subdomínio da plataforma: `joao.horafy.com.br`
3. Alias: `horafy.com.br/joao`

- `TenantMiddleware` resolve o tenant antes de qualquer requisição e injeta `ITenantContext` via DI
- Certificados SSL automáticos via Caddy (Let's Encrypt) para domínios próprios

---

## 4. Autenticação e Autorização

### Perfis de usuário

| Perfil | Login permitido | Role |
|---|---|---|
| Cliente final | Google OAuth2 ou Apple Sign-In apenas | `Customer` |
| Owner do tenant | E-mail + senha **ou** OAuth | `TenantOwner` |
| Admin do tenant | E-mail + senha **ou** OAuth | `TenantAdmin` |
| Admin da plataforma | E-mail + senha | `PlatformAdmin` |

### Decisão de implementação
- `RegisterWithEmailCommand` e `LoginWithEmailCommand` existentes são **exclusivos para owners/admins**.
- No portal do cliente, exibir apenas os botões Google e Apple — sem formulário de e-mail.
- JWT inclui claims: `tenant_id`, `tenant_schema`, `role`, `sub`.

---

## 5. Domain Model

### Entidades Globais (schema `public`)
- `Tenant` — agregado raiz com `Slug`, `CustomDomain`, `SchemaName`, `TenantTheme`, `TenantPlan`, `TenantVertical`, `TimeZoneId`
- `User` — dados de autenticação, `ExternalId` (OAuth), `Role`, `TenantId` (null para PlatformAdmin)

### Entidades por Tenant (schema `tenant_{slug}`)

#### Resource *(migração de `Professional` na Sprint 4)*
```csharp
public sealed class Resource : BaseEntity
{
    public string Name { get; }
    public ResourceType Type { get; }       // Professional | PhysicalSpace | Equipment | Court
    public string? Email { get; }
    public string? Phone { get; }
    public string? Specialty { get; }       // usado quando Type = Professional
    public string? Bio { get; }
    public string? AvatarUrl { get; }
    public Guid? UserId { get; }            // vínculo opcional com User autenticado
    public bool IsActive { get; }
}
```

#### Service
```csharp
public sealed class Service : BaseEntity
{
    public string Name { get; }
    public string? Description { get; }
    public int DurationMinutes { get; }
    public decimal Price { get; }
    public string? Category { get; }
    public bool IsActive { get; }
}
```

#### ResourceService *(tabela de relação — Sprint 4)*
```csharp
// Quais serviços um recurso pode executar
public sealed class ResourceService : BaseEntity
{
    public Guid ResourceId { get; }
    public Guid ServiceId { get; }
}
```

#### BusinessHours *(Sprint 4)*
```csharp
// Grade de funcionamento do tenant por dia da semana
public sealed class BusinessHours : BaseEntity
{
    public DayOfWeek DayOfWeek { get; }
    public TimeOnly OpenTime { get; }
    public TimeOnly CloseTime { get; }
    public bool IsOpen { get; }
}
```

#### AvailabilityRule *(Sprint 4)*
```csharp
// Disponibilidade regular de um recurso por dia da semana
public sealed class AvailabilityRule : BaseEntity
{
    public Guid ResourceId { get; }
    public DayOfWeek DayOfWeek { get; }
    public TimeOnly StartTime { get; }
    public TimeOnly EndTime { get; }
    public int SlotDurationMinutes { get; }
    public int BreakAfterMinutes { get; }   // intervalo entre atendimentos
}
```

#### AvailabilityException *(Sprint 4)*
```csharp
// Folga, manutenção, feriado ou horário especial de um recurso
public sealed class AvailabilityException : BaseEntity
{
    public Guid ResourceId { get; }
    public DateOnly Date { get; }
    public bool IsBlocked { get; }          // true = recurso indisponível o dia todo
    public TimeOnly? CustomStart { get; }   // horário especial (se não bloqueado)
    public TimeOnly? CustomEnd { get; }
    public string? Reason { get; }
}
```

#### Booking
```csharp
public sealed class Booking : BaseEntity
{
    public Guid ServiceId { get; }
    public Guid ResourceId { get; }         // era ProfessionalId — alterado na Sprint 4
    public Guid CustomerId { get; }
    public string CustomerName { get; }
    public string CustomerEmail { get; }
    public DateTimeOffset ScheduledAt { get; }
    public DateTimeOffset EndsAt { get; }
    public int DurationMinutes { get; }
    public string? Notes { get; }
    public BookingStatus Status { get; }
    public string? CancellationReason { get; }
    public Guid? RecurrenceGroupId { get; } // Sprint 5 — agendamentos recorrentes
    public DateTimeOffset? ConfirmedAt { get; }
    public DateTimeOffset? CancelledAt { get; }
    public DateTimeOffset? CompletedAt { get; }
}
```

#### BookingStatus
```
Pending → Confirmed → Completed
Pending → Cancelled
Confirmed → Cancelled
Confirmed → NoShow
```

### Padrões Obrigatórios (todos os agregados)
- `BaseEntity`: `Id` (Guid), `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `DeletedAt` (soft delete)
- **Result Pattern**: `Result<T>` / `Error` — sem exceções para fluxo de negócio
- **Domain Events**: publicados via `RaiseDomainEvent()`, persistidos via Outbox Pattern
- **Auditoria automática**: override de `SaveChangesAsync` no `DbContext`

---

## 6. Módulos do Sistema

### 6.1 Tenants & Configuração *(Sprint 3 — concluído)*
- CRUD tenant com onboarding
- Identidade visual (`TenantTheme` — owned entity JSON)
- Domínio próprio (`SetCustomDomain`, `RemoveCustomDomain`)
- Ativação/suspensão (PlatformAdmin)
- Planos e trial de 14 dias

### 6.2 Catálogo de Serviços *(Sprint 3 — scaffolding concluído)*
- CRUD de serviços (nome, descrição, duração, preço, categoria)
- Ativação/desativação
- Serviços complementares para upsell (Sprint 5)

### 6.3 Recursos e Disponibilidade *(Sprint 4)*
- CRUD de `Resource` (migração de `Professional`)
- Vínculo `ResourceService` (quais serviços o recurso executa)
- Grade semanal via `AvailabilityRule`
- Exceções via `AvailabilityException` (folgas, feriados, bloqueios)
- `BusinessHours` do tenant
- **Algoritmo de disponibilidade**: dado `(serviceId, date)`, retornar slots livres
  - Filtrar `AvailabilityRule` pelo dia da semana
  - Remover slots bloqueados por `AvailabilityException`
  - Remover slots ocupados por `Booking` com status ativo (Pending/Confirmed)
  - Aplicar `BreakAfterMinutes` entre atendimentos
  - Locking otimista no banco para evitar double-booking

### 6.4 Agendamento *(Sprint 5)*
- Fluxo: Disponibilidade → Serviço/Recurso/Horário → Confirmação → Pagamento (opcional)
- Pré-agendamento (reserva sem pagamento, com prazo)
- Recorrência: semanal / quinzenal / mensal com `RecurrenceGroupId`
- Fila de espera: cliente entra na fila se horário indisponível
- Política de cancelamento configurável por tenant (prazo mínimo, taxa)
- Lembretes automáticos: D-1 e H-2 (via Job + consumer RabbitMQ)
- Múltiplos serviços no mesmo agendamento (duração = soma)

### 6.5 Pagamento *(Sprint 6)*
- Gateway: **Mercado Pago** (`MercadoPagoPaymentGateway : IPaymentGateway`)
- Métodos: PIX (preferencial), cartão de crédito/débito, boleto
- Fluxos: pagamento imediato, pré-agendamento com link posterior, sinal/entrada
- Webhooks de confirmação → `PagamentoConfirmadoEvent` → atualiza `Booking.Status`
- Taxa de cancelamento cobrada automaticamente se fora do prazo
- Relatório financeiro por período/serviço/recurso

### 6.6 Notificações & Mensageria *(Sprint 7)*

**Eventos publicados no RabbitMQ:**
| Evento | Consumers |
|---|---|
| `AgendamentoCriadoEvent` | Notificação (WhatsApp + e-mail), Audit |
| `AgendamentoConfirmadoEvent` | Notificação |
| `AgendamentoCanceladoEvent` | Notificação, Audit |
| `AgendamentoLembreteEvent` | Notificação (job D-1 / H-2) |
| `PagamentoConfirmadoEvent` | Booking (atualiza status), Notificação |
| `PagamentoPendenteEvent` | Notificação (envia link) |
| `AvaliacaoRecebidaEvent` | Audit |

**WhatsApp:**
- MVP: Evolution API (self-hosted no Docker)
- Produção: Twilio WhatsApp Business API
- Interface `IWhatsAppService` abstrai a troca sem alterar os consumers

**Templates:** configuráveis por tenant no painel admin, com variáveis `{{cliente_nome}}`, `{{horario}}`, etc.

### 6.7 Clientes (Usuários Finais) *(Sprint 8)*
- Login exclusivamente via Google OAuth2 ou Apple Sign-In
- Perfil: nome, foto (via OAuth), telefone (WhatsApp), histórico
- Favoritos, avaliações (1–5 estrelas + comentário)
- Wallet de créditos/vouchers (Sprint 11)

### 6.8 Observabilidade & Auditoria *(Sprint 10)*
- Serilog com structured logging + `X-Correlation-Id` propagado
- Tabela `audit_logs` por schema: `tenant_id`, `user_id`, `action`, `entity_type`, `entity_id`, `old_value` (JSON), `new_value` (JSON), `ip`, `user_agent`, `timestamp`
- OpenTelemetry → Grafana + Prometheus
- Alertas automáticos para falhas em pagamento e envio de notificações

---

## 7. Frontend — Estrutura de Páginas

### Portal do Cliente (por tenant, SSR para SEO)
```
/                     → Home: banner, serviços em destaque, equipe, avaliações
/servicos             → Catálogo com filtros e busca
/servicos/:id         → Detalhe + serviços complementares (upsell)
/agendar              → Wizard: Serviço → Recurso → Data/Hora → Confirmação → Pagamento
/minha-conta          → Histórico, próximos agendamentos, favoritos, avaliações
/agendar/:id/status   → Status do agendamento
```

### Painel Administrativo do Tenant
```
/admin/dashboard      → Métricas: agendamentos hoje/semana, receita, cancelamentos, avaliação média
/admin/agenda         → Calendário dia/semana/mês com drag-and-drop
/admin/agendamentos   → Lista com filtros e exportação CSV
/admin/clientes       → CRM: histórico, frequência, valor gasto
/admin/servicos       → CRUD de serviços + upsell
/admin/recursos       → CRUD de recursos/profissionais + disponibilidade
/admin/financeiro     → Relatórios, extrato de transações
/admin/notificacoes   → Templates de mensagens WhatsApp/e-mail
/admin/configuracoes  → Identidade visual, domínio, horários, plano, integrações
```

### Painel Super Admin (plataforma)
```
/platform/tenants     → Listagem, status, plano, métricas globais
/platform/planos      → Gestão de planos e limites
/platform/financeiro  → Receita da plataforma
/platform/suporte     → Impersonate tenant, logs de erros globais
```

---

## 8. Segurança

- Isolamento: Global Query Filter no EF Core garante `tenant_schema` em todas as queries
- JWT com `tenant_id`, `tenant_schema`, `role` — validados a cada requisição
- Rate limiting por tenant e por endpoint (ex: máx 10 tentativas de login/min por IP)
- CORS configurado por domínio do tenant
- LGPD: consentimento no primeiro acesso, endpoint de exportação e exclusão de dados pessoais
- Secrets em variáveis de ambiente (VPS) ou Azure Key Vault (cloud) — nunca em `appsettings`
- FluentValidation em todos os Commands/Queries

---

## 9. Testes — Cobertura Mínima Obrigatória

### Unitários (xUnit + Moq + FluentAssertions)
1. Verificação de disponibilidade (conflitos, bloqueios, `BreakAfterMinutes`)
2. Cálculo de valor total (múltiplos serviços, descontos)
3. Geração de slots recorrentes (semanal/quinzenal — feriados)
4. Validação de política de cancelamento (prazo, taxa)
5. Resolução de tenant por domínio/subdomínio (middleware)
6. Publicação de eventos no Outbox Pattern
7. Caução e multa por atraso (aluguel)
8. Race condition em locações simultâneas (estoque)
9. Geração de link de pagamento + webhook de confirmação
10. Retry de notificações em falha (consumer RabbitMQ)

### Integração (Testcontainers — PostgreSQL real)
- Criação de tenant com schema próprio e migration automática
- Fluxo completo: API → DB → Event → Consumer
- Isolamento de dados entre dois tenants na mesma instância

---

## 10. Roadmap de Sprints

| Sprint | Foco | Entregável principal |
|---|---|---|
| ✓ 1 | Infraestrutura base | .NET, EF Core, multi-tenancy, Docker Compose |
| ✓ 2 | Autenticação | Google/Apple OAuth, JWT, e-mail+senha (owners) |
| ✓ 3 | Módulo Tenant + scaffold Catálogo | CRUD tenant, tema, domínio; entidades Service/Professional/Booking (Professional migra para Resource na Sprint 4) |
| → **4** | **Agenda & Disponibilidade** | `Resource` (migração), `BusinessHours`, `AvailabilityRule/Exception`, `ResourceService`, algoritmo de slots, testes de race condition |
| 5 | Agendamento completo | Recorrência, fila de espera, política de cancelamento, lembretes |
| 6 | Pagamento | Mercado Pago (PIX + cartão + boleto), webhooks, taxa de cancelamento |
| 7 | Mensageria | RabbitMQ consumers, Evolution API WhatsApp, templates, retry/dead-letter |
| 8 | Frontend — Portal Cliente | Next.js: home, catálogo, wizard de agendamento, login OAuth |
| 9 | Frontend — Painel Admin | Calendário drag-and-drop, CRUD recursos/serviços, financeiro |
| 10 | Observabilidade | OpenTelemetry, Grafana, audit_logs, testes de carga, alertas |
| 11 | Onboarding & PWA | Wizard 5 passos, next-pwa, upsell, avaliações, wallet |
| 12 | Super Admin & Go-live | Painel plataforma, gestão de planos, deploy VPS (Caddy), smoke tests |

---

## 11. Fora do Escopo do MVP

- Emissão de NF-e / NFS-e (pós go-live via Focus NFe ou eNotas)
- Aplicativo mobile nativo (React Native / Flutter) — apenas PWA
- Internacionalização / i18n (pt-BR apenas)
- Módulo fiscal / contabilidade
- Marketplace público de tenants

---

## 12. Decisões de Arquitetura Registradas

| # | Decisão | Escolha | Motivo |
|---|---|---|---|
| 1 | Frontend | Next.js 14 + shadcn/ui | SSR para SEO do portal público; componentes prontos para wizard/calendar |
| 2 | Pagamento | Mercado Pago | PIX obrigatório no BR; `IPaymentGateway` abstrai troca futura para Stripe |
| 3 | WhatsApp | Evolution API (MVP) → Twilio (produção) | Zero custo no desenvolvimento; `IWhatsAppService` isola a migração |
| 4 | Infraestrutura | VPS + Docker Compose → Azure quando escalar | Docker Compose já existe; Caddy resolve SSL + domínios próprios |
| 5 | Cobrança | Mensalidade por plano | `TenantPlan` já no domínio; receita previsível; mais simples que split |
| 6 | NF-e | Fora do MVP | Complexidade municipal alta; não bloqueante para maioria dos verticais |
| 7 | Mobile | PWA apenas (next-pwa) | Mesma codebase; app nativo só com demanda validada |
| 8 | Idioma | pt-BR apenas | Mercado BR; `Tenant.Locale` no domínio para expansão futura sem refatoração |
| 9 | Auth clientes | OAuth apenas (Google + Apple) | Reduz atrito no cadastro; sem gerenciamento de senha pelo cliente |
| 10 | Entidade recurso | `Resource` com `ResourceType` | Um motor de disponibilidade para todos os verticais; migração barata agora |
