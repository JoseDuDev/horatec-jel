# Horafy — Status do Projeto

> Última atualização: 2026-06-23

---

## O que está implementado (completo)

| Sprint | Área | Status |
|--------|------|--------|
| 1–3 | Infraestrutura, Auth, Multi-tenancy, Tenants | ✅ |
| 4 | Recursos e Disponibilidade | ✅ |
| 5 | Agendamentos (CRUD, recorrência, waitlist) | ✅ |
| 6 | Pagamentos (Mercado Pago, webhook, reembolso) | ✅ |
| 7 | Notificações (RabbitMQ, WhatsApp, email, templates) | ✅ |
| 8 | Clientes (perfil, telefone, histórico) | ✅ |
| 9 | Admin Panel (11 páginas) | ✅ |
| 10 | Portal do Cliente (6 páginas + booking wizard) | ✅ |
| 11 | PWA, Onboarding Wizard (5 steps), Reviews, Upsell | ✅ |
| 12 | Super Admin (platform/tenants, planos, financeiro, CI/CD) | ✅ |
| 13 | Wallet + Vouchers | ✅ |
| 14 | Checkout com Wallet + Voucher | ✅ |
| 15 | Fidelidade + Cancelamento self-service | ✅ |
| — | **Trigger de Onboarding** (redirect no primeiro login) | ✅ |
| — | **UI vinculação Serviço ↔ Recurso** (multiselect + endpoints add/remove) | ✅ |
| — | **E2E Playwright** (fluxos críticos via API + UI) | ✅ |
| — | **UI: Selects exibem rótulo** (mapeamento valor→rótulo; antes mostravam o valor cru/GUID) | ✅ |
| Locação 0–6 | **Módulo de Locação** (item alugável, estoque, diária, caução, ciclo de vida, financeiro, notificações) | ✅ |
| Planos 0–4 | **Capacidades + Limites por plano** (módulos por tenant + quotas editáveis; pacotes vendáveis) | ✅ |

**Módulo de Locação (`docs/rental-plan.md`) — concluído (Fases 0–6):**

- Item alugável (`RentableItem`) com estoque, diária, caução e buffer.
- Disponibilidade por intervalo de dias contra estoque (`GetRentalAvailabilityQuery`).
- Reserva ponta a ponta (`CreateRentalBookingCommand`) reusando pagamento/carteira/voucher.
- Ciclo de vida: retirada/devolução (`RentalLifecycle`), multa por atraso, **estoque
  reservado atomicamente sob isolamento Serializable** (anti-overbooking).
- Estorno da caução na **carteira** (padrão, botão "Devolver") **ou no gateway** — estorno
  parcial via `IPaymentGateway.RefundAsync`, com fallback p/ carteira; opt-in via
  `?refundToGateway=true` na API e botão **"Estornar no cartão"** na tabela de agendamentos.
- Financeiro de locação, lembrete de devolução (D-1) e aviso de atraso (Quartz).
- E2E: alugar → pagar → retirar → devolver → caução estornada (carteira **e** gateway).

> Pendência menor não bloqueante do módulo: o estado `Overdue` é **computado**
> (`Booking.IsOverdue`), não persistido como estágio do `RentalLifecycle` — suficiente para
> notificações; só virar estado persistido se relatórios precisarem filtrar atrasados.

**Capacidades + Limites por plano (`docs/superpowers/plans/2026-06-23-tenant-capacidades-limites.md`) — concluído (Fases 0–4):**

- **Capacidades** por tenant (`TenantCapability` `[Flags]`: Agendamento e/ou Aluguel) — a
  plataforma define o pacote contratado.
- **Limites por plano** (`PlanLimits` default em código + `PlanConfiguration` **editável por
  tabela**, que sobrepõe os defaults): máx. serviços, recursos e itens de locação.
- **Enforcement** na criação de serviço/recurso/item: bloqueia por capacidade ausente ou quota
  atingida (`IPlanLimitsService` + `PlanErrors`). Sem tenant resolvido → não bloqueia.
- **Super Admin** (`/platform`): define capacidades + plano por tenant
  (`PUT /platform/tenants/{id}/plan`) e edita os limites de cada plano (`GET/PUT /platform/plans`).
- **Painel do tenant**: menu esconde o módulo não contratado; aba *Plano* mostra uso vs. limite
  (`GET /tenants/me/usage`); onboarding adaptado ao pacote (pula serviço/recurso/horários se
  não houver agendamento).
- Default permissivo (ambos os módulos + plano `Free`) + migrações `AddTenantCapabilities`
  (coluna `capabilities` default ambos) e `AddPlanConfigurations` preservam tenants existentes.

**Testes:** 347 backend (0 falhas) · 48 frontend (0 falhas) · 12 E2E Playwright (0 falhas)

> Teste pré-existente flaky (alheio às features acima): `GetAvailableSlotsQueryHandlerTests`
> `.Handle_TodayPastSlots_AreExcluded` depende da hora do dia (UTC) — falha fora do horário
> comercial por não haver slots futuros "hoje". Estabilizar injetando o relógio.

---

