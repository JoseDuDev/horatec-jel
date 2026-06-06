# Sprint 7 — Notificações & Mensageria Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar mensageria assíncrona via RabbitMQ + MassTransit, WhatsApp (Evolution API), e-mail (MailKit/SMTP), templates por tenant, lembretes automáticos D-1/H-2 e processador do outbox existente.

**Architecture:** Domain events existentes → MediatR notification publishers (Application) → `IPublishEndpoint` → RabbitMQ → MassTransit consumers (Infrastructure) → `IWhatsAppService` + `IEmailService`. Lembretes via Quartz job que publica `BookingReminderMessage`. Outbox de eventos globais processado por `OutboxProcessorService`. Templates default hardcoded; tabela tenant para customização futura (admin panel Sprint 9).

**Tech Stack:** .NET 8, MassTransit 8.4.x, MassTransit.RabbitMQ 8.4.x, MassTransit.Quartz 8.4.x, Quartz 3.14.x, MailKit 4.12.x, xUnit, Moq, FluentAssertions

---

## File Map

### Criar
- `src/Horafy.Domain/Events/Bookings/BookingConfirmedEvent.cs`
- `src/Horafy.Domain/Entities/Notifications/NotificationEventType.cs`
- `src/Horafy.Domain/Entities/Notifications/NotificationChannel.cs`
- `src/Horafy.Domain/Entities/Notifications/NotificationTemplate.cs`
- `src/Horafy.Domain/Interfaces/Repositories/INotificationTemplateRepository.cs`
- `src/Horafy.Application/Interfaces/IWhatsAppService.cs`
- `src/Horafy.Application/Interfaces/IEmailService.cs`
- `src/Horafy.Application/Features/Notifications/Messages/BookingCreatedMessage.cs`
- `src/Horafy.Application/Features/Notifications/Messages/BookingConfirmedMessage.cs`
- `src/Horafy.Application/Features/Notifications/Messages/BookingCancelledMessage.cs`
- `src/Horafy.Application/Features/Notifications/Messages/BookingReminderMessage.cs`
- `src/Horafy.Application/Features/Notifications/Messages/PaymentPendingMessage.cs`
- `src/Horafy.Application/Features/Notifications/Messages/PaymentConfirmedMessage.cs`
- `src/Horafy.Application/Features/Notifications/DefaultTemplates.cs`
- `src/Horafy.Application/Features/Notifications/TemplateRenderer.cs`
- `src/Horafy.Application/Features/Notifications/Publishers/BookingCreatedNotificationPublisher.cs`
- `src/Horafy.Application/Features/Notifications/Publishers/BookingConfirmedNotificationPublisher.cs`
- `src/Horafy.Application/Features/Notifications/Publishers/BookingCancelledNotificationPublisher.cs`
- `src/Horafy.Application/Features/Notifications/Publishers/PaymentCreatedNotificationPublisher.cs`
- `src/Horafy.Application/Features/Notifications/Publishers/PaymentConfirmedNotificationPublisher.cs`
- `src/Horafy.Application/Features/Notifications/Commands/UpsertNotificationTemplateCommand.cs`
- `src/Horafy.Application/Features/Notifications/Queries/GetNotificationTemplatesQuery.cs`
- `src/Horafy.Infrastructure/Gateways/EvolutionApiOptions.cs`
- `src/Horafy.Infrastructure/Gateways/EvolutionApiWhatsAppService.cs`
- `src/Horafy.Infrastructure/Email/SmtpOptions.cs`
- `src/Horafy.Infrastructure/Email/SmtpEmailService.cs`
- `src/Horafy.Infrastructure/Messaging/RabbitMqOptions.cs`
- `src/Horafy.Infrastructure/Messaging/Consumers/BookingCreatedConsumer.cs`
- `src/Horafy.Infrastructure/Messaging/Consumers/BookingConfirmedConsumer.cs`
- `src/Horafy.Infrastructure/Messaging/Consumers/BookingCancelledConsumer.cs`
- `src/Horafy.Infrastructure/Messaging/Consumers/PaymentPendingConsumer.cs`
- `src/Horafy.Infrastructure/Messaging/Consumers/PaymentConfirmedConsumer.cs`
- `src/Horafy.Infrastructure/Messaging/Consumers/BookingReminderConsumer.cs`
- `src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs`
- `src/Horafy.Infrastructure/Messaging/OutboxProcessorService.cs`
- `src/Horafy.Infrastructure/Repositories/NotificationTemplateRepository.cs`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/NotificationTemplateEntityConfiguration.cs`
- `src/Horafy.API/Controllers/V1/NotificationTemplatesController.cs`
- `tests/Horafy.Application.Tests/Notifications/TemplateRendererTests.cs`
- `tests/Horafy.Application.Tests/Notifications/EvolutionApiWhatsAppServiceTests.cs`
- `tests/Horafy.Application.Tests/Notifications/SmtpEmailServiceTests.cs`
- `tests/Horafy.Domain.Tests/Entities/NotificationTemplateTests.cs`
- `tests/Horafy.Application.Tests/Notifications/BookingCreatedNotificationPublisherTests.cs`
- `tests/Horafy.Application.Tests/Notifications/BookingCreatedConsumerTests.cs`
- `tests/Horafy.Application.Tests/Notifications/OutboxProcessorServiceTests.cs`
- `tests/Horafy.Application.Tests/Notifications/BookingReminderJobTests.cs`
- `tests/Horafy.Application.Tests/Notifications/UpsertNotificationTemplateCommandTests.cs`

### Modificar
- `src/Horafy.Domain/Entities/Bookings/Booking.cs` — `Confirm()` raises `BookingConfirmedEvent`
- `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs` — add `DbSet<NotificationTemplate>`
- `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs` — add `notification_templates` DDL
- `src/Horafy.Infrastructure/DependencyInjection.cs` — register MassTransit, Evolution, SMTP, repos
- `src/Horafy.API/appsettings.json` — add RabbitMq, EvolutionApi, Smtp sections
- `src/Horafy.API/appsettings.Development.json` — add development values

---

## Task 1: BookingConfirmedEvent + NuGet packages

**Files:**
- Create: `src/Horafy.Domain/Events/Bookings/BookingConfirmedEvent.cs`
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`
- Modify: `src/Horafy.Infrastructure/Horafy.Infrastructure.csproj`
- Modify: `src/Horafy.Application/Horafy.Application.csproj`

- [ ] **Step 1: Add NuGet packages**

```
dotnet add src/Horafy.Infrastructure package MassTransit --version 8.4.1
dotnet add src/Horafy.Infrastructure package MassTransit.RabbitMQ --version 8.4.1
dotnet add src/Horafy.Infrastructure package MassTransit.Quartz --version 8.4.1
dotnet add src/Horafy.Infrastructure package Quartz --version 3.14.0
dotnet add src/Horafy.Infrastructure package MailKit --version 4.12.0
dotnet add src/Horafy.Application package MassTransit --version 8.4.1
```

- [ ] **Step 2: Write failing test**

```csharp
// tests/Horafy.Domain.Tests/Entities/BookingConfirmedEventTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Events.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public sealed class BookingConfirmedEventTests
{
    [Fact]
    public void Confirm_RaisesBookingConfirmedEvent()
    {
        var booking = Booking.Create(
            new[] { (Service.Create("Corte", 60, 100m).Id, "Corte", 60) },
            Resource.Create("João", ResourceType.Professional).Id,
            Guid.NewGuid(), "Cliente", "cliente@test.com",
            DateTimeOffset.UtcNow.AddHours(2));

        booking.Confirm();

        var evt = booking.DomainEvents.OfType<BookingConfirmedEvent>().Single();
        evt.BookingId.Should().Be(booking.Id);
        evt.CustomerEmail.Should().Be("cliente@test.com");
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```
dotnet test tests/Horafy.Domain.Tests --filter "BookingConfirmedEventTests" 2>&1 | tail -5
```
Expected: compile error — `BookingConfirmedEvent` not found.

- [ ] **Step 4: Create BookingConfirmedEvent**

```csharp
// src/Horafy.Domain/Events/Bookings/BookingConfirmedEvent.cs
using Horafy.Domain.Events.Base;

namespace Horafy.Domain.Events.Bookings;

public sealed record BookingConfirmedEvent(
    Guid BookingId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset ScheduledAt) : DomainEvent;
```

- [ ] **Step 5: Modify Booking.Confirm() to raise the event**

In `src/Horafy.Domain/Entities/Bookings/Booking.cs`, replace the entire `Confirm()` method:

```csharp
    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Não é possível confirmar um agendamento no status {Status}.");

        Status      = BookingStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt   = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new BookingConfirmedEvent(Id, CustomerId, CustomerName, CustomerEmail, ScheduledAt));
    }
