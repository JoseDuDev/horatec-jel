# Prompt — SaaS de Agendamento Multi-Tenant

## Contexto Geral

Você é um arquiteto de software sênior especializado em SaaS B2B. Seu objetivo é projetar e implementar, passo a passo, um sistema completo de agendamento multi-tenant voltado para diferentes segmentos de negócio. O sistema deve ser robusto, escalável, seguro e altamente personalizável para cada tenant.

---

## Visão Geral do Produto

Plataforma SaaS de agendamento que atende múltiplos verticais de negócio (barbearias, salões de festa, quadras esportivas, aluguel de brinquedos, aluguel de ferramentas, consultórios médicos e estéticos, entre outros). Cada tenant possui seu próprio espaço isolado com domínio/subdomínio configurável, identidade visual própria e configurações independentes.

---

## Stack Tecnológica

### Backend
- **Runtime:** .NET 8 (ou superior)
- **Linguagem:** C#
- **Arquitetura:** Clean Architecture + CQRS com MediatR
- **ORM:** Entity Framework Core 8 com PostgreSQL
- **Banco de Dados:** PostgreSQL 16+
- **Mensageria:** RabbitMQ (ou Azure Service Bus como alternativa cloud) com MassTransit
- **Cache:** Redis
- **Autenticação:** ASP.NET Core Identity + OAuth2 (Google, Apple) via OpenIddict ou Keycloak
- **Documentação de API:** Swagger / Scalar
- **Testes:** xUnit + Moq + FluentAssertions + Testcontainers (integração com PostgreSQL real)
- **Logs:** Serilog com sink para Seq ou OpenTelemetry (rastreabilidade distribuída)
- **Pagamentos:** Stripe ou Mercado Pago (abstraído por interface para troca futura)
- **WhatsApp:** Evolution API (self-hosted) ou Twilio WhatsApp Business API

### Frontend
- **Framework:** React 18+ (Next.js 14+ com App Router) **ou** Vue 3 (Nuxt 3 como alternativa)
- **Estilização:** Tailwind CSS + shadcn/ui (React) ou PrimeVue (Vue)
- **Estado Global:** Zustand (React) ou Pinia (Vue)
- **Formulários:** React Hook Form + Zod (React) ou VeeValidate + Zod (Vue)
- **Build Tool:** Vite
- **Testes Frontend:** Vitest + Testing Library

### Infraestrutura / DevOps
- **Containers:** Docker + Docker Compose (desenvolvimento) / Kubernetes (produção)
- **CI/CD:** GitHub Actions
- **Observabilidade:** OpenTelemetry + Grafana + Prometheus
- **Storage:** MinIO (self-hosted) ou AWS S3 / Azure Blob para imagens e assets

---

## Requisitos de Multi-Tenancy

### Isolamento de Tenant
- Estratégia: **Schema por tenant no PostgreSQL** (melhor isolamento sem custo de banco separado)
- Cada tenant possui seu próprio schema: `tenant_{slug}` (ex: `tenant_barbearia_joao`)
- Schema compartilhado `public` para tabelas globais: tenants, planos, configurações de plataforma
- Middleware de resolução de tenant por: subdomínio, alias customizado ou domínio próprio do cliente

### Resolução de Domínio (por ordem de prioridade)
```
1. Domínio próprio do cliente   → barbeariadojoao.com.br  (CNAME apontando para a plataforma)
2. Subdomínio da plataforma     → joao.agendaai.com.br
3. Alias customizado            → agendaai.com.br/joao
```
- Tabela `tenants` com campos: `slug`, `custom_domain`, `subdomain`, `is_active`
- Middleware resolve o tenant antes de qualquer requisição e injeta `ITenantContext` via DI
- Certificados SSL automáticos via Let's Encrypt para domínios próprios (integração com Caddy ou Traefik)

---

## Arquitetura do Backend

### Estrutura de Solução (Clean Architecture)
```
src/
  AgendaAI.Domain/              # Entidades, Value Objects, Domain Events, Interfaces
  AgendaAI.Application/         # Use Cases (Commands/Queries), DTOs, Validators, Mappings
  AgendaAI.Infrastructure/      # EF Core, Repositórios, Mensageria, Integrações externas
  AgendaAI.API/                 # Controllers, Middlewares, Configuração de DI, Swagger
  AgendaAI.Shared/              # Contratos compartilhados, Result pattern, Pagination

tests/
  AgendaAI.Domain.Tests/
  AgendaAI.Application.Tests/
  AgendaAI.Infrastructure.Tests/
  AgendaAI.API.Tests/
```

