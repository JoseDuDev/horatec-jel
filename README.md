# Horafy

Plataforma SaaS multi-tenant para agendamento e locação, voltada a múltiplos verticais de negócio (barbearias, clínicas, quadras esportivas, salões de festas, locação de brinquedos, entre outros). Cada tenant tem dados isolados, tema próprio e configurações independentes.

---

## Índice

- [Visão Geral](#visão-geral)
- [Stack Tecnológica](#stack-tecnológica)
- [Arquitetura](#arquitetura)
- [Estrutura de Pastas](#estrutura-de-pastas)
- [Funcionalidades](#funcionalidades)
- [Multi-tenancy](#multi-tenancy)
- [Banco de Dados](#banco-de-dados)
- [Configuração e Variáveis de Ambiente](#configuração-e-variáveis-de-ambiente)
- [Como Rodar Localmente](#como-rodar-localmente)
- [Testes](#testes)
- [Deploy](#deploy)
- [Roadmap de Sprints](#roadmap-de-sprints)

---

## Visão Geral

O Horafy permite que empresas de qualquer porte criem seu próprio portal de agendamentos ou locações sem código. A plataforma provisiona automaticamente um schema de banco de dados isolado para cada tenant, oferece checkout com pagamento integrado (Mercado Pago), notificações via WhatsApp e e-mail, programa de fidelidade com carteira digital, vouchers e muito mais.

---

## Stack Tecnológica

### Backend

| Camada | Tecnologia |
|--------|-----------|
| Runtime | .NET 8 / C# |
| Banco de dados | PostgreSQL 16 (schema-per-tenant) |
| ORM | Entity Framework Core 8 + Npgsql |
| CQRS / Mediator | MediatR 12 |
| Mensageria | RabbitMQ 3.13 + MassTransit 8.4 |
| Cache | Redis 7 |
| Autenticação | JWT + OAuth2 (Google, Apple) |
| Pagamentos | Mercado Pago (abstração trocável) |
| WhatsApp | Evolution API |
| Scheduler | Quartz + MassTransit |
| E-mail | MailKit (SMTP) |
| Logs | Serilog + Seq |
| Documentação de API | Swagger + Scalar |
| Versionamento de API | Asp.Versioning 8.1 |
| Testes | xUnit + Moq + FluentAssertions |

### Frontend

| Camada | Tecnologia |
|--------|-----------|
| Framework | Next.js 16.2 (App Router) |
| UI | React 19 + shadcn/ui + Tailwind CSS 4 |
| State | Zustand 5 |
| Formulários | React Hook Form + Zod |
| HTTP | TanStack React Query 5 |
| Datas | date-fns 4 |
| Gráficos | Recharts 3 |
| PWA | @ducanh2912/next-pwa |
| Testes unitários | Vitest + Testing Library |
| Testes E2E | Playwright 1.60 |

### Infraestrutura

| Componente | Tecnologia |
|-----------|-----------|
| Containers | Docker + Docker Compose |
| CI/CD | GitHub Actions |
| Reverse Proxy | Caddy (TLS automático via Let's Encrypt) |
| Observabilidade | Serilog → Seq |
| Health Checks | AspNetCore.HealthChecks |

---

## Arquitetura

O projeto segue **Clean Architecture** com **CQRS** no backend e **App Router** no frontend.

```
┌──────────────────────────────────────────┐
│               Frontend (Next.js)          │
│  Admin Panel │ Customer Portal │ Catalog  │
└──────────────────────┬───────────────────┘
                       │ HTTPS / REST v1
┌──────────────────────▼───────────────────┐
│              Horafy.API (.NET 8)          │
│  Controllers → MediatR → Handlers        │
│  TenantMiddleware │ JWT Auth │ RBAC       │
└────────┬─────────────────────┬────────────┘
         │                     │
┌────────▼──────────┐  ┌──────▼──────────┐
│ Horafy.Application│  │Horafy.Infrastructure│
│  Commands/Queries │  │  Repositories   │
│  Domain Services  │  │  EF Core        │
└────────┬──────────┘  │  RabbitMQ       │
         │             │  Redis          │
┌────────▼──────────┐  │  Mercado Pago  │
│  Horafy.Domain    │  │  Evolution API │
│  Entities/Events  │  └──────┬─────────┘
│  Interfaces       │         │
└───────────────────┘  ┌──────▼─────────────────────┐
                       │       PostgreSQL 16          │
                       │  schema: public (plataforma) │
                       │  schema: tenant_{slug} (N×)  │
                       └────────────────────────────┘
```

### Padrões utilizados

- **CQRS** — Commands e Queries separados por feature, cada um com seu handler
- **Outbox Pattern** — garantia de entrega de eventos via tabela `outbox_messages` + consumer assíncrono
- **Repository Pattern** — contratos em `Horafy.Domain`, implementações em `Horafy.Infrastructure`
- **Unit of Work** — transações atômicas envolvendo múltiplos repositórios
- **Domain Events** — entidades emitem eventos (`BookingCreated`, `PaymentConfirmed`) que disparam notificações e outras reações
- **Result Pattern** — todos os handlers retornam `Result<T>` em vez de lançar exceções
- **Pipeline Behaviors** (MediatR) — validação e logging automáticos antes de cada handler

---

## Estrutura de Pastas

```
Horafy/
├── src/
│   ├── Horafy.Shared/              # Result<T>, DTOs compartilhados, paginação
│   ├── Horafy.Domain/              # Entidades, eventos, interfaces (sem dependências externas)
│   │   ├── Entities/
│   │   │   ├── Tenants/            # Tenant, TenantCapability, TenantTheme, PaymentSettings
│   │   │   ├── Users/              # User, UserRole, UserPermission
│   │   │   ├── Bookings/           # Booking, BookingService, Waitlist
│   │   │   ├── Services/           # Service (catálogo de serviços)
│   │   │   ├── Resources/          # Resource (profissional / sala / equipamento)
│   │   │   ├── Rentals/            # RentableItem (inventário)
│   │   │   ├── Availability/       # AvailabilityRule, BusinessHours, Holiday
│   │   │   ├── Payments/           # Payment
│   │   │   ├── Notifications/      # NotificationTemplate, NotificationLog
│   │   │   ├── Reviews/            # Review
│   │   │   ├── Integrations/       # IntegrationApiKey, IntegrationWebhook
│   │   │   ├── Vouchers/           # Voucher
│   │   │   └── Wallet/             # Wallet, WalletTransaction
│   │   └── Events/                 # BookingCreated, PaymentConfirmed, etc.
│   │
│   ├── Horafy.Application/         # Features (Commands/Queries/Handlers)
│   │   └── Features/
│   │       ├── Auth/               # Login (email, Google, Apple), refresh JWT
│   │       ├── Bookings/           # Criar, cancelar, reagendar, concluir
│   │       ├── Rentals/            # Criar, retirar, devolver item
│   │       ├── Availability/       # Slots disponíveis, regras, exceções
│   │       ├── Services/           # CRUD serviços
│   │       ├── Resources/          # CRUD recursos / profissionais
│   │       ├── Payments/           # Criar pagamento, webhook, reembolso
│   │       ├── Notifications/      # Enviar, gerenciar templates
│   │       ├── Tenants/            # Criar, atualizar, limites de plano
│   │       ├── Customers/          # Perfil, telefone, histórico
│   │       ├── Dashboard/          # Métricas administrativas
│   │       ├── Reports/            # Relatórios de receita e agendamentos
│   │       ├── Favorites/          # Serviços favoritos
│   │       ├── Vouchers/           # Criar e aplicar vouchers
│   │       ├── Wallet/             # Operações de carteira e transações
│   │       ├── Catalog/            # Catálogo público
│   │       ├── Reviews/            # Avaliações
│   │       ├── Waitlist/           # Fila de espera
│   │       └── Integrations/       # Webhooks externos e chaves de API
│   │
│   ├── Horafy.Infrastructure/      # Persistência, Gateways, Messaging, Auth
│   │   ├── Persistence/
│   │   │   ├── HorafyDbContext     # DbContext público (tenants, users, logs)
│   │   │   ├── TenantDbContext     # DbContext por tenant (schema isolado)
│   │   │   ├── Interceptors/       # Audit, Outbox
│   │   │   ├── Migrations/         # Migrations EF Core
│   │   │   └── Repositories/       # Implementações dos repositórios
│   │   ├── MultiTenancy/           # TenantMiddleware, TenantService, TenantSchemaService
│   │   ├── Auth/                   # JwtTokenService, GoogleOAuth, AppleOAuth, BCrypt
│   │   ├── Gateways/               # MercadoPagoPaymentGateway, FakePaymentGateway
│   │   ├── Email/                  # SmtpEmailService
│   │   └── Messaging/              # Consumers, Publishers, Outbox, Jobs (Quartz)
│   │
│   └── Horafy.API/                 # Entry point HTTP
│       ├── Controllers/V1/         # 25+ controllers versionados
│       ├── Middleware/             # ExceptionHandling, TenantMiddleware
│       └── Program.cs
│
├── tests/
│   ├── Horafy.Domain.Tests/
│   ├── Horafy.Application.Tests/
│   └── Horafy.Infrastructure.Tests/
│
├── frontend/                       # Next.js App Router
│   ├── app/
│   │   ├── (auth)/                 # Login, registro, OAuth callbacks
│   │   ├── (app)/                  # Painel admin protegido
│   │   └── (customer)/             # Portal do cliente
│   ├── components/                 # Componentes reutilizáveis + shadcn/ui
│   ├── store/                      # Zustand stores (auth, tenant)
│   ├── lib/                        # API client, utilitários
│   └── e2e/                        # Testes Playwright
│
├── docker-compose.yml              # Stack de desenvolvimento
├── docker-compose.prod.yml         # Stack de produção
├── Caddyfile                       # Reverse proxy + TLS
├── .env.example                    # Template de variáveis de ambiente
└── .github/workflows/ci.yml        # Pipeline CI/CD
```

---

## Funcionalidades

### Autenticação e Autorização

- Login com e-mail/senha (BCrypt)
- Login social: Google OAuth 2.0 e Apple Sign-In
- JWT com claims de tenant (impede replay cross-tenant)
- Roles: `PlatformAdmin`, `TenantOwner`, `TenantAdmin`, `TenantStaff`, `Customer`
- Permissões granulares (15+ permissões configuráveis por role)

### Agendamentos (Módulo Appointments)

- Criar, confirmar, cancelar, reagendar, concluir agendamentos
- Detecção automática de conflitos (anti double-booking)
- Agendamentos recorrentes com grupos de recorrência
- Múltiplos serviços por agendamento
- Fila de espera com promoção automática
- Integração com pagamentos

### Locações (Módulo Rentals)

- Inventário de itens locáveis com controle de estoque
- Verificação de disponibilidade por período contra estoque disponível
- Ciclo de vida: Reservado → Retirado → Devolvido
- Caução (depósito de segurança) com reembolso automático
- Cálculo de multa por atraso
- Dias de buffer para higienização/manutenção

### Pagamentos

- Checkout via Mercado Pago
- Rastreamento de status (NotRequired, Pending, Paid, PartiallyPaid, Refunded)
- Webhooks de confirmação de pagamento
- Reembolso total e parcial
- Pagamento por carteira digital ou gateway externo

### Disponibilidade

- Horários comerciais por dia da semana
- Calendário de feriados
- Regras de exceção (bloqueios de data/horário)
- Cálculo automático de slots disponíveis

### Portal do Cliente

- Catálogo público de serviços por busca e filtros avançados
- Wizard de agendamento com calendário mensal
- Histórico de agendamentos e locações
- Favoritar serviços
- Avaliações e notas
- Carteira de fidelidade
- Vouchers de desconto
- Gerenciamento de perfil

### Painel Administrativo

- Dashboard com métricas (receita, agendamentos, taxa de ocupação)
- Gestão de serviços e profissionais/recursos
- Visão geral de agendamentos com filtros
- Histórico de pagamentos
- Gerenciamento de templates de notificação
- Configuração do tenant (tema, horários, política de cancelamento)
- Relatórios de receita e agendamentos

### Super Admin (Plataforma)

- Visão geral de todos os tenants
- Atribuição de planos (Free, Starter, Professional, Enterprise)
- Habilitação de módulos por tenant (Appointments, Rentals)
- Relatórios financeiros consolidados
- Gestão de webhooks e integrações

### Notificações

- WhatsApp via Evolution API
- E-mail via SMTP
- Templates configuráveis com variáveis dinâmicas
- Log de notificações (auditoria)
- Disparo orientado a eventos (booking confirmado, pagamento recebido, lembrete, etc.)

### Fidelidade e Promoções

- Carteira digital com créditos
- Vouchers com desconto percentual ou fixo
- Configurações de fidelidade por tenant (taxa de acúmulo, valor mínimo)
- Histórico de transações da carteira

---

## Multi-tenancy

A estratégia de isolamento adotada é **schema-per-tenant** no PostgreSQL:

| Schema | Conteúdo |
|--------|---------|
| `public` | Tabelas globais da plataforma: `tenants`, `users`, `plan_configurations`, `integration_api_keys`, `notification_logs` |
| `tenant_{slug}` | Dados isolados por tenant: `bookings`, `services`, `resources`, `payments`, `rentable_items`, `reviews`, `wallet`, `vouchers`, `outbox_messages`, etc. |

**Resolução de tenant** ocorre via:
1. Subdomínio: `meu-negocio.horafy.com.br`
2. Domínio personalizado: `agendamentos.minhaclinica.com.br`

O `TenantMiddleware` resolve o tenant em cada request e o injeta via DI para que todos os repositórios usem automaticamente o schema correto.

---

## Banco de Dados

### Schema público (plataforma)

```sql
tenants               -- Configuração, plano, capabilities, tema
users                 -- Usuários da plataforma (admins, owners)
plan_configurations   -- Limites de plano (editáveis)
integration_api_keys  -- Chaves de API para integrações externas
integration_webhooks  -- Endpoints de webhook de saída
notification_logs     -- Histórico de notificações enviadas
```

### Schema por tenant (`tenant_{slug}`)

```sql
services              -- Catálogo de serviços
resources             -- Profissionais / salas / equipamentos
availability_rules    -- Regras de disponibilidade
business_hours        -- Horários de funcionamento
holidays              -- Calendário de feriados
bookings              -- Agendamentos e locações
booking_services      -- Serviços por agendamento
rentable_items        -- Inventário de locação
payments              -- Registros de pagamento
notification_templates -- Templates personalizáveis
reviews               -- Avaliações dos clientes
favorites             -- Serviços favoritos
vouchers              -- Vouchers de desconto
wallet                -- Carteira de fidelidade
wallet_transactions   -- Transações da carteira
outbox_messages       -- Outbox para entrega garantida de eventos
```

---

## Configuração e Variáveis de Ambiente

Copie `.env.example` para `.env` e preencha:

```env
# Banco de dados
POSTGRES_PASSWORD=horafy_dev_pass

# Cache
REDIS_PASSWORD=horafy_redis_pass

# Mensageria
RABBITMQ_PASSWORD=horafy_rabbit_pass

# JWT (mínimo 64 caracteres)
JWT_SECRET=<secret>

# Mercado Pago
MERCADOPAGO_ACCESS_TOKEN=<token>
MERCADOPAGO_WEBHOOK_SECRET=<secret>

# WhatsApp (Evolution API)
EVOLUTION_API_URL=http://localhost:8081
EVOLUTION_API_KEY=<key>

# OAuth - Google
GOOGLE_CLIENT_ID=<id>
GOOGLE_CLIENT_SECRET=<secret>

# OAuth - Apple
APPLE_CLIENT_ID=<id>
APPLE_TEAM_ID=<id>
APPLE_KEY_ID=<id>
```

---

## Como Rodar Localmente

### Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)

### 1. Subir a infraestrutura (Docker)

```bash
docker compose up -d
```

Isso sobe: PostgreSQL (5433), Redis (6380), RabbitMQ (5673 / UI: 15673) e Seq (5341).

### 2. Aplicar as migrations

```bash
cd src/Horafy.API
dotnet ef database update --project ../Horafy.Infrastructure
```

### 3. Rodar a API

```bash
cd src/Horafy.API
dotnet run
```

API disponível em `https://localhost:8443` / `http://localhost:8083`.  
Documentação Swagger: `https://localhost:8443/scalar/v1`

### 4. Rodar o Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend disponível em `http://localhost:3000`.

---

## Testes

### Backend (347 testes)

```bash
dotnet test
```

Categorias cobertas:
- Domain: entidades, lógica de negócio, domain events
- Application: handlers de Commands/Queries com mocks
- Infrastructure: repositórios, integração com banco

### Frontend (unitários)

```bash
cd frontend
npm test
```

### E2E (Playwright — 48 testes em 12 specs)

```bash
cd frontend
npx playwright test
```

Fluxos cobertos: onboarding, booking completo, locação (lifecycle), pagamento, fidelidade, portal do cliente, reembolso, limites de plano.

---

## Deploy

### Produção com Docker Compose

```bash
# Configure as variáveis de ambiente de produção
cp .env.prod.example .env.prod

# Suba os serviços
docker compose -f docker-compose.prod.yml up -d
```

O `Caddyfile` configura automaticamente TLS via Let's Encrypt para o domínio principal e domínios customizados dos tenants.

### CI/CD (GitHub Actions)

O pipeline em `.github/workflows/ci.yml` executa em cada push:

1. Build da API (.NET 8)
2. Execução de todos os testes backend
3. Build do frontend (Next.js)
4. Execução dos testes unitários do frontend
5. Deploy automático ao branch `main`

---

## Roadmap de Sprints

| Sprint | Área | Status |
|--------|------|--------|
| 1–3 | Infraestrutura, Auth, Multi-tenancy base | ✅ Concluído |
| 4 | Recursos e Disponibilidade | ✅ Concluído |
| 5 | Agendamentos (CRUD, recorrência, fila) | ✅ Concluído |
| 6 | Pagamentos (Mercado Pago, webhooks, reembolso) | ✅ Concluído |
| 7 | Notificações (WhatsApp, e-mail, templates) | ✅ Concluído |
| 8 | Clientes (perfil, telefone, histórico) | ✅ Concluído |
| 9 | Painel Admin (11 páginas) | ✅ Concluído |
| 10 | Portal do Cliente (6 páginas + wizard) | ✅ Concluído |
| 11 | PWA, Onboarding, Avaliações, Upsell | ✅ Concluído |
| 12 | Super Admin (tenants, planos, financeiro) | ✅ Concluído |
| 13 | Carteira + Vouchers | ✅ Concluído |
| 14 | Checkout integrado (Carteira + Voucher) | ✅ Concluído |
| 15 | Fidelidade + Autocancelamento | ✅ Concluído |
| Rentals 0–6 | Módulo completo de locação | ✅ Concluído |
| Plans 0–4 | Capabilities + Limites por plano | ✅ Concluído |
| Sprint 6 atual | Busca avançada, calendário mensal, perfil do cliente | ✅ Concluído |

---

## Contribuindo

1. Crie uma branch a partir de `main`: `git checkout -b feat/minha-feature`
2. Faça commits descritivos seguindo o padrão Conventional Commits
3. Abra um Pull Request descrevendo o que foi feito e como testar

---

*Horafy — Plataforma de agendamentos e locações para qualquer negócio.*