## Próximos passos — em ordem de prioridade

### 1. 🔴 Deploy em produção (alta prioridade)

**O que existe:** CI/CD completo (`.github/workflows/ci.yml`), `docker-compose.prod.yml`, `Caddyfile`, `.env.prod.example`.

**O que falta fazer:**

#### Passo a passo:

1. **Secrets no GitHub** — ir em `Settings > Secrets and variables > Actions` e criar:
   - `GHCR_TOKEN` — Personal Access Token com `write:packages`
   - `JWT_SECRET` — string aleatória: `openssl rand -base64 32`
   - `DB_PASSWORD` — senha forte para PostgreSQL
   - `REDIS_PASSWORD` — senha forte para Redis
   - `RABBITMQ_PASSWORD` — senha forte para RabbitMQ
   - `MERCADOPAGO_ACCESS_TOKEN` — token de produção do Mercado Pago
   - `GOOGLE_CLIENT_ID` — OAuth client ID do Google
   - `EVOLUTION_API_KEY` — chave da Evolution API (WhatsApp)
   - `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASS` — credenciais de email
   - `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` — acesso ao servidor (se deploy automático)

2. **Servidor (VPS):**
   - Instalar Docker + Docker Compose
   - Criar arquivo `.env.prod` baseado em `.env.prod.example`
   - Abrir portas 80 e 443 no firewall

3. **DNS:**
   - Apontar domínio principal (ex: `horafy.com.br`) para o IP do servidor
   - Apontar `*.horafy.com.br` (wildcard) para subdomínios de tenants

4. **Primeira execução:**
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   dotnet ef database update --project src/Horafy.Infrastructure --startup-project src/Horafy.API
   ```

5. **Verificar:** O `smoke.test.ts` em `frontend/__tests__/smoke.test.ts` tem testes para validar endpoints após o deploy. Rodar com `SMOKE_API_URL=https://horafy.com.br npx vitest run __tests__/smoke.test.ts`

---

### 3. 🟢 Testes E2E (suíte Playwright — 12 testes, 0 falhas)

**Estado:** suíte em `frontend/e2e/` (helpers de API + storage state de admin/cliente),
rodando contra a stack do `docker-compose.e2e.yml` (sobe/derruba via global setup/teardown).
Cada spec cria seu próprio tenant; rodam em série.

Specs:
- `onboarding.spec.ts` — criação de conta → wizard de onboarding
- `booking.spec.ts` — cliente agenda pelo portal
- `admin-workflow.spec.ts` — admin confirma → cliente cancela → financeiro
- `loyalty.spec.ts` — fidelidade credita a carteira
- `rental.spec.ts` — locação: alugar → pagar → retirar → devolver → caução na carteira
- `rental-gateway-refund.spec.ts` — locação com estorno da caução no cartão (gateway)
- `resources.spec.ts` — criação de recurso escolhendo o Tipo pelo Select

Backend: teste de concorrência de estoque em
`tests/Horafy.Application.Tests/Rentals/RentalStockConcurrencyTests.cs` (Testcontainers
Postgres — **requer Docker**).

```bash
cd frontend && npx playwright test          # sobe a stack e roda a suíte
```

---

### 4. 🟢 Cobertura de testes frontend (baixa prioridade)

**O problema:** 22 arquivos de teste, 46 testes — média de ~2 por arquivo. A maioria cobre apenas render e validação básica.

**Componentes sem testes suficientes:**
- `BookingWizard.tsx` — apenas 2 testes superficiais
- `WizardStepConfirm.tsx` — sem testes
- `OnboardingStepTheme/Service/Resource/Hours` — sem testes (apenas Tenant tem)
- Páginas admin (agenda, clientes, financeiro) — sem testes

---

## Referências rápidas

### Rodar o projeto localmente
```bash
# Backend (requer Docker rodando)
docker compose up -d          # PostgreSQL, Redis, RabbitMQ, Seq
dotnet run --project src/Horafy.API

# Frontend
cd frontend && npm run dev
```

### Rodar os testes
```bash
# Backend
dotnet test

# Frontend
cd frontend && npx vitest run
```

### Gerar migration
```bash
dotnet ef migrations add <NomeDaMigration> \
  --project src/Horafy.Infrastructure \
  --startup-project src/Horafy.API \
  --context HorafyDbContext
```

### Arquitetura resumida
- **Backend:** .NET 8, Clean Architecture, CQRS (MediatR), EF Core + PostgreSQL
- **Frontend:** Next.js 16 (App Router), React 19, shadcn/ui, TanStack Query
- **Multi-tenancy:** schema isolado por tenant (`tenant_{slug}`) no PostgreSQL
- **Auth admin:** JWT em cookie `access_token` + `tenant_slug`
- **Auth portal:** cookie `access_token_customer` via Google/Apple OAuth
- **Auth platform:** cookie `platform_access_token` para PlatformAdmin

### Planos de implementação salvos
```
docs/superpowers/plans/
├── 2026-06-06-sprint11-pwa-onboarding.md
├── 2026-06-08-onboarding-trigger.md   ← último plano executado
└── ...
```