### Padrões Obrigatórios
- **Result Pattern** para retorno de erros sem exceções desnecessárias (`Result<T>`, `Error`)
- **Domain Events** para comunicação entre agregados (ex: `AgendamentoCriadoEvent` → dispara notificação WhatsApp)
- **Outbox Pattern** para garantia de entrega de mensagens (tabela `outbox_messages` por schema)
- **Soft Delete** em todas as entidades com `deleted_at` e `deleted_by`
- **Auditoria automática:** `created_at`, `updated_at`, `created_by`, `updated_by` via `SaveChangesAsync` override
- **Paginação cursor-based** para listagens de alta performance

### Módulos do Sistema

#### 1. Tenants & Configuração
```
- Cadastro e onboarding de tenant
- Planos e limites (ex: máximo de atendentes, agendamentos/mês)
- Configuração de identidade visual (tema, cores primária/secundária, logo, banner, favicon)
- Configuração de domínio/subdomínio
- Horários de funcionamento (por dia da semana, feriados, exceções)
- Timezone por tenant
```

#### 2. Catálogo de Serviços
```
- Serviços com: nome, descrição, duração (em minutos), valor, imagem, categoria
- Serviços agregados/complementares (upsell automático — ex: "cabelo + barba com desconto")
- Visibilidade: público, privado, apenas para clientes recorrentes
- Variações de serviço (ex: "corte simples" vs "corte + lavagem")
```

#### 3. Recursos e Atendentes
```
- Recursos genéricos: podem ser profissionais, espaços físicos, equipamentos ou quadras
- Cada recurso tem: disponibilidade própria, lista de serviços que executa, capacidade simultânea
- Bloqueios de agenda (folgas, manutenção, feriados)
- Intervalo entre atendimentos configurável por recurso
```

#### 4. Agendamento
```
- Verificação de disponibilidade em tempo real (com locking otimista no banco)
- Fluxo: Consulta disponibilidade → Seleção de serviço/recurso/horário → Confirmação → Pagamento (opcional)
- Pré-agendamento: reserva sem pagamento imediato, com prazo de confirmação
- Recorrência: semanal, quinzenal, mensal (para quadras esportivas, terapias, etc.)
- Fila de espera automática: se horário indisponível, cliente entra na fila e é notificado
- Cancelamento com política configurável (prazo mínimo, taxa de cancelamento)
- Lembrete automático: D-1 e H-2 antes do agendamento via WhatsApp e/ou e-mail
```

#### 5. Pagamento
```
- Pagamento no ato do agendamento (Stripe / Mercado Pago)
- Pré-agendamento sem pagamento (link de pagamento enviado depois)
- Sinal/entrada configurável (ex: pagar 30% para confirmar)
- Cobrança automática de taxa de cancelamento fora do prazo
- Relatório financeiro por período, serviço e profissional
- Split de pagamento (ex: plataforma retém % por transação)
```

#### 6. Clientes (Usuários Finais)
```
- Login via Google OAuth2 ou Apple Sign-In (obrigatório — sem senha tradicional)
- Perfil: nome, foto (via OAuth), telefone (WhatsApp), histórico de agendamentos
- Consulta de serviços disponíveis com filtros (categoria, valor, disponibilidade)
- Favoritos: salvar serviços e estabelecimentos
- Avaliação pós-atendimento (1–5 estrelas + comentário)
- Wallet interna (créditos, vouchers, fidelidade)
```

#### 7. Notificações & Mensageria
```
- Eventos publicados no RabbitMQ (via MassTransit):
    AgendamentoCriado
    AgendamentoConfirmado
    AgendamentoCancelado
    AgendamentoLembrete
    PagamentoConfirmado
    PagamentoPendente
    AvaliacaoRecebida
- Consumers independentes por canal: WhatsApp, E-mail, Push (PWA)
- Templates de mensagem configuráveis por tenant (com variáveis: {{cliente_nome}}, {{horario}}, etc.)
- Histórico de notificações enviadas (sucesso, falha, retry)
```

#### 8. Logs & Observabilidade
```
- Serilog com structured logging em todos os serviços
- Correlation ID propagado em todas as requisições (X-Correlation-Id)
- Auditoria de ações críticas: login, agendamento, cancelamento, alteração de config, pagamento
- Tabela audit_logs: tenant_id, user_id, action, entity_type, entity_id, old_value (JSON), new_value (JSON), ip, user_agent, timestamp
- OpenTelemetry para traces distribuídos entre API, consumers e jobs
- Alertas automáticos para falhas em pagamento ou envio de notificações
```