```

- [ ] **Step 6: Run tests**

```
dotnet test Horafy.sln 2>&1 | tail -8
```
Expected: all tests pass (the new test + 143 existing).

- [ ] **Step 7: Commit**

```
git add src/Horafy.Domain/Events/Bookings/BookingConfirmedEvent.cs
git add src/Horafy.Domain/Entities/Bookings/Booking.cs
git add tests/Horafy.Domain.Tests/Entities/BookingConfirmedEventTests.cs
git add src/Horafy.Infrastructure/Horafy.Infrastructure.csproj
git add src/Horafy.Application/Horafy.Application.csproj
git commit -m "feat: add BookingConfirmedEvent and Sprint 7 NuGet packages"
```

---

## Task 2: IWhatsAppService + EvolutionApiWhatsAppService

**Files:**
- Create: `src/Horafy.Application/Interfaces/IWhatsAppService.cs`
- Create: `src/Horafy.Infrastructure/Gateways/EvolutionApiOptions.cs`
- Create: `src/Horafy.Infrastructure/Gateways/EvolutionApiWhatsAppService.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/EvolutionApiWhatsAppServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/EvolutionApiWhatsAppServiceTests.cs
using System.Net;
using FluentAssertions;
using Horafy.Infrastructure.Gateways;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class EvolutionApiWhatsAppServiceTests
{
    private static EvolutionApiWhatsAppService MakeService(HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(status);
        var client  = new HttpClient(handler) { BaseAddress = new Uri("http://evo.local") };
        var opts    = Options.Create(new EvolutionApiOptions
        {
            BaseUrl = "http://evo.local", ApiKey = "key", InstanceName = "horafy"
        });
        return new EvolutionApiWhatsAppService(client, opts,
            NullLogger<EvolutionApiWhatsAppService>.Instance);
    }

    [Fact]
    public async Task SendTextAsync_SuccessResponse_DoesNotThrow()
    {
        var svc = MakeService(HttpStatusCode.OK);
        var act = () => svc.SendTextAsync("5511999999999", "Olá!", default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendTextAsync_ErrorResponse_ThrowsHttpRequestException()
    {
        var svc = MakeService(HttpStatusCode.InternalServerError);
        var act = () => svc.SendTextAsync("5511999999999", "Olá!", default);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"key":{"id":"abc"}}""")
            });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "EvolutionApiWhatsAppServiceTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create IWhatsAppService**

```csharp
// src/Horafy.Application/Interfaces/IWhatsAppService.cs
namespace Horafy.Application.Interfaces;

public interface IWhatsAppService
{
    Task SendTextAsync(string phoneNumber, string message, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create EvolutionApiOptions**

```csharp
// src/Horafy.Infrastructure/Gateways/EvolutionApiOptions.cs
namespace Horafy.Infrastructure.Gateways;

public sealed class EvolutionApiOptions
{
    public const string SectionName = "EvolutionApi";
    public string BaseUrl      { get; set; } = string.Empty;
    public string ApiKey       { get; set; } = string.Empty;
    public string InstanceName { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Create EvolutionApiWhatsAppService**

```csharp
// src/Horafy.Infrastructure/Gateways/EvolutionApiWhatsAppService.cs
using System.Net.Http.Json;
using Horafy.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Horafy.Infrastructure.Gateways;

internal sealed class EvolutionApiWhatsAppService(
    HttpClient httpClient,
    IOptions<EvolutionApiOptions> options,
    ILogger<EvolutionApiWhatsAppService> logger) : IWhatsAppService
{
    private readonly EvolutionApiOptions _opts = options.Value;

    public async Task SendTextAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var body = new { number = phoneNumber, text = message };
        logger.LogInformation("Enviando WhatsApp para {Phone}", phoneNumber);
        var response = await httpClient.PostAsJsonAsync(
            $"/message/sendText/{_opts.InstanceName}", body, ct);
        response.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 6: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "EvolutionApiWhatsAppServiceTests" 2>&1 | tail -5
```
Expected: 2 passed.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Interfaces/IWhatsAppService.cs
git add src/Horafy.Infrastructure/Gateways/EvolutionApiOptions.cs
git add src/Horafy.Infrastructure/Gateways/EvolutionApiWhatsAppService.cs
git add tests/Horafy.Application.Tests/Notifications/EvolutionApiWhatsAppServiceTests.cs
git commit -m "feat: add IWhatsAppService and EvolutionApiWhatsAppService"
```

---

## Task 3: IEmailService + SmtpEmailService

**Files:**
- Create: `src/Horafy.Application/Interfaces/IEmailService.cs`
- Create: `src/Horafy.Infrastructure/Email/SmtpOptions.cs`
- Create: `src/Horafy.Infrastructure/Email/SmtpEmailService.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/SmtpEmailServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/SmtpEmailServiceTests.cs
using FluentAssertions;
using Horafy.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var opts = Options.Create(new SmtpOptions
        {
            Host = "smtp.example.com", Port = 587,
            FromAddress = "no-reply@horafy.com.br", FromName = "Horafy"
        });
        var act = () => new SmtpEmailService(opts, NullLogger<SmtpEmailService>.Instance);
        act.Should().NotThrow();
    }

    [Fact]
    public void SmtpOptions_DefaultValues_AreCorrect()
    {
        var opts = new SmtpOptions();
        opts.Port.Should().Be(587);
        opts.FromName.Should().Be("Horafy");
        opts.UseSsl.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "SmtpEmailServiceTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create IEmailService**

```csharp
// src/Horafy.Application/Interfaces/IEmailService.cs
namespace Horafy.Application.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create SmtpOptions**

```csharp
// src/Horafy.Infrastructure/Email/SmtpOptions.cs
namespace Horafy.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string Host        { get; set; } = string.Empty;
    public int    Port        { get; set; } = 587;
    public string Username    { get; set; } = string.Empty;
    public string Password    { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName    { get; set; } = "Horafy";
    public bool   UseSsl      { get; set; } = true;
}
```

- [ ] **Step 5: Create SmtpEmailService**

```csharp
// src/Horafy.Infrastructure/Email/SmtpEmailService.cs
using Horafy.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Horafy.Infrastructure.Email;

internal sealed class SmtpEmailService(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body    = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        logger.LogInformation("Enviando e-mail para {To}: {Subject}", to, subject);

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _opts.Host, _opts.Port,
            _opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

        if (!string.IsNullOrEmpty(_opts.Username))
            await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);

        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
```

- [ ] **Step 6: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "SmtpEmailServiceTests" 2>&1 | tail -5
```
Expected: 2 passed.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Interfaces/IEmailService.cs
git add src/Horafy.Infrastructure/Email/
git add tests/Horafy.Application.Tests/Notifications/SmtpEmailServiceTests.cs
git commit -m "feat: add IEmailService and SmtpEmailService with MailKit"
```

---

## Task 4: Notification enums + message contracts + TemplateRenderer + DefaultTemplates

**Files:**
- Create: `src/Horafy.Domain/Entities/Notifications/NotificationEventType.cs`
- Create: `src/Horafy.Domain/Entities/Notifications/NotificationChannel.cs`
- Create: `src/Horafy.Application/Features/Notifications/Messages/` (6 arquivos)
- Create: `src/Horafy.Application/Features/Notifications/TemplateRenderer.cs`
- Create: `src/Horafy.Application/Features/Notifications/DefaultTemplates.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/TemplateRendererTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/TemplateRendererTests.cs
using FluentAssertions;
using Horafy.Application.Features.Notifications;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesAllVariables()
    {
        var template = "Olá, {{customer_name}}! Serviço: {{service_name}}.";
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = "João",
            ["service_name"]  = "Corte"
        };
        TemplateRenderer.Render(template, vars).Should().Be("Olá, João! Serviço: Corte.");
    }

    [Fact]
    public void Render_MissingVariable_LeavesPlaceholderIntact()
    {
        var template = "Olá, {{customer_name}}! Serviço: {{service_name}}.";
        var vars = new Dictionary<string, string> { ["customer_name"] = "Maria" };
        var result = TemplateRenderer.Render(template, vars);
        result.Should().Be("Olá, Maria! Serviço: {{service_name}}.");
    }

    [Fact]
    public void Render_EmptyTemplate_ReturnsEmpty()
    {
        TemplateRenderer.Render("", new Dictionary<string, string>()).Should().BeEmpty();
    }

    [Fact]
    public void FormatBrazilianDateTime_FormatsCorrectly()
    {
        var dt = new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.FromHours(-3));
        TemplateRenderer.FormatBrazilian(dt).Should().Be("15/06/2026 14:30");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "TemplateRendererTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create enums (Domain)**

```csharp
// src/Horafy.Domain/Entities/Notifications/NotificationEventType.cs
namespace Horafy.Domain.Entities.Notifications;

public enum NotificationEventType
{
    BookingCreated   = 0,
    BookingConfirmed = 1,
    BookingCancelled = 2,
    BookingReminder  = 3,
    PaymentPending   = 4,
    PaymentConfirmed = 5
}
```

```csharp
// src/Horafy.Domain/Entities/Notifications/NotificationChannel.cs
namespace Horafy.Domain.Entities.Notifications;

public enum NotificationChannel { WhatsApp = 0, Email = 1 }
```

- [ ] **Step 4: Create TemplateRenderer**

```csharp
// src/Horafy.Application/Features/Notifications/TemplateRenderer.cs
namespace Horafy.Application.Features.Notifications;

public static class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        return result;
    }

    public static string FormatBrazilian(DateTimeOffset dt) =>
        dt.ToString("dd/MM/yyyy HH:mm");
}
```

- [ ] **Step 5: Create message contracts**

```csharp
// src/Horafy.Application/Features/Notifications/Messages/BookingCreatedMessage.cs
namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingCreatedMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ServiceName,
    string ResourceName,
    DateTimeOffset ScheduledAt,
    string TenantSlug,
    string TenantName);
```

```csharp
// src/Horafy.Application/Features/Notifications/Messages/BookingConfirmedMessage.cs
namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingConfirmedMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ServiceName,
    string ResourceName,
    DateTimeOffset ScheduledAt,
    string TenantSlug,
    string TenantName);
```

```csharp
// src/Horafy.Application/Features/Notifications/Messages/BookingCancelledMessage.cs
namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingCancelledMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string? Reason,
    string TenantSlug,
    string TenantName);
```

```csharp
// src/Horafy.Application/Features/Notifications/Messages/BookingReminderMessage.cs
namespace Horafy.Application.Features.Notifications.Messages;

public sealed record BookingReminderMessage(
    Guid   BookingId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ServiceName,
    string ResourceName,
    DateTimeOffset ScheduledAt,
    string TenantSlug,
    string TenantName,
    bool   IsOneDayBefore);
```

```csharp
// src/Horafy.Application/Features/Notifications/Messages/PaymentPendingMessage.cs
namespace Horafy.Application.Features.Notifications.Messages;

public sealed record PaymentPendingMessage(
    Guid    PaymentId,
    Guid    BookingId,
    string  CustomerName,
    string  CustomerEmail,
    string? CustomerPhone,
    string? PaymentUrl,
    decimal Amount,
    string  TenantSlug,
    string  TenantName);
```

```csharp
// src/Horafy.Application/Features/Notifications/Messages/PaymentConfirmedMessage.cs
namespace Horafy.Application.Features.Notifications.Messages;

public sealed record PaymentConfirmedMessage(
    Guid    PaymentId,
    Guid    BookingId,
    string  CustomerName,
    string  CustomerEmail,
    string? CustomerPhone,
    decimal Amount,
    string  TenantSlug,
    string  TenantName);
```

- [ ] **Step 6: Create DefaultTemplates**

```csharp
// src/Horafy.Application/Features/Notifications/DefaultTemplates.cs
namespace Horafy.Application.Features.Notifications;

public static class DefaultTemplates
{
    public static class WhatsApp
    {
        public const string BookingCreated =
            "Olá, {{customer_name}}! 👋 Seu agendamento de *{{service_name}}* " +
            "com *{{resource_name}}* foi recebido para *{{scheduled_at}}*. " +
            "Em breve você receberá a confirmação. — {{tenant_name}}";

        public const string BookingConfirmed =
            "✅ Confirmado! Olá, {{customer_name}}. Seu horário de *{{service_name}}* " +
            "com *{{resource_name}}* está marcado para *{{scheduled_at}}*. " +
            "Te esperamos! — {{tenant_name}}";

        public const string BookingCancelled =
            "❌ Agendamento cancelado. Olá, {{customer_name}}. " +
            "{{cancellation_reason}}Entre em contato para reagendar. — {{tenant_name}}";

        public const string BookingReminderOneDay =
            "⏰ Lembrete! Olá, {{customer_name}}. Amanhã você tem *{{service_name}}* " +
            "com *{{resource_name}}* às *{{scheduled_time}}*. Te esperamos! — {{tenant_name}}";

        public const string BookingReminderTwoHours =
            "⏰ Daqui a 2 horas! Olá, {{customer_name}}. Seu agendamento de " +
            "*{{service_name}}* com *{{resource_name}}* é às *{{scheduled_time}}*. " +
            "— {{tenant_name}}";

        public const string PaymentPending =
            "💳 Link de pagamento. Olá, {{customer_name}}. Para confirmar seu agendamento, " +
            "pague *R$ {{amount}}* pelo link: {{payment_url}} — {{tenant_name}}";

        public const string PaymentConfirmed =
            "✅ Pagamento confirmado! Olá, {{customer_name}}. Recebemos seu pagamento " +
            "de *R$ {{amount}}*. Agendamento confirmado! — {{tenant_name}}";
    }

    public static class EmailSubject
    {
        public const string BookingCreated   = "Agendamento recebido — {{service_name}}";
        public const string BookingConfirmed = "Agendamento confirmado — {{service_name}}";
        public const string BookingCancelled = "Agendamento cancelado";
        public const string BookingReminder  = "Lembrete: {{service_name}} em {{scheduled_at}}";
        public const string PaymentPending   = "Link de pagamento — R$ {{amount}}";
        public const string PaymentConfirmed = "Pagamento confirmado — R$ {{amount}}";
    }

    public static class EmailBody
    {
        public const string BookingCreated =
            "<h2>Olá, {{customer_name}}!</h2>" +
            "<p>Seu agendamento de <strong>{{service_name}}</strong> com " +
            "<strong>{{resource_name}}</strong> foi recebido para " +
            "<strong>{{scheduled_at}}</strong>.</p><p>— {{tenant_name}}</p>";

        public const string BookingConfirmed =
            "<h2>✅ Agendamento confirmado!</h2>" +
            "<p>Olá, {{customer_name}}. Seu horário de <strong>{{service_name}}</strong> " +
            "com <strong>{{resource_name}}</strong> está confirmado para " +
            "<strong>{{scheduled_at}}</strong>.</p><p>— {{tenant_name}}</p>";

        public const string BookingCancelled =
            "<h2>❌ Agendamento cancelado</h2>" +
            "<p>Olá, {{customer_name}}. {{cancellation_reason}}" +
            "Entre em contato para reagendar.</p><p>— {{tenant_name}}</p>";

        public const string BookingReminder =
            "<h2>⏰ Lembrete!</h2><p>Olá, {{customer_name}}. Não esqueça do seu " +
            "agendamento de <strong>{{service_name}}</strong> com " +
            "<strong>{{resource_name}}</strong> em <strong>{{scheduled_at}}</strong>.</p>" +
            "<p>— {{tenant_name}}</p>";

        public const string PaymentPending =
            "<h2>💳 Pagamento pendente</h2><p>Olá, {{customer_name}}. Para confirmar " +
            "seu agendamento, efetue o pagamento de <strong>R$ {{amount}}</strong>:</p>" +
            "<p><a href=\"{{payment_url}}\">Pagar agora</a></p><p>— {{tenant_name}}</p>";

        public const string PaymentConfirmed =
            "<h2>✅ Pagamento confirmado!</h2><p>Olá, {{customer_name}}. Recebemos seu " +
            "pagamento de <strong>R$ {{amount}}</strong>. Agendamento confirmado!</p>" +
            "<p>— {{tenant_name}}</p>";
    }
}
```

- [ ] **Step 7: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "TemplateRendererTests" 2>&1 | tail -5
```
Expected: 4 passed.

- [ ] **Step 8: Commit**

```
git add src/Horafy.Domain/Entities/Notifications/
git add src/Horafy.Application/Features/Notifications/
git add tests/Horafy.Application.Tests/Notifications/TemplateRendererTests.cs
git commit -m "feat: add notification enums, message contracts, TemplateRenderer and DefaultTemplates"
```

---

## Task 5: NotificationTemplate entity + DDL + Repository + EF config

**Files:**
- Create: `src/Horafy.Domain/Entities/Notifications/NotificationTemplate.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/INotificationTemplateRepository.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/NotificationTemplateEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/NotificationTemplateRepository.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Create: `tests/Horafy.Domain.Tests/Entities/NotificationTemplateTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Domain.Tests/Entities/NotificationTemplateTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Notifications;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public sealed class NotificationTemplateTests
{
    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            bodyTemplate: "Olá, {{customer_name}}!");

        t.EventType.Should().Be(NotificationEventType.BookingCreated);
        t.Channel.Should().Be(NotificationChannel.WhatsApp);
        t.BodyTemplate.Should().Be("Olá, {{customer_name}}!");
        t.SubjectTemplate.Should().BeNull();
        t.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ChangesTemplates()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.BookingConfirmed,
            NotificationChannel.Email,
            bodyTemplate: "original",
            subjectTemplate: "original subject");

        t.Update("novo subject", "novo body");

        t.SubjectTemplate.Should().Be("novo subject");
        t.BodyTemplate.Should().Be("novo body");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.BookingCancelled,
            NotificationChannel.WhatsApp,
            bodyTemplate: "cancelado");

        t.Deactivate();
        t.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var t = NotificationTemplate.Create(
            NotificationEventType.PaymentPending,
            NotificationChannel.Email,
            bodyTemplate: "pagamento");
        t.Deactivate();

        t.Activate();
        t.IsActive.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Domain.Tests --filter "NotificationTemplateTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create NotificationTemplate entity**

```csharp
// src/Horafy.Domain/Entities/Notifications/NotificationTemplate.cs
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Notifications;

public sealed class NotificationTemplate : BaseEntity
{
    private NotificationTemplate() { }

    public NotificationEventType EventType       { get; private set; }
    public NotificationChannel   Channel         { get; private set; }
    public string?               SubjectTemplate { get; private set; }
    public string                BodyTemplate    { get; private set; } = default!;
    public bool                  IsActive        { get; private set; } = true;

    public static NotificationTemplate Create(
        NotificationEventType eventType,
        NotificationChannel   channel,
        string                bodyTemplate,
        string?               subjectTemplate = null) =>
        new()
        {
            EventType       = eventType,
            Channel         = channel,
            BodyTemplate    = bodyTemplate,
            SubjectTemplate = subjectTemplate
        };

    public void Update(string? subjectTemplate, string bodyTemplate)
    {
        SubjectTemplate = subjectTemplate;
        BodyTemplate    = bodyTemplate;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Activate()   { IsActive = true;  UpdatedAt = DateTimeOffset.UtcNow; }
}
```

- [ ] **Step 4: Create INotificationTemplateRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/INotificationTemplateRepository.cs
using Horafy.Domain.Entities.Notifications;

namespace Horafy.Domain.Interfaces.Repositories;

public interface INotificationTemplateRepository : IRepository<NotificationTemplate>
{
    Task<NotificationTemplate?> GetActiveAsync(
        NotificationEventType eventType,
        NotificationChannel channel,
        CancellationToken ct = default);

    Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(
        CancellationToken ct = default);
}
```

- [ ] **Step 5: Create NotificationTemplateEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/NotificationTemplateEntityConfiguration.cs
using Horafy.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class NotificationTemplateEntityConfiguration
    : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.EventType).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.SubjectTemplate).HasMaxLength(300);
        builder.Property(t => t.BodyTemplate).IsRequired();

        builder.HasIndex(t => new { t.EventType, t.Channel })
            .HasFilter("is_active = TRUE AND is_deleted = FALSE")
            .HasDatabaseName("uq_notification_templates_event_channel");
    }
}
```

- [ ] **Step 6: Create NotificationTemplateRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/NotificationTemplateRepository.cs
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class NotificationTemplateRepository(TenantDbContext context)
    : BaseRepository<NotificationTemplate, TenantDbContext>(context),
      INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> GetActiveAsync(
        NotificationEventType eventType,
        NotificationChannel channel,
        CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.EventType == eventType &&
                t.Channel   == channel   &&
                t.IsActive, ct);

    public async Task<IReadOnlyList<NotificationTemplate>> GetAllActiveAsync(
        CancellationToken ct = default) =>
        await DbSet.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.EventType)
            .ThenBy(t => t.Channel)
            .ToListAsync(ct);
}
```

- [ ] **Step 7: Add DbSet to TenantDbContext**

In `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`, after `public DbSet<Payment> Payments`:

```csharp
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
```

Add `using Horafy.Domain.Entities.Notifications;` at the top.

- [ ] **Step 8: Add DDL to TenantSchemaService**

In `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, inside `BuildSchemaScript`, after the payments/indexes block, add:

```sql
        -- ── Templates de Notificação ──────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.notification_templates (
            id               UUID         NOT NULL DEFAULT gen_random_uuid(),
            event_type       VARCHAR(50)  NOT NULL,
            channel          VARCHAR(20)  NOT NULL,
            subject_template VARCHAR(300),
            body_template    TEXT         NOT NULL,
            is_active        BOOLEAN      NOT NULL DEFAULT TRUE,
            created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at       TIMESTAMPTZ,
            created_by       VARCHAR(256),
            updated_by       VARCHAR(256),
            is_deleted       BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at       TIMESTAMPTZ,
            deleted_by       VARCHAR(256),
            CONSTRAINT pk_notification_templates PRIMARY KEY (id)
        );

        CREATE INDEX IF NOT EXISTS uq_notification_templates_event_channel
            ON {s}.notification_templates (event_type, channel)
            WHERE is_active = TRUE AND is_deleted = FALSE;
```

- [ ] **Step 9: Register repository in DI**

In `src/Horafy.Infrastructure/DependencyInjection.cs`, after `services.AddScoped<IPaymentRepository, PaymentRepository>();`:

```csharp
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
```

Add `using Horafy.Domain.Interfaces.Repositories;` if missing.

- [ ] **Step 10: Build and run tests**

```
dotnet build Horafy.sln 2>&1 | grep -E "^.*error" | head -10
dotnet test tests/Horafy.Domain.Tests --filter "NotificationTemplateTests" 2>&1 | tail -5
```
Expected: 0 build errors, 4 tests passed.

- [ ] **Step 11: Commit**

```
git add src/Horafy.Domain/Entities/Notifications/
git add src/Horafy.Domain/Interfaces/Repositories/INotificationTemplateRepository.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/NotificationTemplateEntityConfiguration.cs
git add src/Horafy.Infrastructure/Repositories/NotificationTemplateRepository.cs
git add src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add tests/Horafy.Domain.Tests/Entities/NotificationTemplateTests.cs
git commit -m "feat: add NotificationTemplate entity, repository, DDL and EF config"
```

---

## Task 6: MassTransit + RabbitMQ setup

**Files:**
- Create: `src/Horafy.Infrastructure/Messaging/RabbitMqOptions.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`
- Modify: `src/Horafy.API/appsettings.json`
- Modify: `src/Horafy.API/appsettings.Development.json`

Sem testes unitários nesta task — verificação via `dotnet build`.

- [ ] **Step 1: Create RabbitMqOptions**

```csharp
// src/Horafy.Infrastructure/Messaging/RabbitMqOptions.cs
namespace Horafy.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string Host        { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username    { get; set; } = "guest";
    public string Password    { get; set; } = "guest";
}
```

- [ ] **Step 2: Wire up MassTransit in DependencyInjection.cs**

In `src/Horafy.Infrastructure/DependencyInjection.cs`, add the following usings at the top:

```csharp
using Horafy.Infrastructure.Email;
using Horafy.Infrastructure.Messaging;
using MassTransit;
```

Then, before `return services;`, add:

```csharp
        // Evolution API WhatsApp
        services.Configure<EvolutionApiOptions>(
            configuration.GetSection(EvolutionApiOptions.SectionName));
        services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
        {
            var baseUrl = configuration[$"{EvolutionApiOptions.SectionName}:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
                client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("apikey",
                configuration[$"{EvolutionApiOptions.SectionName}:ApiKey"] ?? string.Empty);
        });

        // SMTP e-mail
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        // MassTransit + RabbitMQ
        var rabbitOpts = configuration.GetSection(RabbitMqOptions.SectionName)
                             .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            // Consumers auto-discovered from this assembly
            x.AddConsumers(typeof(DependencyInjection).Assembly);

            // Quartz scheduler for reminders
            x.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();
            });
            x.AddQuartzConsumers();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitOpts.Host, rabbitOpts.VirtualHost, h =>
                {
                    h.Username(rabbitOpts.Username);
                    h.Password(rabbitOpts.Password);
                });

                cfg.UseMessageRetry(r => r.Exponential(3,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(125),
                    TimeSpan.FromSeconds(5)));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        // Outbox processor
        services.AddHostedService<OutboxProcessorService>();
```

Add `using Horafy.Infrastructure.Messaging;` and `using Horafy.Application.Interfaces;` if not present.

- [ ] **Step 3: Update appsettings.json**

Add to `src/Horafy.API/appsettings.json` (inside the root JSON object):

```json
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  },
  "EvolutionApi": {
    "BaseUrl": "",
    "ApiKey": "",
    "InstanceName": "horafy"
  },
  "Smtp": {
    "Host": "",
    "Port": 587,
    "Username": "",
    "Password": "",
    "FromAddress": "no-reply@horafy.com.br",
    "FromName": "Horafy",
    "UseSsl": true
  }
```

- [ ] **Step 4: Update appsettings.Development.json**

Add to `src/Horafy.API/appsettings.Development.json`:

```json
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  },
  "EvolutionApi": {
    "BaseUrl": "http://localhost:8080",
    "ApiKey": "dev-key",
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
```

- [ ] **Step 5: Build**

```
dotnet build Horafy.sln 2>&1 | grep -E "^.*error" | head -10
```
Expected: 0 erros.

- [ ] **Step 6: Commit**

```
git add src/Horafy.Infrastructure/Messaging/RabbitMqOptions.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add src/Horafy.API/appsettings.json
git add src/Horafy.API/appsettings.Development.json
git commit -m "feat: add MassTransit + RabbitMQ setup, Evolution API and SMTP registration"
```

---

## Task 7: MediatR notification publishers

**Files:**
- Create: `src/Horafy.Application/Features/Notifications/Publishers/BookingCreatedNotificationPublisher.cs`
- Create: `src/Horafy.Application/Features/Notifications/Publishers/BookingConfirmedNotificationPublisher.cs`
- Create: `src/Horafy.Application/Features/Notifications/Publishers/BookingCancelledNotificationPublisher.cs`
- Create: `src/Horafy.Application/Features/Notifications/Publishers/PaymentCreatedNotificationPublisher.cs`
- Create: `src/Horafy.Application/Features/Notifications/Publishers/PaymentConfirmedNotificationPublisher.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/BookingCreatedNotificationPublisherTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/BookingCreatedNotificationPublisherTests.cs
using FluentAssertions;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Features.Notifications.Publishers;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class BookingCreatedNotificationPublisherTests
{
    private readonly Mock<IBookingRepository>    _bookingRepo  = new();
    private readonly Mock<IResourceRepository>   _resourceRepo = new();
    private readonly Mock<ITenantRepository>     _tenantRepo   = new();
    private readonly Mock<ICurrentTenantService> _tenantSvc    = new();
    private readonly Mock<IPublishEndpoint>       _bus          = new();

    private BookingCreatedNotificationPublisher MakeHandler() =>
        new(_bookingRepo.Object, _resourceRepo.Object, _tenantRepo.Object,
            _tenantSvc.Object, _bus.Object);

    private static Booking MakeBooking()
    {
        var svcId = Service.Create("Corte", 60, 100m).Id;
        return Booking.Create(
            new[] { (svcId, "Corte de Cabelo", 60) },
            Resource.Create("Ana", ResourceType.Professional).Id,
            Guid.NewGuid(), "João Cliente", "joao@test.com",
            DateTimeOffset.UtcNow.AddHours(3));
    }

    [Fact]
    public async Task Handle_BookingAndTenantFound_PublishesBookingCreatedMessage()
    {
        var booking  = MakeBooking();
        var tenantId = Guid.NewGuid();
        var resource = Resource.Create("Ana", ResourceType.Professional);
        var tenant   = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);

        _tenantSvc.SetupGet(t => t.TenantId).Returns(tenantId);
        _tenantSvc.SetupGet(t => t.Slug).Returns("barbearia");
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _resourceRepo.Setup(r => r.GetByIdAsync(booking.ResourceId, default)).ReturnsAsync(resource);
        _tenantRepo.Setup(r => r.GetByIdAsync(tenantId, default)).ReturnsAsync(tenant);

        var evt = new BookingCreatedEvent(
            booking.Id, booking.ServiceId, booking.ResourceId,
            booking.CustomerId, booking.ScheduledAt);

        await MakeHandler().Handle(evt, default);

        _bus.Verify(b => b.Publish(
            It.Is<BookingCreatedMessage>(m =>
                m.BookingId     == booking.Id &&
                m.CustomerEmail == "joao@test.com" &&
                m.ServiceName   == "Corte de Cabelo" &&
                m.TenantSlug    == "barbearia"),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_BookingNotFound_DoesNotPublish()
    {
        _tenantSvc.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _tenantSvc.SetupGet(t => t.Slug).Returns("barbearia");
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Booking?)null);

        var evt = new BookingCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(2));

        await MakeHandler().Handle(evt, default);

        _bus.Verify(b => b.Publish(It.IsAny<BookingCreatedMessage>(), default), Times.Never);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingCreatedNotificationPublisherTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create BookingCreatedNotificationPublisher**

```csharp
// src/Horafy.Application/Features/Notifications/Publishers/BookingCreatedNotificationPublisher.cs
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingCreatedNotificationPublisher(
    IBookingRepository    bookingRepository,
    IResourceRepository   resourceRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<Domain.Events.Bookings.BookingCreatedEvent>
{
    public async Task Handle(
        Domain.Events.Bookings.BookingCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var resource   = await resourceRepository.GetByIdAsync(notification.ResourceId, cancellationToken);
        var tenantId   = currentTenant.TenantId;
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (tenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(tenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                          ?? booking.ServiceId.ToString();

        await publishEndpoint.Publish(new BookingCreatedMessage(
            BookingId:     booking.Id,
            CustomerName:  booking.CustomerName,
            CustomerEmail: booking.CustomerEmail,
            CustomerPhone: null,
            ServiceName:   serviceName,
            ResourceName:  resource?.Name ?? "Profissional",
            ScheduledAt:   booking.ScheduledAt,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
```

- [ ] **Step 4: Create BookingConfirmedNotificationPublisher**

```csharp
// src/Horafy.Application/Features/Notifications/Publishers/BookingConfirmedNotificationPublisher.cs
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingConfirmedNotificationPublisher(
    IBookingRepository    bookingRepository,
    IResourceRepository   resourceRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<BookingConfirmedEvent>
{
    public async Task Handle(BookingConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var booking    = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var resource   = await resourceRepository.GetByIdAsync(booking.ResourceId, cancellationToken);
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                          ?? booking.ServiceId.ToString();

        await publishEndpoint.Publish(new BookingConfirmedMessage(
            BookingId:     booking.Id,
            CustomerName:  notification.CustomerName,
            CustomerEmail: notification.CustomerEmail,
            CustomerPhone: null,
            ServiceName:   serviceName,
            ResourceName:  resource?.Name ?? "Profissional",
            ScheduledAt:   notification.ScheduledAt,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
```

- [ ] **Step 5: Create BookingCancelledNotificationPublisher**

```csharp
// src/Horafy.Application/Features/Notifications/Publishers/BookingCancelledNotificationPublisher.cs
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class BookingCancelledNotificationPublisher(
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<BookingCancelledEvent>
{
    public async Task Handle(BookingCancelledEvent notification, CancellationToken cancellationToken)
    {
        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        var reason = string.IsNullOrWhiteSpace(notification.Reason)
            ? string.Empty
            : $"Motivo: {notification.Reason}. ";

        await publishEndpoint.Publish(new BookingCancelledMessage(
            BookingId:     notification.BookingId,
            CustomerName:  "Cliente",
            CustomerEmail: string.Empty,
            CustomerPhone: null,
            Reason:        reason,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
```

**Nota:** `BookingCancelledEvent` não carrega `CustomerName`/`CustomerEmail` por enquanto. O consumer enviará e-mail apenas quando esses dados forem incluídos no evento (Sprint 8 poderá enriquecer o evento).

- [ ] **Step 6: Create PaymentCreatedNotificationPublisher**

```csharp
// src/Horafy.Application/Features/Notifications/Publishers/PaymentCreatedNotificationPublisher.cs
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class PaymentCreatedNotificationPublisher(
    IPaymentRepository    paymentRepository,
    IBookingRepository    bookingRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<PaymentCreatedEvent>
{
    public async Task Handle(PaymentCreatedEvent notification, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(notification.PaymentId, cancellationToken);
        if (payment is null) return;

        var booking    = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        await publishEndpoint.Publish(new PaymentPendingMessage(
            PaymentId:     payment.Id,
            BookingId:     notification.BookingId,
            CustomerName:  booking.CustomerName,
            CustomerEmail: booking.CustomerEmail,
            CustomerPhone: null,
            PaymentUrl:    payment.PaymentUrl,
            Amount:        notification.Amount,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
```

- [ ] **Step 7: Create PaymentConfirmedNotificationPublisher**

```csharp
// src/Horafy.Application/Features/Notifications/Publishers/PaymentConfirmedNotificationPublisher.cs
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Domain.Events.Payments;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using MediatR;

namespace Horafy.Application.Features.Notifications.Publishers;

internal sealed class PaymentConfirmedNotificationPublisher(
    IPaymentRepository    paymentRepository,
    IBookingRepository    bookingRepository,
    ITenantRepository     tenantRepository,
    ICurrentTenantService currentTenant,
    IPublishEndpoint      publishEndpoint)
    : INotificationHandler<PaymentConfirmedEvent>
{
    public async Task Handle(PaymentConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(notification.PaymentId, cancellationToken);
        if (payment is null) return;

        var booking    = await bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);
        if (booking is null) return;

        var tenantName = "Horafy";
        var tenantSlug = currentTenant.Slug ?? "horafy";

        if (currentTenant.TenantId.HasValue)
        {
            var tenant = await tenantRepository.GetByIdAsync(currentTenant.TenantId.Value, cancellationToken);
            if (tenant is not null) tenantName = tenant.Name;
        }

        await publishEndpoint.Publish(new PaymentConfirmedMessage(
            PaymentId:     payment.Id,
            BookingId:     notification.BookingId,
            CustomerName:  booking.CustomerName,
            CustomerEmail: booking.CustomerEmail,
            CustomerPhone: null,
            Amount:        payment.Amount,
            TenantSlug:    tenantSlug,
            TenantName:    tenantName), cancellationToken);
    }
}
```

- [ ] **Step 8: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingCreatedNotificationPublisherTests" 2>&1 | tail -5
dotnet build Horafy.sln 2>&1 | grep -E "^.*error" | head -10
```
Expected: 2 passed, 0 build errors.

- [ ] **Step 9: Commit**

```
git add src/Horafy.Application/Features/Notifications/Publishers/
git add tests/Horafy.Application.Tests/Notifications/BookingCreatedNotificationPublisherTests.cs
git commit -m "feat: add MediatR notification publishers for booking and payment events"
```

---

## Task 8: MassTransit consumers

**Files:**
- Create: `src/Horafy.Infrastructure/Messaging/Consumers/BookingCreatedConsumer.cs`
- Create: `src/Horafy.Infrastructure/Messaging/Consumers/BookingConfirmedConsumer.cs`
- Create: `src/Horafy.Infrastructure/Messaging/Consumers/BookingCancelledConsumer.cs`
- Create: `src/Horafy.Infrastructure/Messaging/Consumers/PaymentPendingConsumer.cs`
- Create: `src/Horafy.Infrastructure/Messaging/Consumers/PaymentConfirmedConsumer.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/BookingCreatedConsumerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/BookingCreatedConsumerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using Horafy.Infrastructure.Messaging.Consumers;
using MassTransit;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class BookingCreatedConsumerTests
{
    private readonly Mock<IWhatsAppService> _whatsApp = new();
    private readonly Mock<IEmailService>    _email    = new();

    private BookingCreatedConsumer MakeConsumer() =>
        new(_whatsApp.Object, _email.Object);

    private static ConsumeContext<BookingCreatedMessage> MakeContext(
        string? phone = "5511999999999")
    {
        var msg = new BookingCreatedMessage(
            Guid.NewGuid(), "João", "joao@test.com", phone,
            "Corte", "Ana", DateTimeOffset.UtcNow.AddHours(2),
            "barbearia", "Barbearia do João");

        var ctx = new Mock<ConsumeContext<BookingCreatedMessage>>();
        ctx.SetupGet(c => c.Message).Returns(msg);
        return ctx.Object;
    }

    [Fact]
    public async Task Consume_WithPhone_SendsWhatsAppAndEmail()
    {
        var ctx = MakeContext(phone: "5511999999999");
        await MakeConsumer().Consume(ctx);

        _whatsApp.Verify(w => w.SendTextAsync(
            "5511999999999", It.IsAny<string>(), default), Times.Once);
        _email.Verify(e => e.SendAsync(
            "joao@test.com", It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task Consume_WithoutPhone_SendsEmailOnly()
    {
        var ctx = MakeContext(phone: null);
        await MakeConsumer().Consume(ctx);

        _whatsApp.Verify(w => w.SendTextAsync(
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        _email.Verify(e => e.SendAsync(
            "joao@test.com", It.IsAny<string>(), It.IsAny<string>(), default), Times.Once);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingCreatedConsumerTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create BookingCreatedConsumer**

```csharp
// src/Horafy.Infrastructure/Messaging/Consumers/BookingCreatedConsumer.cs
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingCreatedConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingCreatedMessage>
{
    public async Task Consume(ConsumeContext<BookingCreatedMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"]  = msg.CustomerName,
            ["service_name"]   = msg.ServiceName,
            ["resource_name"]  = msg.ResourceName,
            ["scheduled_at"]   = TemplateRenderer.FormatBrazilian(msg.ScheduledAt),
            ["tenant_name"]    = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingCreated, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingCreated, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingCreated, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
```

- [ ] **Step 4: Create BookingConfirmedConsumer**

```csharp
// src/Horafy.Infrastructure/Messaging/Consumers/BookingConfirmedConsumer.cs
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingConfirmedConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingConfirmedMessage>
{
    public async Task Consume(ConsumeContext<BookingConfirmedMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["service_name"]  = msg.ServiceName,
            ["resource_name"] = msg.ResourceName,
            ["scheduled_at"]  = TemplateRenderer.FormatBrazilian(msg.ScheduledAt),
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingConfirmed, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingConfirmed, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingConfirmed, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
```

- [ ] **Step 5: Create BookingCancelledConsumer**

```csharp
// src/Horafy.Infrastructure/Messaging/Consumers/BookingCancelledConsumer.cs
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingCancelledConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingCancelledMessage>
{
    public async Task Consume(ConsumeContext<BookingCancelledMessage> context)
    {
        var msg  = context.Message;
        if (string.IsNullOrEmpty(msg.CustomerEmail)) return;

        var vars = new Dictionary<string, string>
        {
            ["customer_name"]        = msg.CustomerName,
            ["cancellation_reason"]  = string.IsNullOrEmpty(msg.Reason) ? "" : $"{msg.Reason} ",
            ["tenant_name"]          = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.BookingCancelled, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingCancelled, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingCancelled, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
```

- [ ] **Step 6: Create PaymentPendingConsumer**

```csharp
// src/Horafy.Infrastructure/Messaging/Consumers/PaymentPendingConsumer.cs
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class PaymentPendingConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<PaymentPendingMessage>
{
    public async Task Consume(ConsumeContext<PaymentPendingMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["amount"]        = msg.Amount.ToString("N2"),
            ["payment_url"]   = msg.PaymentUrl ?? "",
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.PaymentPending, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.PaymentPending, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.PaymentPending, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
```

- [ ] **Step 7: Create PaymentConfirmedConsumer**

```csharp
// src/Horafy.Infrastructure/Messaging/Consumers/PaymentConfirmedConsumer.cs
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class PaymentConfirmedConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<PaymentConfirmedMessage>
{
    public async Task Consume(ConsumeContext<PaymentConfirmedMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"] = msg.CustomerName,
            ["amount"]        = msg.Amount.ToString("N2"),
            ["tenant_name"]   = msg.TenantName
        };

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(DefaultTemplates.WhatsApp.PaymentConfirmed, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.PaymentConfirmed, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.PaymentConfirmed, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
```

- [ ] **Step 8: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingCreatedConsumerTests" 2>&1 | tail -5
dotnet build Horafy.sln 2>&1 | grep -E "^.*error" | head -10
```
Expected: 2 passed, 0 errors.

- [ ] **Step 9: Commit**

```
git add src/Horafy.Infrastructure/Messaging/Consumers/
git add tests/Horafy.Application.Tests/Notifications/BookingCreatedConsumerTests.cs
git commit -m "feat: add MassTransit consumers for booking and payment notifications"
```

---

## Task 9: OutboxProcessorService

**Files:**
- Create: `src/Horafy.Infrastructure/Messaging/OutboxProcessorService.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/OutboxProcessorServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/OutboxProcessorServiceTests.cs
using FluentAssertions;
using Horafy.Infrastructure.Messaging;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class OutboxProcessorServiceTests
{
    private static HorafyDbContext MakeContext()
    {
        var opts = new DbContextOptionsBuilder<HorafyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HorafyDbContext(opts);
    }

    [Fact]
    public async Task ProcessBatchAsync_UnprocessedMessage_MarksAsProcessed()
    {
        var context = MakeContext();
        var bus     = new Mock<IBus>();
        var msg     = new OutboxMessage
        {
            Id         = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type       = typeof(FakeEvent).AssemblyQualifiedName!,
            Content    = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-06-06T00:00:00+00:00"}"""
        };
        context.Set<OutboxMessage>().Add(msg);
        await context.SaveChangesAsync();

        var processor = new OutboxProcessorService(
            context, bus.Object, NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessBatchAsync(default);

        var stored = await context.Set<OutboxMessage>().FindAsync(msg.Id);
        stored!.ProcessedAt.Should().NotBeNull();
        stored.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessBatchAsync_PublishThrows_IncrementsRetryCount()
    {
        var context = MakeContext();
        var bus     = new Mock<IBus>();
        bus.Setup(b => b.Publish(It.IsAny<object>(), It.IsAny<Type>(), default))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        var msg = new OutboxMessage
        {
            Id         = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type       = typeof(FakeEvent).AssemblyQualifiedName!,
            Content    = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-06-06T00:00:00+00:00"}"""
        };
        context.Set<OutboxMessage>().Add(msg);
        await context.SaveChangesAsync();

        var processor = new OutboxProcessorService(
            context, bus.Object, NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessBatchAsync(default);

        var stored = await context.Set<OutboxMessage>().FindAsync(msg.Id);
        stored!.RetryCount.Should().Be(1);
        stored.ProcessedAt.Should().BeNull();
        stored.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessBatchAsync_MaxRetriesReached_StopsProcessing()
    {
        var context = MakeContext();
        var bus     = new Mock<IBus>();
        var msg     = new OutboxMessage
        {
            Id         = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Type       = typeof(FakeEvent).AssemblyQualifiedName!,
            Content    = "{}",
            RetryCount = 3
        };
        context.Set<OutboxMessage>().Add(msg);
        await context.SaveChangesAsync();

        var processor = new OutboxProcessorService(
            context, bus.Object, NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessBatchAsync(default);

        bus.Verify(b => b.Publish(It.IsAny<object>(), It.IsAny<Type>(), default), Times.Never);
    }

    private sealed record FakeEvent : Horafy.Domain.Events.Base.DomainEvent;
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "OutboxProcessorServiceTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create OutboxProcessorService**

```csharp
// src/Horafy.Infrastructure/Messaging/OutboxProcessorService.cs
using System.Text.Json;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Messaging;

internal sealed class OutboxProcessorService(
    HorafyDbContext context,
    IBus            bus,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private const int MaxRetries        = 3;
    private const int BatchSize         = 20;
    private const int IntervalSeconds   = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro no OutboxProcessorService");
            }

            await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
        }
    }

    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        var messages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type);
                if (type is null)
                {
                    message.Error      = $"Tipo não encontrado: {message.Type}";
                    message.RetryCount = MaxRetries;
                    continue;
                }

                var payload = JsonSerializer.Deserialize(message.Content, type);
                if (payload is not null)
                    await bus.Publish(payload, type, ct);

                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.Error       = null;

                logger.LogInformation("Outbox message {Id} publicado: {Type}", message.Id, type.Name);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                logger.LogWarning(ex,
                    "Falha ao processar outbox message {Id} (tentativa {Retry}/{Max})",
                    message.Id, message.RetryCount, MaxRetries);
            }
        }

        if (messages.Count > 0)
            await context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "OutboxProcessorServiceTests" 2>&1 | tail -5
```
Expected: 3 passed.

- [ ] **Step 5: Commit**

```
git add src/Horafy.Infrastructure/Messaging/OutboxProcessorService.cs
git add tests/Horafy.Application.Tests/Notifications/OutboxProcessorServiceTests.cs
git commit -m "feat: add OutboxProcessorService — reads outbox_messages and publishes to RabbitMQ"
```

---

## Task 10: BookingReminderJob + BookingReminderConsumer

**Files:**
- Create: `src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs`
- Create: `src/Horafy.Infrastructure/Messaging/Consumers/BookingReminderConsumer.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/BookingReminderJobTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/BookingReminderJobTests.cs
using FluentAssertions;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Entities.Tenants;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Messaging.Jobs;
using MassTransit;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class BookingReminderJobTests
{
    private readonly Mock<IBookingRepository>  _bookingRepo  = new();
    private readonly Mock<IResourceRepository> _resourceRepo = new();
    private readonly Mock<ITenantRepository>   _tenantRepo   = new();
    private readonly Mock<IBus>                _bus          = new();

    private BookingReminderJob MakeJob() =>
        new(_bookingRepo.Object, _resourceRepo.Object, _tenantRepo.Object, _bus.Object);

    private static Booking MakeConfirmedBookingAt(DateTimeOffset scheduledAt)
    {
        var svcId = Service.Create("Corte", 60, 100m).Id;
        var b = Booking.Create(
            new[] { (svcId, "Corte", 60) },
            Resource.Create("Ana", ResourceType.Professional).Id,
            Guid.NewGuid(), "João", "joao@test.com",
            scheduledAt);
        b.Confirm();
        b.ClearDomainEvents();
        return b;
    }

    [Fact]
    public async Task ExecuteAsync_BookingIn24Hours_PublishesOneDayBeforeReminder()
    {
        var now     = DateTimeOffset.UtcNow;
        var booking = MakeConfirmedBookingAt(now.AddHours(24));
        var tenant  = Tenant.Create("Barbearia", "barbearia", TenantVertical.Barbershop);
        var resource = Resource.Create("Ana", ResourceType.Professional);

        _bookingRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync(new List<Booking> { booking });
        _resourceRepo.Setup(r => r.GetByIdAsync(booking.ResourceId, default)).ReturnsAsync(resource);
        _tenantRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Tenant> { tenant });

        await MakeJob().ExecuteAsync(now, default);

        _bus.Verify(b => b.Publish(
            It.Is<BookingReminderMessage>(m =>
                m.BookingId      == booking.Id &&
                m.IsOneDayBefore == true),
            default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoUpcomingBookings_DoesNotPublish()
    {
        _bookingRepo.Setup(r => r.FindAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Booking, bool>>>(), default))
            .ReturnsAsync(new List<Booking>());
        _tenantRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Tenant>());

        await MakeJob().ExecuteAsync(DateTimeOffset.UtcNow, default);

        _bus.Verify(b => b.Publish(It.IsAny<BookingReminderMessage>(), default), Times.Never);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingReminderJobTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create BookingReminderJob**

```csharp
// src/Horafy.Infrastructure/Messaging/Jobs/BookingReminderJob.cs
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Horafy.Infrastructure.Messaging.Jobs;

[DisallowConcurrentExecution]
public sealed class BookingReminderJob(
    IBookingRepository  bookingRepository,
    IResourceRepository resourceRepository,
    ITenantRepository   tenantRepository,
    IBus                bus,
    ILogger<BookingReminderJob>? logger = null) : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await ExecuteAsync(DateTimeOffset.UtcNow, context.CancellationToken);

    public async Task ExecuteAsync(DateTimeOffset now, CancellationToken ct)
    {
        var oneDayMin = now.AddHours(22);
        var oneDayMax = now.AddHours(26);
        var twoHrMin  = now.AddHours(1);
        var twoHrMax  = now.AddHours(3);

        var oneDayBookings = await bookingRepository.FindAsync(
            b => b.Status == BookingStatus.Confirmed &&
                 b.ScheduledAt >= oneDayMin && b.ScheduledAt <= oneDayMax, ct);

        var twoHourBookings = await bookingRepository.FindAsync(
            b => b.Status == BookingStatus.Confirmed &&
                 b.ScheduledAt >= twoHrMin && b.ScheduledAt <= twoHrMax, ct);

        var tenants = await tenantRepository.GetAllAsync(ct);
        var tenantMap = tenants.ToDictionary(t => t.Id, t => t.Name);

        foreach (var (booking, isOneDay) in
            oneDayBookings.Select(b => (b, true))
            .Concat(twoHourBookings.Select(b => (b, false))))
        {
            var resource   = await resourceRepository.GetByIdAsync(booking.ResourceId, ct);
            var serviceName = booking.Services.FirstOrDefault()?.ServiceName
                              ?? booking.ServiceId.ToString();

            var msg = new BookingReminderMessage(
                BookingId:      booking.Id,
                CustomerName:   booking.CustomerName,
                CustomerEmail:  booking.CustomerEmail,
                CustomerPhone:  null,
                ServiceName:    serviceName,
                ResourceName:   resource?.Name ?? "Profissional",
                ScheduledAt:    booking.ScheduledAt,
                TenantSlug:     "horafy",
                TenantName:     "Horafy",
                IsOneDayBefore: isOneDay);

            await bus.Publish(msg, ct);

            logger?.LogInformation(
                "Lembrete {Type} publicado para booking {Id}",
                isOneDay ? "D-1" : "H-2", booking.Id);
        }
    }
}
```

- [ ] **Step 4: Register Quartz job in DI**

In `src/Horafy.Infrastructure/DependencyInjection.cs`, inside the `x.AddQuartz(q => { ... })` block:

```csharp
            x.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                var jobKey = new Quartz.JobKey("booking-reminder");
                q.AddJob<Horafy.Infrastructure.Messaging.Jobs.BookingReminderJob>(
                    opts => opts.WithIdentity(jobKey));
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("booking-reminder-trigger")
                    .WithCronSchedule("0 0 * * * ?"));  // toda hora em :00
            });
```

- [ ] **Step 5: Create BookingReminderConsumer**

```csharp
// src/Horafy.Infrastructure/Messaging/Consumers/BookingReminderConsumer.cs
using Horafy.Application.Features.Notifications;
using Horafy.Application.Features.Notifications.Messages;
using Horafy.Application.Interfaces;
using MassTransit;

namespace Horafy.Infrastructure.Messaging.Consumers;

internal sealed class BookingReminderConsumer(
    IWhatsAppService whatsAppService,
    IEmailService    emailService) : IConsumer<BookingReminderMessage>
{
    public async Task Consume(ConsumeContext<BookingReminderMessage> context)
    {
        var msg  = context.Message;
        var vars = new Dictionary<string, string>
        {
            ["customer_name"]  = msg.CustomerName,
            ["service_name"]   = msg.ServiceName,
            ["resource_name"]  = msg.ResourceName,
            ["scheduled_at"]   = TemplateRenderer.FormatBrazilian(msg.ScheduledAt),
            ["scheduled_time"] = msg.ScheduledAt.ToString("HH:mm"),
            ["tenant_name"]    = msg.TenantName
        };

        var whatsAppTemplate = msg.IsOneDayBefore
            ? DefaultTemplates.WhatsApp.BookingReminderOneDay
            : DefaultTemplates.WhatsApp.BookingReminderTwoHours;

        if (!string.IsNullOrEmpty(msg.CustomerPhone))
        {
            var text = TemplateRenderer.Render(whatsAppTemplate, vars);
            await whatsAppService.SendTextAsync(msg.CustomerPhone, text, context.CancellationToken);
        }

        var subject = TemplateRenderer.Render(DefaultTemplates.EmailSubject.BookingReminder, vars);
        var body    = TemplateRenderer.Render(DefaultTemplates.EmailBody.BookingReminder, vars);
        await emailService.SendAsync(msg.CustomerEmail, subject, body, context.CancellationToken);
    }
}
```

- [ ] **Step 6: Run tests**

```
dotnet test tests/Horafy.Application.Tests --filter "BookingReminderJobTests" 2>&1 | tail -5
dotnet build Horafy.sln 2>&1 | grep -E "^.*error" | head -10
```
Expected: 2 passed, 0 errors.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Infrastructure/Messaging/Jobs/
git add src/Horafy.Infrastructure/Messaging/Consumers/BookingReminderConsumer.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add tests/Horafy.Application.Tests/Notifications/BookingReminderJobTests.cs
git commit -m "feat: add BookingReminderJob (Quartz D-1/H-2) and BookingReminderConsumer"
```

---

## Task 11: UpsertNotificationTemplateCommand + GetNotificationTemplatesQuery + Controller

**Files:**
- Create: `src/Horafy.Application/Features/Notifications/Commands/UpsertNotificationTemplateCommand.cs`
- Create: `src/Horafy.Application/Features/Notifications/Queries/GetNotificationTemplatesQuery.cs`
- Create: `src/Horafy.API/Controllers/V1/NotificationTemplatesController.cs`
- Create: `tests/Horafy.Application.Tests/Notifications/UpsertNotificationTemplateCommandTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Notifications/UpsertNotificationTemplateCommandTests.cs
using FluentAssertions;
using Horafy.Application.Features.Notifications.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Notifications;

public sealed class UpsertNotificationTemplateCommandTests
{
    private readonly Mock<INotificationTemplateRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork>               _uow  = new();

    private UpsertNotificationTemplateCommandHandler MakeHandler() =>
        new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NoExistingTemplate_CreatesNew()
    {
        _repo.Setup(r => r.GetActiveAsync(
                NotificationEventType.BookingCreated,
                NotificationChannel.WhatsApp, default))
            .ReturnsAsync((NotificationTemplate?)null);

        var result = await MakeHandler().Handle(new UpsertNotificationTemplateCommand(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            BodyTemplate: "Olá {{customer_name}}!",
            SubjectTemplate: null), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingTemplate_UpdatesIt()
    {
        var existing = NotificationTemplate.Create(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            bodyTemplate: "original");

        _repo.Setup(r => r.GetActiveAsync(
                NotificationEventType.BookingCreated,
                NotificationChannel.WhatsApp, default))
            .ReturnsAsync(existing);

        var result = await MakeHandler().Handle(new UpsertNotificationTemplateCommand(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            BodyTemplate: "Novo texto",
            SubjectTemplate: null), default);

        result.IsSuccess.Should().BeTrue();
        existing.BodyTemplate.Should().Be("Novo texto");
        _repo.Verify(r => r.Add(It.IsAny<NotificationTemplate>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyBodyTemplate_ReturnsValidationError()
    {
        var result = await MakeHandler().Handle(new UpsertNotificationTemplateCommand(
            NotificationEventType.BookingCreated,
            NotificationChannel.WhatsApp,
            BodyTemplate: "",
            SubjectTemplate: null), default);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "UpsertNotificationTemplateCommandTests" 2>&1 | tail -5
```
Expected: compile error.

- [ ] **Step 3: Create UpsertNotificationTemplateCommand**

```csharp
// src/Horafy.Application/Features/Notifications/Commands/UpsertNotificationTemplateCommand.cs
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Notifications.Commands;

public sealed record UpsertNotificationTemplateCommand(
    NotificationEventType EventType,
    NotificationChannel   Channel,
    string                BodyTemplate,
    string?               SubjectTemplate) : IRequest<Result>;

public sealed class UpsertNotificationTemplateCommandValidator
    : AbstractValidator<UpsertNotificationTemplateCommand>
{
    public UpsertNotificationTemplateCommandValidator()
    {
        RuleFor(x => x.BodyTemplate).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SubjectTemplate).MaximumLength(300).When(x => x.SubjectTemplate is not null);
    }
}

internal sealed class UpsertNotificationTemplateCommandHandler(
    INotificationTemplateRepository repository,
    ITenantUnitOfWork unitOfWork) : IRequestHandler<UpsertNotificationTemplateCommand, Result>
{
    public async Task<Result> Handle(
        UpsertNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
            return Result.Failure(new Error(
                "NotificationTemplate.EmptyBody", "O corpo do template não pode ser vazio.",
                ErrorType.Validation));

        var existing = await repository.GetActiveAsync(
            request.EventType, request.Channel, cancellationToken);

        if (existing is not null)
        {
            existing.Update(request.SubjectTemplate, request.BodyTemplate);
            repository.Update(existing);
        }
        else
        {
            var template = NotificationTemplate.Create(
                request.EventType, request.Channel,
                request.BodyTemplate, request.SubjectTemplate);
            repository.Add(template);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Create GetNotificationTemplatesQuery**

```csharp
// src/Horafy.Application/Features/Notifications/Queries/GetNotificationTemplatesQuery.cs
using Horafy.Domain.Entities.Notifications;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Notifications.Queries;

public sealed record GetNotificationTemplatesQuery : IRequest<Result<IReadOnlyList<NotificationTemplateResult>>>;

public sealed record NotificationTemplateResult(
    Guid                  Id,
    NotificationEventType EventType,
    NotificationChannel   Channel,
    string?               SubjectTemplate,
    string                BodyTemplate,
    bool                  IsActive);

internal sealed class GetNotificationTemplatesQueryHandler(
    INotificationTemplateRepository repository)
    : IRequestHandler<GetNotificationTemplatesQuery, Result<IReadOnlyList<NotificationTemplateResult>>>
{
    public async Task<Result<IReadOnlyList<NotificationTemplateResult>>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllActiveAsync(cancellationToken);
        var result = templates
            .Select(t => new NotificationTemplateResult(
                t.Id, t.EventType, t.Channel,
                t.SubjectTemplate, t.BodyTemplate, t.IsActive))
            .ToList();
        return Result.Success<IReadOnlyList<NotificationTemplateResult>>(result);
    }
}
```

- [ ] **Step 5: Create NotificationTemplatesController**

```csharp
// src/Horafy.API/Controllers/V1/NotificationTemplatesController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Notifications.Commands;
using Horafy.Application.Features.Notifications.Queries;
using Horafy.Domain.Entities.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "TenantOwner,TenantAdmin")]
public sealed class NotificationTemplatesController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationTemplateResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetNotificationTemplatesQuery(), ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertTemplateRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(new UpsertNotificationTemplateCommand(
            request.EventType, request.Channel,
            request.BodyTemplate, request.SubjectTemplate), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record UpsertTemplateRequest(
    NotificationEventType EventType,
    NotificationChannel   Channel,
    string                BodyTemplate,
    string?               SubjectTemplate);
```

- [ ] **Step 6: Run all tests**

```
dotnet test Horafy.sln 2>&1 | tail -10
```
Expected: todos os testes passando.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Features/Notifications/Commands/
git add src/Horafy.Application/Features/Notifications/Queries/
git add src/Horafy.API/Controllers/V1/NotificationTemplatesController.cs
git add tests/Horafy.Application.Tests/Notifications/UpsertNotificationTemplateCommandTests.cs
git commit -m "feat: add UpsertNotificationTemplateCommand, GetNotificationTemplatesQuery and controller"
```

---

## Self-Review

**Spec coverage:**
- ✅ RabbitMQ + MassTransit (Task 6)
- ✅ Evolution API WhatsApp (Task 2)
- ✅ Templates por tenant (Task 5, 11)
- ✅ Retry/dead-letter (Task 6 — configurado em `UseMessageRetry`)
- ✅ BookingCreated, BookingConfirmed, BookingCancelled, PaymentPending, PaymentConfirmed events (Tasks 7+8)
- ✅ Lembretes D-1/H-2 (Task 10)
- ✅ OutboxProcessor (Task 9)
- ⚠️ E-mail transacional (Task 3 — SMTP; migração para SendGrid/SES é pós-sprint)
- ⚠️ `CustomerPhone` é `null` em todos os messages — aguarda Sprint 8 (módulo Clientes que adiciona phone ao User)

**Gaps identificados e resolvidos:**
- `BookingConfirmedEvent` ausente no domínio → adicionado na Task 1
- Enums `NotificationEventType`/`NotificationChannel` precisam estar no Domain (não Application) → estão em `Horafy.Domain.Entities.Notifications`
- `BookingCancelledEvent` não carrega `CustomerEmail` → consumer só envia se email não for vazio; Sprint 8 enriquece o evento

**Consistência de tipos verificada:**
- `NotificationEventType` e `NotificationChannel` usados em Task 4, 5, 7, 8, 9, 10, 11 — todos referenciam `Horafy.Domain.Entities.Notifications`
- `BookingCreatedMessage`, `BookingConfirmedMessage`, etc. são `sealed record` — usados em publishers (Task 7) e consumers (Task 8)
- `INotificationTemplateRepository.GetActiveAsync(eventType, channel)` — assinatura consistente entre Task 5 (interface), Task 5 (impl) e Task 11 (handler)
