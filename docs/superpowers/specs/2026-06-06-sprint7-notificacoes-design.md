# Sprint 7 — Notificações & Mensageria: Design Spec

## Objetivo

Adicionar o módulo de mensageria ao Horafy: RabbitMQ + MassTransit para entrega assíncrona confiável, WhatsApp via Evolution API (MVP), e-mail via SMTP (MailKit), templates configuráveis por tenant e lembretes automáticos D-1/H-2 via Quartz.

---

## Decisões Arquiteturais

| Decisão | Escolha | Motivo |
|---|---|---|
| Bus de mensagens | MassTransit + RabbitMQ | Abstrai o broker; retry/dead-letter nativos |
| WhatsApp MVP | Evolution API (self-hosted) | Zero custo no dev; `IWhatsAppService` isola a troca para Twilio |
| E-mail | MailKit via `IEmailService` | Substitui `SmtpClient` legado; suporte a TLS/OAuth |
| Templates | Default hardcoded + override por tenant na DB | Admin panel vem no Sprint 9; infra pronta agora |
| Lembretes | MassTransit Quartz (job a cada hora) | Já no ecossistema MassTransit; sem dependência extra |
| Outbox existente | Processar `public.outbox_messages` via `OutboxProcessorService` | Tabela já existe no HorafyDbContext; events globais (TenantCreated, UserCreated) passam pelo outbox |
| Tenant events | MediatR handlers → `IPublishEndpoint` | TenantDbContext já publica eventos via MediatR; basta adicionar handlers de notificação |

### Fluxo principal

```
Booking.Create() / Payment.Approve()
  → RaiseDomainEvent(BookingCreatedEvent / PaymentConfirmedEvent)
  → TenantDbContext.SaveChangesAsync() → IPublisher.Publish()
    → (existing) PaymentConfirmedEventHandler (atualiza booking status)
    → (NEW) BookingCreatedNotificationPublisher → IPublishEndpoint → RabbitMQ
                                                                    → BookingCreatedConsumer
                                                                        → IWhatsAppService.SendTextAsync()
                                                                        → IEmailService.SendAsync()
```

### Fluxo de lembretes

```
Quartz cron (a cada hora)
  → BookingReminderJob
    → consulta bookings 22-26h à frente (D-1) e 1-3h à frente (H-2)
    → IBus.Publish(BookingReminderMessage) por booking
      → BookingReminderConsumer → WhatsApp + e-mail
```

---

## Enums de domínio

```csharp
// src/Horafy.Domain/Entities/Notifications/NotificationEventType.cs
public enum NotificationEventType
{
    BookingCreated   = 0,
    BookingConfirmed = 1,
    BookingCancelled = 2,
    BookingReminder  = 3,
    PaymentPending   = 4,
    PaymentConfirmed = 5
}

// src/Horafy.Domain/Entities/Notifications/NotificationChannel.cs
public enum NotificationChannel { WhatsApp = 0, Email = 1 }
```

---

## NotificationTemplate — entidade tenant

```csharp
// src/Horafy.Domain/Entities/Notifications/NotificationTemplate.cs
public sealed class NotificationTemplate : BaseEntity
{
    public NotificationEventType EventType       { get; private set; }
    public NotificationChannel   Channel         { get; private set; }
    public string?               SubjectTemplate { get; private set; }  // null para WhatsApp
    public string                BodyTemplate    { get; private set; }
    public bool                  IsActive        { get; private set; } = true;

    public static NotificationTemplate Create(
        NotificationEventType eventType, NotificationChannel channel,
        string bodyTemplate, string? subjectTemplate = null) => new() { ... };

    public void Update(string? subjectTemplate, string bodyTemplate) { ... }
    public void Deactivate() { ... }
    public void Activate()   { ... }
}
```

DDL: tabela `notification_templates` no schema `tenant_{slug}`, com índice único em `(event_type, channel)` para `is_active = TRUE`.

---

## Contratos de mensagem (RabbitMQ)

Todos em `Horafy.Application.Features.Notifications.Messages`. São records simples com todos os dados necessários para renderizar o template **sem fazer queries adicionais** no consumer.

| Contrato | Campos-chave |
|---|---|
| `BookingCreatedMessage` | BookingId, CustomerName, CustomerEmail, CustomerPhone, ServiceName, ResourceName, ScheduledAt, TenantSlug, TenantName |
| `BookingConfirmedMessage` | idem |
| `BookingCancelledMessage` | BookingId, CustomerName, CustomerEmail, CustomerPhone, Reason, TenantSlug, TenantName |
| `BookingReminderMessage` | idem BookingCreated + `IsOneDayBefore` (true=D-1, false=H-2) |
| `PaymentPendingMessage` | PaymentId, BookingId, CustomerName, CustomerEmail, CustomerPhone, PaymentUrl, Amount, TenantSlug, TenantName |
| `PaymentConfirmedMessage` | PaymentId, BookingId, CustomerName, CustomerEmail, CustomerPhone, Amount, TenantSlug, TenantName |