---

## Modelos de Domínio por Vertical

### Barbearia
```yaml
Entidades principais:
  - Barbeiro (recurso com especialidades)
  - Serviço: { nome, categoria (cabelo|barba|sobrancelha|nariz|limpeza_de_pele), duracao_min, valor }
  - Cadeira (recurso físico, opcional — um barbeiro pode ter cadeira fixa)
  - Agendamento: { cliente, barbeiro, servico[], data_hora, valor_total, status }

Regras de negócio:
  - Um barbeiro pode ter agenda própria independente
  - Múltiplos serviços no mesmo agendamento (ex: cabelo + barba = soma de duração)
  - Intervalo de limpeza entre clientes configurável (ex: 5 min)
  - Comanda digital: visualização do agendamento com checklist de serviços
```

### Salão de Festas
```yaml
Entidades principais:
  - Espaço: { nome, capacidade_pessoas, descricao, fotos[], valor_hora, valor_diaria }
  - Pacote: { nome, descricao, inclui_decoracao, inclui_comida, inclui_bebida, valor_total }
  - Serviço Adicional: { nome, tipo (decoracao|buffet|seguranca|fotografo), valor }
  - Reserva: { cliente, espaco, data_inicio, data_fim, pacote, servicos_adicionais[], valor_total, status }

Regras de negócio:
  - Reserva por período (mínimo X horas), não por slot de minutos
  - Bloquear espaço imediatamente ao confirmar pagamento (ou sinal)
  - Checklist de contrato digital gerado no agendamento
  - Regra de lotação máxima por espaço
```

### Quadras Esportivas
```yaml
Entidades principais:
  - Quadra: { nome, tipo (futebol|beach_tennis|vôlei|basquete), cobertura (coberta|aberta), valor_hora }
  - Recurso Avulso: { nome, tipo (goleiro|churrasqueiro|material_esportivo), valor, quantidade_disponivel }
  - Reserva: { cliente, quadra, data_hora_inicio, duracao_horas, recorrencia, recursos_avulsos[], valor_total }

Regras de negócio:
  - Recorrência semanal/quinzenal com geração automática de slots futuros
  - Cancelamento de recorrência sem afetar reservas já pagas
  - Aluguel de materiais esportivos vinculado ao horário da quadra
  - Limite de reservas simultâneas por cliente
```

### Consultórios Médicos e Estéticos
```yaml
Entidades principais:
  - Profissional: { nome, especialidade, registro_profissional, foto }
  - Procedimento: { nome, descricao, duracao_min, valor, exige_avaliacao_previa (bool) }
  - Sala: { nome, equipamentos[] } (recurso físico vinculado ao profissional)
  - Consulta: { paciente, profissional, procedimento, data_hora, anamnese, status, observacoes }

Regras de negócio:
  - Procedimentos que exigem avaliação prévia não permitem agendamento online direto (pré-agendamento somente)
  - Bloqueio de agenda para procedimentos com recuperação (ex: não agendar no mesmo dia)
  - Envio de formulário de anamnese via link antes da consulta
  - Histórico médico do paciente (apenas para o tenant, nunca compartilhado entre tenants)
```

### Aluguel de Brinquedos / Ferramentas
```yaml
Entidades principais:
  - Item: { nome, descricao, categoria, fotos[], valor_diaria, valor_semanal, quantidade_disponivel, caução }
  - Locação: { cliente, itens[], data_retirada, data_devolucao, valor_total, caução_total, status }

Regras de negócio:
  - Controle de estoque em tempo real (múltiplas unidades do mesmo item)
  - Caução configurável por item (bloqueada no cartão ou paga separado)
  - Multa por atraso na devolução (configurável por item ou categoria)
  - Check-in e check-out com registro fotográfico (upload de fotos no agendamento)
```

---

## Personalização Visual por Tenant

### Configurações de Tema
```json
{
  "primary_color": "#2563EB",
  "secondary_color": "#7C3AED",
  "background_color": "#F8FAFC",
  "font_family": "Inter",
  "logo_url": "https://cdn.../logo.png",
  "favicon_url": "https://cdn.../favicon.ico",
  "banner_url": "https://cdn.../banner.jpg",
  "banner_text": "Agende agora com facilidade!",
  "show_reviews": true,
  "show_team": true,
  "show_services_prices": true,
  "social_links": { "instagram": "...", "whatsapp": "..." },
  "sections_order": ["banner", "services", "team", "reviews", "contact"]
}
```

- CSS Variables injetadas dinamicamente no frontend baseado nas configs do tenant
- Upload de imagens com redimensionamento automático (via job ou middleware)
- Preview em tempo real no painel administrativo antes de salvar

---

## Frontend — Estrutura de Páginas

### Portal do Cliente (público, por tenant)
```
/ (home)              → Banner, serviços em destaque, upsell, avaliações, equipe
/servicos             → Catálogo completo com filtros e busca
/servicos/:id         → Detalhe do serviço com serviços complementares sugeridos
/agendar              → Wizard: Serviço → Profissional/Recurso → Data/Hora → Confirmação → Pagamento
/minha-conta          → Histórico de agendamentos, próximos agendamentos, favoritos, avaliações
/agendar/:id/status   → Status do agendamento (confirmado, pendente, cancelado)
```

### Painel Administrativo do Tenant
```
/admin/dashboard      → Métricas: agendamentos hoje/semana, receita, taxa de cancelamento, avaliação média
/admin/agenda         → Visualização em calendário (dia/semana/mês) com drag-and-drop
/admin/agendamentos   → Lista com filtros, exportação CSV
/admin/clientes       → CRM simples: histórico, frequência, valor gasto
/admin/servicos       → CRUD de serviços com variações e upsell
/admin/equipe         → CRUD de profissionais/recursos com disponibilidade
/admin/financeiro     → Relatórios, repasses, extrato de transações
/admin/notificacoes   → Templates de mensagens WhatsApp/e-mail
/admin/configuracoes  → Identidade visual, domínio, horários, plano, integrações
```

### Painel Super Admin (plataforma)
```
/platform/tenants     → Listagem, status, plano, métricas globais
/platform/planos      → Gestão de planos e limites
/platform/financeiro  → Receita da plataforma, split por tenant
/platform/suporte     → Impersonate tenant, logs de erros globais
```

---

## Segurança

- **Isolamento de dados:** todos os queries filtrados por `tenant_id` via EF Core Global Query Filter
- **JWT com claims de tenant:** `tenant_id`, `tenant_schema`, `role` embutidos no token
- **Rate limiting** por tenant e por endpoint (ex: max 10 tentativas de login/min por IP)
- **CORS** configurado por domínio do tenant
- **LGPD:** consentimento de uso de dados no primeiro acesso, endpoint de exportação e exclusão de dados pessoais
- **Secrets:** armazenados em Azure Key Vault ou HashiCorp Vault (nunca em appsettings)
- **Validação de input:** FluentValidation em todos os Commands/Queries

---

## Testes Unitários — Cobertura Mínima Obrigatória

Implementar testes xUnit para as seguintes rotinas críticas:

```
1. Verificação de disponibilidade de horário (conflitos, bloqueios, intervalo entre atendimentos)
2. Cálculo de valor total do agendamento (múltiplos serviços, descontos, pacotes)
3. Geração de slots recorrentes (semanal/quinzenal — verificar salto de feriados)
4. Validação de política de cancelamento (prazo, taxa)
5. Resolução de tenant por domínio/subdomínio (middleware)
6. Publicação de eventos de domínio no Outbox Pattern
7. Aplicação de caução e multa por atraso (aluguel)
8. Controle de estoque em locações simultâneas (race condition)
9. Geração de link de pagamento e webhook de confirmação
10. Envio de notificações com retry em falha (consumer RabbitMQ)
```

Testes de integração com Testcontainers (PostgreSQL real):
```
- Criação de tenant com schema próprio e migração automática
- Fluxo completo de agendamento (API → DB → Event → Consumer)
- Isolamento de dados entre tenants diferentes na mesma instância
```

---

## Mensageria — Fluxo de Eventos

```
[API] AgendamentoCriado
        │
        ├─→ [Consumer: Notificação] → WhatsApp "Seu agendamento foi recebido!"
        ├─→ [Consumer: Email]       → Confirmação por e-mail com detalhes
        └─→ [Consumer: Audit]       → Grava em audit_logs

[API] PagamentoConfirmado (webhook Stripe/MP)
        │
        ├─→ [Consumer: Agendamento] → Atualiza status para "Confirmado"
        └─→ [Consumer: Notificação] → WhatsApp "Pagamento confirmado! Te esperamos em {{data_hora}}."

[Job: Lembrete - D-1 e H-2]
        └─→ [Consumer: Notificação] → WhatsApp lembrete com opção de cancelamento via link
```

---

## Integrações Externas