---

## Publishers MediatR (Application layer)

Cada publisher é um `INotificationHandler<TDomainEvent>` que enriquece os dados e chama `IPublishEndpoint.Publish(message)`.

| Publisher | Domain Event | Dados extras buscados |
|---|---|---|
| `BookingCreatedNotificationPublisher` | `BookingCreatedEvent` | ServiceName (booking.Services.First()), ResourceName (IResourceRepository), CustomerPhone (IUserRepository), TenantName (ICurrentTenantService) |
| `BookingConfirmedNotificationPublisher` | `BookingConfirmedEvent` | idem |
| `BookingCancelledNotificationPublisher` | `BookingCancelledEvent` | idem |
| `PaymentCreatedNotificationPublisher` | `PaymentCreatedEvent` | dados do booking + tenant |
| `PaymentConfirmedNotificationPublisher` | `PaymentConfirmedEvent` | dados do booking + tenant |

---

## Consumers MassTransit (Infrastructure layer)

Cada consumer recebe a mensagem, renderiza o template (default ou tenant) e chama `IWhatsAppService` + `IEmailService`.

Para Sprint 7: **usa templates default hardcoded**. Quando o admin panel for construído (Sprint 9), o consumer poderá buscar o template customizado do tenant via `INotificationTemplateRepository`.

Retry policy: 3 tentativas, intervalo exponencial (5s, 25s, 125s). Após 3 falhas → dead-letter queue.

---

## OutboxProcessorService

`BackgroundService` que a cada 5 segundos:
1. Busca até 20 registros de `public.outbox_messages` onde `processed_at IS NULL` e `retry_count < 3`
2. Desserializa o tipo do domain event via `Type.GetType(message.Type)`
3. Publica no bus via `IBus.Publish(event, eventType)`
4. Marca `processed_at = now`
5. Em caso de exceção: incrementa `retry_count`, define `error = ex.Message`
6. Após `retry_count >= 3`: marca `error` permanente (não processa mais)

---

## BookingReminderJob

`IJob` (Quartz), configurado via MassTransit com cron `0 0 * * * ?` (todo início de hora):

1. Para cada tenant ativo: abre conexão com o tenant schema
2. Busca bookings onde:
   - Status = `Confirmed`
   - `ScheduledAt` entre `now + 22h` e `now + 26h` → `IsOneDayBefore = true`
   - `ScheduledAt` entre `now + 1h` e `now + 3h` → `IsOneDayBefore = false`
3. Para cada booking encontrado: `IBus.Publish(BookingReminderMessage)`

---

## appsettings — novas seções

```json
"RabbitMq": {
  "Host": "localhost",
  "VirtualHost": "/",
  "Username": "guest",
  "Password": "guest"
},
"EvolutionApi": {
  "BaseUrl": "http://localhost:8080",
  "ApiKey": "",
  "InstanceName": "horafy"
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
```

---

## API — NotificationTemplatesController

```
GET    /api/v1/notification-templates              → GetNotificationTemplatesQuery
POST   /api/v1/notification-templates              → UpsertNotificationTemplateCommand (cria ou atualiza)
DELETE /api/v1/notification-templates/{id}         → deactivate
```

Todos com `[Authorize(Roles = "TenantOwner,TenantAdmin")]`.

---

## Testes

| Classe de teste | Cenários |
|---|---|
| `TemplateRendererTests` | substitui variáveis, variável ausente mantém placeholder, template vazio |
| `EvolutionApiWhatsAppServiceTests` | POST correto, erro HTTP lança exceção |
| `SmtpEmailServiceTests` | construção sem throw, opções default |
| `NotificationTemplateTests` | Create, Update, Deactivate |
| `BookingCreatedNotificationPublisherTests` | publica mensagem com dados corretos, booking não encontrado → silencioso |
| `ConsumerTests` (BookingCreated, PaymentPending) | consumer chama WhatsApp e e-mail, falha de gateway → lança para retry |
| `OutboxProcessorServiceTests` | processa mensagem, marca processed_at, incrementa retry em falha, para após 3 tentativas |
| `BookingReminderJobTests` | encontra bookings D-1 e H-2, publica mensagens corretas, sem bookings → sem publicação |
| `UpsertNotificationTemplateCommandTests` | cria novo template, atualiza existente, validação de campos obrigatórios |

---

## Fora do Escopo deste Sprint

- UI de configuração de templates (Sprint 9 — admin panel)
- Twilio WhatsApp Business API (migração pré-go-live)
- Notificações para o owner do tenant (apenas cliente final neste sprint)
- Avaliações (Sprint 8)
- Integrações de e-mail transacional (SendGrid, AWS SES) — apenas SMTP neste sprint