### WhatsApp (Evolution API ou Twilio)
```csharp
// Interface a ser implementada
public interface IWhatsAppService
{
    Task SendTextAsync(string phone, string message, CancellationToken ct);
    Task SendTemplateAsync(string phone, string templateName, object variables, CancellationToken ct);
    Task SendButtonsAsync(string phone, string body, IEnumerable<WhatsAppButton> buttons, CancellationToken ct);
}
```

### Google OAuth2 / Apple Sign-In
- Fluxo PKCE no frontend
- Backend valida o token ID e cria/sincroniza o usuário local
- Claims mapeados: `sub` → `external_id`, `email`, `name`, `picture`

### Pagamento (abstraído)
```csharp
public interface IPaymentGateway
{
    Task<PaymentSession> CreateCheckoutSessionAsync(CreatePaymentRequest request, CancellationToken ct);
    Task<PaymentStatus> GetPaymentStatusAsync(string paymentId, CancellationToken ct);
    Task<RefundResult> RefundAsync(string paymentId, decimal amount, CancellationToken ct);
}
// Implementações: StripePaymentGateway, MercadoPagoPaymentGateway
```

---

## Sugestões de Upsell Automático

Lógica no serviço de catálogo para sugerir serviços complementares:

```
Barbearia:      cliente agendou "Corte" → sugerir "Barba" (combo com desconto)
Quadra:         cliente alugou quadra → sugerir "Churrasco + Churrasqueiro"
Salão de festa: cliente reservou espaço → sugerir "Fotógrafo" ou "DJ"
Consultório:    cliente agendou "Limpeza de Pele" → sugerir "Hidratação Facial"
Aluguel:        cliente alugou furadeira → sugerir "Kit Brocas" ou "Extensão"
```

Regras configuráveis por tenant no painel admin (serviços que sugere outros serviços).

---

## Onboarding do Tenant

Wizard de 5 passos ao criar conta:
```
1. Tipo de negócio       → Selecionar vertical (barbearia, quadra, consultório, etc.)
2. Informações básicas   → Nome, endereço, telefone, horários
3. Identidade visual     → Logo, cores, banner
4. Serviços iniciais     → Cadastrar ao menos 1 serviço (template por vertical)
5. Domínio/subdomínio    → Escolher slug ou configurar domínio próprio
```

Ao finalizar: tenant já está operacional com página pública funcional.

---

## Perguntas para Refinamento

Antes de iniciar a implementação, responda:

1. **Plataforma preferida para frontend:** React (Next.js) ou Vue (Nuxt)?
2. **Gateway de pagamento principal:** Stripe (global) ou Mercado Pago (Brasil)?
3. **Provider WhatsApp:** Evolution API (self-hosted, gratuito) ou Twilio (gerenciado, pago)?
4. **Infraestrutura alvo:** On-premise / VPS, ou cloud (AWS/Azure/GCP)?
5. **Modelo de cobrança da plataforma:** Mensalidade por plano, % por transação, ou ambos?
6. **Módulo fiscal:** Emissão de NF-e / NFS-e está no escopo do MVP?
7. **Aplicativo móvel nativo** está no escopo (React Native / Flutter) ou apenas PWA?
8. **Idioma principal:** Português (pt-BR) apenas, ou internacionalização (i18n) desde o início?

---

## Ordem de Implementação Sugerida (MVP)

```
Sprint 1:  Infraestrutura base — projeto .NET, EF Core, multi-tenancy, migrations, Docker Compose
Sprint 2:  Autenticação — Google/Apple OAuth, JWT, claims de tenant
Sprint 3:  Módulo Tenant — CRUD, configuração visual, resolução de domínio
Sprint 4:  Catálogo — Serviços, recursos/atendentes, disponibilidade, horários
Sprint 5:  Agendamento — Verificação de slots, criação, confirmação, cancelamento
Sprint 6:  Pagamento — Integração Stripe/MP, webhooks, status
Sprint 7:  Mensageria — RabbitMQ, consumers, notificações WhatsApp
Sprint 8:  Frontend Portal Cliente — Home, catálogo, wizard de agendamento
Sprint 9:  Frontend Painel Admin — Agenda, serviços, equipe, financeiro
Sprint 10: Logs, auditoria, observabilidade, testes de carga
Sprint 11: Onboarding wizard, polimento UX, PWA
Sprint 12: Super Admin, gestão de planos, go-live
```

---

*Prompt gerado em 2026-06-02 | Contexto: SaaS de Agendamento Multi-Tenant — Valorem*
