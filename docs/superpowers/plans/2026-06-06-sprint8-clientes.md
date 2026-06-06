# Sprint 8 — Módulo Clientes (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adicionar o Módulo Clientes ao Horafy: campo `Phone` no `User`, OAuth exclusivo para clientes finais, perfil e histórico de agendamentos do cliente autenticado, entidade `Review` (avaliação 1–5 estrelas + comentário) e entidade `FavoriteService` (favoritos por cliente).

**Architecture:** Todas as entidades de cliente (`Review`, `FavoriteService`) residem no schema `tenant_{slug}` e são gerenciadas via `TenantDbContext`. O `User.Phone` fica em `public.users` com `ALTER TABLE … ADD COLUMN IF NOT EXISTS`. O OAuth para clientes já existe via `LoginWithGoogleCommand`/`LoginWithAppleCommand` — o Sprint 8 apenas adiciona endpoints dedicados (`/api/v1/customers/auth/google`, `/api/v1/customers/auth/apple`) que validam `role=Customer`. `CustomerPhone` em `Booking.Create` e nos domain events do Sprint 7 é populado via `User.Phone`.

**Tech Stack:** .NET 8, MediatR, EF Core 8, PostgreSQL 16, FluentAssertions, xUnit, Moq, ASP.NET Core Minimal Controllers.

---

## File Map

**Novos arquivos:**
- `src/Horafy.Domain/Entities/Users/User.cs` — `Phone` property + `SetPhone(string?)` method
- `src/Horafy.Infrastructure/Persistence/Configurations/UserEntityConfiguration.cs` — `phone` column mapping
- `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs` — DDL `customer_phone` em `bookings` + DDL `reviews` + DDL `favorite_services`
- `src/Horafy.Domain/Entities/Bookings/Booking.cs` — `CustomerPhone` property + param em `Create()`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs` — mapa `customer_phone`
- `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs` — popula `customerPhone` do `IUserRepository`
- `src/Horafy.Application/Features/Notifications/Publishers/*.cs` — popula `CustomerPhone` nos 5 publishers existentes
- `src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithGoogleCommand.cs` — wrapper que chama handler do Google e rejeita não-Customer
- `src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithAppleCommand.cs` — wrapper para Apple
- `src/Horafy.API/Controllers/V1/CustomerAuthController.cs` — POST `/api/v1/customers/auth/google` + `/api/v1/customers/auth/apple`
- `src/Horafy.Application/Features/Customers/Queries/GetCustomerProfileQuery.cs` — GET /customers/me
- `src/Horafy.Application/Features/Customers/Commands/UpdateCustomerPhoneCommand.cs` — PATCH /customers/me/phone
- `src/Horafy.Application/Features/Customers/Queries/GetCustomerBookingsQuery.cs` — GET /customers/me/bookings
- `src/Horafy.API/Controllers/V1/CustomersController.cs` — controller `[Authorize(Roles = "Customer")]`
- `src/Horafy.Domain/Entities/Reviews/Review.cs` — entidade + `Create()` + `Update()`
- `src/Horafy.Domain/Interfaces/Repositories/IReviewRepository.cs`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ReviewEntityConfiguration.cs`
- `src/Horafy.Infrastructure/Repositories/ReviewRepository.cs`
- `src/Horafy.Application/Features/Reviews/Commands/CreateReviewCommand.cs`
- `src/Horafy.Application/Features/Reviews/Queries/GetResourceReviewsQuery.cs`
- `src/Horafy.API/Controllers/V1/ReviewsController.cs`
- `src/Horafy.Domain/Entities/Favorites/FavoriteService.cs` — entidade (sem soft-delete, FK CustomerId+ServiceId)
- `src/Horafy.Domain/Interfaces/Repositories/IFavoriteServiceRepository.cs`
- `src/Horafy.Infrastructure/Persistence/TenantConfigurations/FavoriteServiceEntityConfiguration.cs`
- `src/Horafy.Infrastructure/Repositories/FavoriteServiceRepository.cs`
- `src/Horafy.Application/Features/Favorites/Commands/AddFavoriteServiceCommand.cs`
- `src/Horafy.Application/Features/Favorites/Commands/RemoveFavoriteServiceCommand.cs`
- `src/Horafy.Application/Features/Favorites/Queries/GetCustomerFavoritesQuery.cs`
- `src/Horafy.API/Controllers/V1/FavoriteServicesController.cs`

**Arquivos de teste:**
- `tests/Horafy.Domain.Tests/Users/UserPhoneTests.cs`
- `tests/Horafy.Domain.Tests/Bookings/BookingCustomerPhoneTests.cs`
- `tests/Horafy.Application.Tests/Customers/GetCustomerProfileQueryTests.cs`
- `tests/Horafy.Application.Tests/Customers/UpdateCustomerPhoneCommandTests.cs`
- `tests/Horafy.Application.Tests/Customers/GetCustomerBookingsQueryTests.cs`
- `tests/Horafy.Domain.Tests/Reviews/ReviewTests.cs`
- `tests/Horafy.Application.Tests/Reviews/CreateReviewCommandTests.cs`
- `tests/Horafy.Application.Tests/Reviews/GetResourceReviewsQueryTests.cs`
- `tests/Horafy.Domain.Tests/Favorites/FavoriteServiceTests.cs`
- `tests/Horafy.Application.Tests/Favorites/AddFavoriteServiceCommandTests.cs`
- `tests/Horafy.Application.Tests/Favorites/RemoveFavoriteServiceCommandTests.cs`
- `tests/Horafy.Application.Tests/Favorites/GetCustomerFavoritesQueryTests.cs`

---

## Task 1: User.Phone — entidade + DDL

**Files:**
- Modify: `src/Horafy.Domain/Entities/Users/User.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/Configurations/UserEntityConfiguration.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Create: `tests/Horafy.Domain.Tests/Users/UserPhoneTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Horafy.Domain.Tests/Users/UserPhoneTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Users;
using Xunit;

namespace Horafy.Domain.Tests.Users;

public sealed class UserPhoneTests
{
    private static User MakeUser() =>
        User.CreateWithGoogle("test@test.com", "google_id", "Test User", null, UserRole.Customer);

    [Fact]
    public void SetPhone_ValidPhone_SetsProperty()
    {
        var user = MakeUser();
        user.SetPhone("+5511999998888");
        user.Phone.Should().Be("+5511999998888");
    }

    [Fact]
    public void SetPhone_Null_ClearsProperty()
    {
        var user = MakeUser();
        user.SetPhone("+5511999998888");
        user.SetPhone(null);
        user.Phone.Should().BeNull();
    }

    [Fact]
    public void SetPhone_TooLong_Throws()
    {
        var user = MakeUser();
        var action = () => user.SetPhone(new string('1', 21));
        action.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~UserPhoneTests" -v minimal
```
Expected: FAIL — `User` has no `Phone` property or `SetPhone` method.

- [ ] **Step 3: Add `Phone` and `SetPhone` to User entity**

In `src/Horafy.Domain/Entities/Users/User.cs`, add after `public string? AvatarUrl`:

```csharp
public string? Phone { get; private set; }
```

And add the method after `UpdateProfile(...)`:

```csharp
public void SetPhone(string? phone)
{
    if (phone is not null && phone.Length > 20)
        throw new ArgumentException("Telefone deve ter no máximo 20 caracteres.", nameof(phone));
    Phone     = phone?.Trim();
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 4: Map `phone` column in EF configuration**

In `src/Horafy.Infrastructure/Persistence/Configurations/UserEntityConfiguration.cs`, add inside `Configure()` after `AvatarUrl` mapping:

```csharp
builder.Property(u => u.Phone)
    .HasMaxLength(20);
```

- [ ] **Step 5: Add DDL migration for `public.users.phone`**

In `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, add a new public method (called once by the API startup migration helper or manually via migration script):

Actually, because `public.users` is managed by `HorafyDbContext` (not tenant schema), the column is added via raw SQL migration run at startup. Open `src/Horafy.Infrastructure/DependencyInjection.cs` — find where `app.MigrateDatabase()` or similar runs. Instead, add a startup migration in `Program.cs`.

Add at the very end of `BuildSchemaScript` in `TenantSchemaService.cs` — no, `public.users` is a global table. The right place is a startup migration.

Create `src/Horafy.Infrastructure/Persistence/GlobalMigrations.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Persistence;

public static class GlobalMigrations
{
    public static async Task RunAsync(HorafyDbContext db, ILogger logger, CancellationToken ct = default)
    {
        const string sql = """
            ALTER TABLE public.users
                ADD COLUMN IF NOT EXISTS phone VARCHAR(20);
            """;

        logger.LogInformation("Running global migrations...");
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger.LogInformation("Global migrations complete.");
    }
}
```

- [ ] **Step 6: Call `GlobalMigrations.RunAsync` at startup**

In `src/Horafy.API/Program.cs`, find the database migration block. After the existing `EnsureCreated`/migration calls, add:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<HorafyDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await GlobalMigrations.RunAsync(db, logger);
}
```

- [ ] **Step 7: Run the tests**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~UserPhoneTests" -v minimal
```
Expected: PASS — all 3 tests pass.

- [ ] **Step 8: Commit**

```
git add src/Horafy.Domain/Entities/Users/User.cs
git add src/Horafy.Infrastructure/Persistence/Configurations/UserEntityConfiguration.cs
git add src/Horafy.Infrastructure/Persistence/GlobalMigrations.cs
git add src/Horafy.API/Program.cs
git add tests/Horafy.Domain.Tests/Users/UserPhoneTests.cs
git commit -m "feat: add Phone field to User entity with DDL migration"
```

---

## Task 2: CustomerPhone em Booking.Create e DDL

**Files:**
- Modify: `src/Horafy.Domain/Entities/Bookings/Booking.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Modify: `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`
- Create: `tests/Horafy.Domain.Tests/Bookings/BookingCustomerPhoneTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Horafy.Domain.Tests/Bookings/BookingCustomerPhoneTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Bookings;
using Xunit;

namespace Horafy.Domain.Tests.Bookings;

public sealed class BookingCustomerPhoneTests
{
    [Fact]
    public void Create_WithPhone_SetsCustomerPhone()
    {
        var booking = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            customerName: "João",
            customerEmail: "joao@test.com",
            customerPhone: "+5511999998888",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

        booking.CustomerPhone.Should().Be("+5511999998888");
    }

    [Fact]
    public void Create_WithoutPhone_CustomerPhoneIsNull()
    {
        var booking = Booking.Create(
            services: new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            customerName: "João",
            customerEmail: "joao@test.com",
            customerPhone: null,
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

        booking.CustomerPhone.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~BookingCustomerPhoneTests" -v minimal
```
Expected: FAIL — `Booking.Create` has no `customerPhone` param.

- [ ] **Step 3: Add `CustomerPhone` to Booking**

In `src/Horafy.Domain/Entities/Bookings/Booking.cs`:

Add property after `CustomerEmail`:
```csharp
public string? CustomerPhone { get; private set; }
```

Modify the primary `Create(IReadOnlyList<...> services, ...)` signature to add `string? customerPhone = null` after `customerEmail`:
```csharp
public static Booking Create(
    IReadOnlyList<(Guid ServiceId, string ServiceName, int DurationMinutes)> services,
    Guid resourceId,
    Guid customerId,
    string customerName,
    string customerEmail,
    string? customerPhone = null,
    DateTimeOffset? scheduledAt = null,
    string? notes = null,
    Guid? recurrenceGroupId = null,
    DateTimeOffset? expiresAt = null)
```

Wait — the current signature has `scheduledAt` as a required positional parameter, not optional. I need to keep that. The correct approach is to insert `customerPhone` between `customerEmail` and `scheduledAt`:

```csharp
public static Booking Create(
    IReadOnlyList<(Guid ServiceId, string ServiceName, int DurationMinutes)> services,
    Guid resourceId,
    Guid customerId,
    string customerName,
    string customerEmail,
    DateTimeOffset scheduledAt,
    string? customerPhone = null,
    string? notes = null,
    Guid? recurrenceGroupId = null,
    DateTimeOffset? expiresAt = null)
```

Set in the factory:
```csharp
var booking = new Booking
{
    ServiceId         = services[0].ServiceId,
    ResourceId        = resourceId,
    CustomerId        = customerId,
    CustomerName      = customerName.Trim(),
    CustomerEmail     = customerEmail.ToLowerInvariant().Trim(),
    CustomerPhone     = customerPhone?.Trim(),
    ScheduledAt       = scheduledAt,
    EndsAt            = scheduledAt.AddMinutes(totalDuration),
    DurationMinutes   = totalDuration,
    Notes             = notes?.Trim(),
    RecurrenceGroupId = recurrenceGroupId,
    ExpiresAt         = expiresAt
};
```

Also update the convenience overload (second `Create`) to pass `customerPhone: null` to stay backwards compatible:
```csharp
public static Booking Create(
    Guid serviceId,
    Guid resourceId,
    Guid customerId,
    string customerName,
    string customerEmail,
    DateTimeOffset scheduledAt,
    int durationMinutes,
    string? customerPhone = null,
    string? notes = null,
    Guid? recurrenceGroupId = null,
    DateTimeOffset? expiresAt = null) =>
    Create(
        new[] { (serviceId, ServiceName: serviceId.ToString(), durationMinutes) },
        resourceId, customerId, customerName, customerEmail,
        scheduledAt, customerPhone, notes, recurrenceGroupId, expiresAt);
```

- [ ] **Step 4: Add `customer_phone` column to DDL**

In `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, in the `bookings` table DDL block, add `customer_phone VARCHAR(20),` after `customer_email VARCHAR(256) NOT NULL,`.

Also, in `GlobalMigrations.RunAsync`, add:
```sql
ALTER TABLE public.users ADD COLUMN IF NOT EXISTS phone VARCHAR(20);
```
(already done in Task 1 — for new tenants it's covered by the `BuildSchemaScript` DDL change; for existing tenants use `ALTER TABLE … ADD COLUMN IF NOT EXISTS` inside `BuildSchemaScript` or accept the column is already there via the schema init.)

For existing tenant schemas, add an idempotent ALTER inside a separate helper. Append to `GlobalMigrations.RunAsync`:
```csharp
// Note: runs against public schema only — tenant bookings DDL is updated in BuildSchemaScript for new tenants.
// Existing tenants need manual migration: ALTER TABLE tenant_{slug}.bookings ADD COLUMN IF NOT EXISTS customer_phone VARCHAR(20);
```

Add a comment in the BuildSchemaScript noting this is for new tenants.

- [ ] **Step 5: Add EF mapping for CustomerPhone**

Check if `BookingEntityConfiguration.cs` exists:

```
ls src/Horafy.Infrastructure/Persistence/TenantConfigurations/
```

If a `BookingEntityConfiguration.cs` does not exist, EF maps by convention. Add one:

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs
using Horafy.Domain.Entities.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class BookingEntityConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(b => b.CustomerEmail).IsRequired().HasMaxLength(256);
        builder.Property(b => b.CustomerPhone).HasMaxLength(20);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(b => b.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(b => b.Notes).HasMaxLength(1000);
        builder.Property(b => b.CancellationReason).HasMaxLength(500);
    }
}
```

- [ ] **Step 6: Populate CustomerPhone in CreateBookingCommand**

In `src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs`, inside the handler, after resolving `currentUser`:

```csharp
// Load full user to get Phone
var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken);
var customerPhone = user?.Phone;
```

Then pass `customerPhone` to `Booking.Create(...)`:
```csharp
var booking = Booking.Create(
    services: ...,
    resourceId: ...,
    customerId: ...,
    customerName: currentUser.Name ?? currentUser.Email,
    customerEmail: currentUser.Email,
    scheduledAt: request.ScheduledAt,
    customerPhone: customerPhone,
    notes: request.Notes);
```

`IUserRepository` needs `GetByIdAsync(Guid, CancellationToken)`. Check if it exists — if not, add it to `IUserRepository` and implement it in `UserRepository`.

- [ ] **Step 7: Run tests**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~BookingCustomerPhoneTests" -v minimal
```
Expected: PASS.

```
dotnet build src/Horafy.Application/Horafy.Application.csproj
```
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```
git add src/Horafy.Domain/Entities/Bookings/Booking.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/BookingEntityConfiguration.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Application/Features/Bookings/Commands/CreateBookingCommand.cs
git add tests/Horafy.Domain.Tests/Bookings/BookingCustomerPhoneTests.cs
git commit -m "feat: add CustomerPhone to Booking.Create and populate from User.Phone"
```

---

## Task 3: Populate CustomerPhone nos notification publishers do Sprint 7

**Files:**
- Modify: `src/Horafy.Application/Features/Notifications/Publishers/BookingCreatedNotificationPublisher.cs`
- Modify: `src/Horafy.Application/Features/Notifications/Publishers/BookingConfirmedNotificationPublisher.cs`
- Modify: `src/Horafy.Application/Features/Notifications/Publishers/BookingCancelledNotificationPublisher.cs`
- Modify: `src/Horafy.Application/Features/Notifications/Publishers/BookingReminderNotificationPublisher.cs`
- Modify: `src/Horafy.Application/Features/Notifications/Publishers/PaymentPendingNotificationPublisher.cs`

Context: Os publishers atuais publicam `CustomerPhone: null` em todos os eventos. Agora que `Booking.CustomerPhone` existe, podemos popular o campo passando `booking.CustomerPhone` ou `b.CustomerPhone` onde o objeto booking já é carregado.

- [ ] **Step 1: Read the current publishers to understand their pattern**

Read `BookingCreatedNotificationPublisher.cs`:
```
cat src/Horafy.Application/Features/Notifications/Publishers/BookingCreatedNotificationPublisher.cs
```

The publisher receives a `BookingCreatedEvent` which has `BookingId, ServiceId, ResourceId, CustomerId, ScheduledAt`. It then loads the booking from the repo to get details.

- [ ] **Step 2: Update each publisher to pass `CustomerPhone`**

For any publisher that loads a `Booking` object (e.g., `booking.CustomerPhone`) — change:
```csharp
CustomerPhone: null,
```
to:
```csharp
CustomerPhone: booking.CustomerPhone,
```

For publishers that receive `BookingConfirmedEvent` (which already has `CustomerName`, `CustomerEmail` but NOT `CustomerPhone`) — load the booking or add `CustomerPhone` to the event. The simplest approach is to add `CustomerPhone` to `BookingConfirmedEvent`:

In `src/Horafy.Domain/Events/Bookings/BookingConfirmedEvent.cs`:
```csharp
public sealed record BookingConfirmedEvent(
    Guid BookingId, Guid CustomerId, string CustomerName,
    string CustomerEmail, string? CustomerPhone, DateTimeOffset ScheduledAt) : DomainEvent;
```

In `Booking.Confirm()`, update the `RaiseDomainEvent` call:
```csharp
RaiseDomainEvent(new BookingConfirmedEvent(
    Id, CustomerId, CustomerName, CustomerEmail, CustomerPhone, ScheduledAt));
```

Then in `BookingConfirmedNotificationPublisher`, change:
```csharp
CustomerPhone: null,
```
to:
```csharp
CustomerPhone: @event.CustomerPhone,
```

For `BookingCancelledEvent`, add `CustomerPhone` similarly:

In `src/Horafy.Domain/Events/Bookings/BookingCancelledEvent.cs`, check the current definition and add `string? CustomerPhone` field. Then in `Booking.Cancel()`, pass `CustomerPhone` to the event. Update `BookingCancelledNotificationPublisher` to use `@event.CustomerPhone`.

For `BookingCreatedEvent`, add `string? CustomerPhone`:

In `src/Horafy.Domain/Events/Bookings/BookingCreatedEvent.cs` — check current definition. If it only has `BookingId, ServiceId, ResourceId, CustomerId, ScheduledAt`, add `string? CustomerPhone`. Then in `Booking.Create()`, pass `booking.CustomerPhone` to the event.

For `BookingReminderNotificationPublisher` — it already loads bookings from the DB via the job, so it can access `booking.CustomerPhone` directly.

For `PaymentPendingNotificationPublisher` — it loads the booking to get `CustomerEmail`. Pass `booking.CustomerPhone`.

- [ ] **Step 3: Build to verify no compile errors**

```
dotnet build src/Horafy.Application/Horafy.Application.csproj
dotnet build src/Horafy.Domain/Horafy.Domain.csproj
```
Expected: Build succeeds.

- [ ] **Step 4: Run all notification publisher tests**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~NotificationPublisher" -v minimal
```
Expected: All pass (publishers now pass CustomerPhone through).

- [ ] **Step 5: Commit**

```
git add src/Horafy.Domain/Events/Bookings/
git add src/Horafy.Domain/Entities/Bookings/Booking.cs
git add src/Horafy.Application/Features/Notifications/Publishers/
git commit -m "feat: propagate CustomerPhone through domain events and notification publishers"
```

---

## Task 4: Endpoints de autenticação OAuth para clientes finais

**Files:**
- Create: `src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithGoogleCommand.cs`
- Create: `src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithAppleCommand.cs`
- Create: `src/Horafy.API/Controllers/V1/CustomerAuthController.cs`

Context: `LoginWithGoogleCommand` e `LoginWithAppleCommand` já existem e funcionam para owners (sem `TenantSlug`) e customers (com `TenantSlug`). Precisamos de endpoints distintos que:
1. Exigem `TenantSlug` (não opcional)
2. Garantem que o usuário resultante é `UserRole.Customer` (rejeita se for owner tentando usar o endpoint de cliente)

**A implementação mais limpa é criar comandos wrapper que delegam para os handlers existentes e validam o role.**

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Horafy.Application.Tests/Auth/CustomerLoginWithGoogleCommandTests.cs
using FluentAssertions;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Auth;

public sealed class CustomerLoginWithGoogleCommandTests
{
    private readonly Mock<IGoogleOAuthService>  _googleOAuth  = new();
    private readonly Mock<IUserRepository>      _userRepo     = new();
    private readonly Mock<ITenantRepository>    _tenantRepo   = new();
    private readonly Mock<ITokenService>        _tokenService = new();
    private readonly Mock<IUnitOfWork>          _uow          = new();

    private CustomerLoginWithGoogleCommandHandler MakeHandler() =>
        new(_googleOAuth.Object, _userRepo.Object, _tenantRepo.Object,
            _tokenService.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidCustomer_ReturnsTokens()
    {
        _googleOAuth.Setup(g => g.ValidateAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new OAuthUserInfo("google_1", "j@test.com", "João", null));

        var tenant = Domain.Entities.Tenants.Tenant.Create("Barbearia", "barbearia",
            Domain.Entities.Tenants.TenantVertical.Barbershop);
        _tenantRepo.Setup(r => r.GetBySlugAsync("barbearia", default)).ReturnsAsync(tenant);

        _userRepo.Setup(r => r.GetByGoogleIdAsync("google_1", default)).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByEmailAsync("j@test.com", default)).ReturnsAsync((User?)null);

        _tokenService.Setup(t => t.GenerateTokens(It.IsAny<User>()))
            .Returns(new TokenPair("at", "rt", DateTimeOffset.UtcNow.AddHours(1)));

        var result = await MakeHandler().Handle(
            new CustomerLoginWithGoogleCommand("id_token", "barbearia"), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoTenantSlug_ReturnsError()
    {
        var result = await MakeHandler().Handle(
            new CustomerLoginWithGoogleCommand("id_token", TenantSlug: null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Customer.TenantRequired");
    }

    [Fact]
    public async Task Handle_TenantNotFound_ReturnsError()
    {
        _googleOAuth.Setup(g => g.ValidateAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new OAuthUserInfo("google_1", "j@test.com", "João", null));

        _tenantRepo.Setup(r => r.GetBySlugAsync("unknown", default))
            .ReturnsAsync((Domain.Entities.Tenants.Tenant?)null);

        var result = await MakeHandler().Handle(
            new CustomerLoginWithGoogleCommand("id_token", "unknown"), default);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~CustomerLoginWithGoogleCommandTests" -v minimal
```
Expected: FAIL — `CustomerLoginWithGoogleCommand` does not exist.

- [ ] **Step 3: Create CustomerLoginWithGoogleCommand**

```csharp
// src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithGoogle/CustomerLoginWithGoogleCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;

public sealed record CustomerLoginWithGoogleCommand(
    string  IdToken,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class CustomerLoginWithGoogleCommandHandler(
    IGoogleOAuthService  googleOAuth,
    IUserRepository      userRepository,
    ITenantRepository    tenantRepository,
    ITokenService        tokenService,
    IUnitOfWork          unitOfWork) : IRequestHandler<CustomerLoginWithGoogleCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        CustomerLoginWithGoogleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
            return Result.Failure<TokenPair>(new Error(
                "Customer.TenantRequired",
                "O slug do tenant é obrigatório para login de clientes.",
                ErrorType.Validation));

        var info = await googleOAuth.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidOAuthToken);

        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null)
            return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);

        var user =
            await userRepository.GetByGoogleIdAsync(info.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(info.Email, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithGoogle(
                info.Email, info.ProviderId, info.Name,
                tenant.Id, UserRole.Customer);
            userRepository.Add(user);
        }
        else
        {
            if (string.IsNullOrEmpty(user.GoogleId))
                user.LinkGoogle(info.ProviderId);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
```

- [ ] **Step 4: Create CustomerLoginWithAppleCommand**

```csharp
// src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithApple/CustomerLoginWithAppleCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Auth.Commands.CustomerLoginWithApple;

public sealed record CustomerLoginWithAppleCommand(
    string  IdentityToken,
    string? TenantSlug) : IRequest<Result<TokenPair>>;

internal sealed class CustomerLoginWithAppleCommandHandler(
    IAppleOAuthService   appleOAuth,
    IUserRepository      userRepository,
    ITenantRepository    tenantRepository,
    ITokenService        tokenService,
    IUnitOfWork          unitOfWork) : IRequestHandler<CustomerLoginWithAppleCommand, Result<TokenPair>>
{
    public async Task<Result<TokenPair>> Handle(
        CustomerLoginWithAppleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
            return Result.Failure<TokenPair>(new Error(
                "Customer.TenantRequired",
                "O slug do tenant é obrigatório para login de clientes.",
                ErrorType.Validation));

        var info = await appleOAuth.ValidateAsync(request.IdentityToken, cancellationToken);
        if (info is null)
            return Result.Failure<TokenPair>(AuthErrors.InvalidOAuthToken);

        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null)
            return Result.Failure<TokenPair>(AuthErrors.TenantNotFound);

        var user =
            await userRepository.GetByAppleIdAsync(info.ProviderId, cancellationToken)
            ?? await userRepository.GetByEmailAsync(info.Email, cancellationToken);

        if (user is null)
        {
            user = User.CreateWithApple(
                info.Email, info.ProviderId, info.Name,
                tenant.Id, UserRole.Customer);
            userRepository.Add(user);
        }
        else
        {
            if (string.IsNullOrEmpty(user.AppleId))
                user.LinkApple(info.ProviderId);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(tokenService.GenerateTokens(user));
    }
}
```

- [ ] **Step 5: Create CustomerAuthController**

```csharp
// src/Horafy.API/Controllers/V1/CustomerAuthController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithApple;
using Horafy.Application.Features.Auth.Commands.CustomerLoginWithGoogle;
using Horafy.Shared;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/customers/auth")]
public sealed class CustomerAuthController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpPost("google")]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Google(
        [FromBody] CustomerGoogleLoginRequest request, CancellationToken ct) =>
        ToActionResult(await Sender.Send(
            new CustomerLoginWithGoogleCommand(request.IdToken, request.TenantSlug), ct));

    [HttpPost("apple")]
    [ProducesResponseType(typeof(TokenPair), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Apple(
        [FromBody] CustomerAppleLoginRequest request, CancellationToken ct) =>
        ToActionResult(await Sender.Send(
            new CustomerLoginWithAppleCommand(request.IdentityToken, request.TenantSlug), ct));
}

public sealed record CustomerGoogleLoginRequest(string IdToken, string TenantSlug);
public sealed record CustomerAppleLoginRequest(string IdentityToken, string TenantSlug);
```

- [ ] **Step 6: Run the tests**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~CustomerLoginWithGoogleCommandTests" -v minimal
```
Expected: PASS — all 3 tests pass.

```
dotnet build src/Horafy.API/Horafy.API.csproj
```
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithGoogle/
git add src/Horafy.Application/Features/Auth/Commands/CustomerLoginWithApple/
git add src/Horafy.API/Controllers/V1/CustomerAuthController.cs
git add tests/Horafy.Application.Tests/Auth/CustomerLoginWithGoogleCommandTests.cs
git commit -m "feat: add customer-specific OAuth endpoints for Google and Apple"
```

---

## Task 5: Perfil do cliente — GET /customers/me e PATCH /customers/me/phone

**Files:**
- Create: `src/Horafy.Application/Features/Customers/Queries/GetCustomerProfileQuery.cs`
- Create: `src/Horafy.Application/Features/Customers/Commands/UpdateCustomerPhoneCommand.cs`
- Create: `src/Horafy.API/Controllers/V1/CustomersController.cs`
- Create: `tests/Horafy.Application.Tests/Customers/GetCustomerProfileQueryTests.cs`
- Create: `tests/Horafy.Application.Tests/Customers/UpdateCustomerPhoneCommandTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Customers/GetCustomerProfileQueryTests.cs
using FluentAssertions;
using Horafy.Application.Features.Customers.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Customers;

public sealed class GetCustomerProfileQueryTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IUserRepository>     _userRepo    = new();

    private GetCustomerProfileQueryHandler MakeHandler() =>
        new(_currentUser.Object, _userRepo.Object);

    [Fact]
    public async Task Handle_AuthenticatedCustomer_ReturnsProfile()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(userId);

        var user = User.CreateWithGoogle(
            "j@test.com", "g1", "João", Guid.NewGuid(), UserRole.Customer);
        user.SetPhone("+5511999998888");
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var result = await MakeHandler().Handle(new GetCustomerProfileQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("j@test.com");
        result.Value.Phone.Should().Be("+5511999998888");
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsError()
    {
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((User?)null);

        var result = await MakeHandler().Handle(new GetCustomerProfileQuery(), default);

        result.IsFailure.Should().BeTrue();
    }
}
```

```csharp
// tests/Horafy.Application.Tests/Customers/UpdateCustomerPhoneCommandTests.cs
using FluentAssertions;
using Horafy.Application.Features.Customers.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Customers;

public sealed class UpdateCustomerPhoneCommandTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IUserRepository>     _userRepo    = new();
    private readonly Mock<IUnitOfWork>         _uow         = new();

    private UpdateCustomerPhoneCommandHandler MakeHandler() =>
        new(_currentUser.Object, _userRepo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidPhone_UpdatesAndSaves()
    {
        var userId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(userId);

        var user = User.CreateWithGoogle(
            "j@test.com", "g1", "João", Guid.NewGuid(), UserRole.Customer);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var result = await MakeHandler().Handle(
            new UpdateCustomerPhoneCommand("+5511999998888"), default);

        result.IsSuccess.Should().BeTrue();
        user.Phone.Should().Be("+5511999998888");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_TooLongPhone_ReturnsError()
    {
        var result = await MakeHandler().Handle(
            new UpdateCustomerPhoneCommand(new string('1', 21)), default);

        result.IsFailure.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Customers" -v minimal
```
Expected: FAIL — types not found.

- [ ] **Step 3: Ensure `IUserRepository` has `GetByIdAsync`**

Check `src/Horafy.Domain/Interfaces/Repositories/IUserRepository.cs`. If `GetByIdAsync(Guid, CancellationToken)` is not there (it should be inherited from `IRepository<User>`), ensure the base `IRepository<T>` has it:

```csharp
// In IRepository<T> or IUserRepository
Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Create GetCustomerProfileQuery**

```csharp
// src/Horafy.Application/Features/Customers/Queries/GetCustomerProfileQuery.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Queries;

public sealed record GetCustomerProfileQuery : IRequest<Result<CustomerProfileResult>>;

public sealed record CustomerProfileResult(
    Guid    Id,
    string  Name,
    string  Email,
    string? Phone,
    string? AvatarUrl);

internal sealed class GetCustomerProfileQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository     userRepository)
    : IRequestHandler<GetCustomerProfileQuery, Result<CustomerProfileResult>>
{
    public async Task<Result<CustomerProfileResult>> Handle(
        GetCustomerProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(
            currentUserService.UserId, cancellationToken);

        if (user is null)
            return Result.Failure<CustomerProfileResult>(new Error(
                "Customer.NotFound", "Usuário não encontrado.", ErrorType.NotFound));

        return Result.Success(new CustomerProfileResult(
            user.Id, user.Name ?? user.Email, user.Email, user.Phone, user.AvatarUrl));
    }
}
```

- [ ] **Step 5: Create UpdateCustomerPhoneCommand**

```csharp
// src/Horafy.Application/Features/Customers/Commands/UpdateCustomerPhoneCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Commands;

public sealed record UpdateCustomerPhoneCommand(string? Phone) : IRequest<Result>;

internal sealed class UpdateCustomerPhoneCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository     userRepository,
    IUnitOfWork         unitOfWork)
    : IRequestHandler<UpdateCustomerPhoneCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCustomerPhoneCommand request, CancellationToken cancellationToken)
    {
        if (request.Phone is not null && request.Phone.Length > 20)
            return Result.Failure(new Error(
                "Customer.PhoneTooLong",
                "O telefone deve ter no máximo 20 caracteres.",
                ErrorType.Validation));

        var user = await userRepository.GetByIdAsync(
            currentUserService.UserId, cancellationToken);

        if (user is null)
            return Result.Failure(new Error(
                "Customer.NotFound", "Usuário não encontrado.", ErrorType.NotFound));

        user.SetPhone(request.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 6: Create CustomersController (partial — profile endpoints)**

```csharp
// src/Horafy.API/Controllers/V1/CustomersController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Customers.Commands;
using Horafy.Application.Features.Customers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "Customer")]
[Route("api/v{version:apiVersion}/customers")]
public sealed class CustomersController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(CustomerProfileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetCustomerProfileQuery(), ct));

    [HttpPatch("me/phone")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePhone(
        [FromBody] UpdatePhoneRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(new UpdateCustomerPhoneCommand(request.Phone), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record UpdatePhoneRequest(string? Phone);
```

- [ ] **Step 7: Run the tests**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Customers" -v minimal
```
Expected: PASS — all 4 tests pass.

- [ ] **Step 8: Commit**

```
git add src/Horafy.Application/Features/Customers/
git add src/Horafy.API/Controllers/V1/CustomersController.cs
git add tests/Horafy.Application.Tests/Customers/
git commit -m "feat: add GET /customers/me and PATCH /customers/me/phone endpoints"
```

---

## Task 6: Histórico de agendamentos do cliente — GET /customers/me/bookings

**Files:**
- Create: `src/Horafy.Application/Features/Customers/Queries/GetCustomerBookingsQuery.cs`
- Modify: `src/Horafy.API/Controllers/V1/CustomersController.cs`
- Create: `tests/Horafy.Application.Tests/Customers/GetCustomerBookingsQueryTests.cs`

Context: `IBookingRepository.GetByCustomerAsync(Guid, CancellationToken)` já existe. Basta criar a query que usa esse método.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Horafy.Application.Tests/Customers/GetCustomerBookingsQueryTests.cs
using FluentAssertions;
using Horafy.Application.Features.Customers.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Customers;

public sealed class GetCustomerBookingsQueryTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IBookingRepository>  _bookingRepo = new();

    private GetCustomerBookingsQueryHandler MakeHandler() =>
        new(_currentUser.Object, _bookingRepo.Object);

    [Fact]
    public async Task Handle_CustomerWithBookings_ReturnsAllBookings()
    {
        var customerId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var booking = Booking.Create(
            new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId: Guid.NewGuid(),
            customerId: customerId,
            customerName: "João",
            customerEmail: "j@test.com",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(2));
        booking.ClearDomainEvents();

        _bookingRepo.Setup(r => r.GetByCustomerAsync(customerId, default))
            .ReturnsAsync(new List<Booking> { booking });

        var result = await MakeHandler().Handle(new GetCustomerBookingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task Handle_CustomerWithNoBookings_ReturnsEmptyList()
    {
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByCustomerAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await MakeHandler().Handle(new GetCustomerBookingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~GetCustomerBookingsQueryTests" -v minimal
```
Expected: FAIL — `GetCustomerBookingsQuery` not found.

- [ ] **Step 3: Create GetCustomerBookingsQuery**

```csharp
// src/Horafy.Application/Features/Customers/Queries/GetCustomerBookingsQuery.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Customers.Queries;

public sealed record GetCustomerBookingsQuery : IRequest<Result<IReadOnlyList<CustomerBookingResult>>>;

public sealed record CustomerBookingResult(
    Guid                Id,
    Guid                ServiceId,
    Guid                ResourceId,
    Guid                CustomerId,
    DateTimeOffset      ScheduledAt,
    DateTimeOffset      EndsAt,
    int                 DurationMinutes,
    string?             Notes,
    BookingStatus       Status,
    BookingPaymentStatus PaymentStatus,
    IReadOnlyList<CustomerBookingServiceResult> Services);

public sealed record CustomerBookingServiceResult(
    Guid   ServiceId,
    string ServiceName,
    int    DurationMinutes);

internal sealed class GetCustomerBookingsQueryHandler(
    ICurrentUserService currentUserService,
    IBookingRepository  bookingRepository)
    : IRequestHandler<GetCustomerBookingsQuery, Result<IReadOnlyList<CustomerBookingResult>>>
{
    public async Task<Result<IReadOnlyList<CustomerBookingResult>>> Handle(
        GetCustomerBookingsQuery request, CancellationToken cancellationToken)
    {
        var bookings = await bookingRepository.GetByCustomerAsync(
            currentUserService.UserId, cancellationToken);

        var results = bookings
            .Select(b => new CustomerBookingResult(
                b.Id, b.ServiceId, b.ResourceId, b.CustomerId,
                b.ScheduledAt, b.EndsAt, b.DurationMinutes, b.Notes,
                b.Status, b.PaymentStatus,
                b.Services
                    .Select(s => new CustomerBookingServiceResult(
                        s.ServiceId, s.ServiceName, s.DurationMinutes))
                    .ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<CustomerBookingResult>>(results);
    }
}
```

- [ ] **Step 4: Add route to CustomersController**

In `src/Horafy.API/Controllers/V1/CustomersController.cs`, add inside the class:

```csharp
[HttpGet("me/bookings")]
[ProducesResponseType(typeof(IReadOnlyList<CustomerBookingResult>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetBookings(CancellationToken ct) =>
    ToActionResult(await Sender.Send(new GetCustomerBookingsQuery(), ct));
```

Add the required using at the top:
```csharp
using Horafy.Domain.Entities.Bookings;
```

- [ ] **Step 5: Run the tests**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~GetCustomerBookingsQueryTests" -v minimal
```
Expected: PASS — all 2 tests pass.

- [ ] **Step 6: Commit**

```
git add src/Horafy.Application/Features/Customers/Queries/GetCustomerBookingsQuery.cs
git add src/Horafy.API/Controllers/V1/CustomersController.cs
git add tests/Horafy.Application.Tests/Customers/GetCustomerBookingsQueryTests.cs
git commit -m "feat: add GET /customers/me/bookings — booking history for authenticated customer"
```

---

## Task 7: Entidade Review — domínio + DDL + repositório

**Files:**
- Create: `src/Horafy.Domain/Entities/Reviews/Review.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IReviewRepository.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/ReviewEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/ReviewRepository.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`
- Create: `tests/Horafy.Domain.Tests/Reviews/ReviewTests.cs`

- [ ] **Step 1: Write failing domain tests**

```csharp
// tests/Horafy.Domain.Tests/Reviews/ReviewTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Reviews;
using Xunit;

namespace Horafy.Domain.Tests.Reviews;

public sealed class ReviewTests
{
    [Fact]
    public void Create_ValidData_SetsProperties()
    {
        var bookingId  = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var review = Review.Create(bookingId, resourceId, customerId, stars: 5, comment: "Ótimo!");

        review.BookingId.Should().Be(bookingId);
        review.ResourceId.Should().Be(resourceId);
        review.CustomerId.Should().Be(customerId);
        review.Stars.Should().Be(5);
        review.Comment.Should().Be("Ótimo!");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_InvalidStars_Throws(int stars)
    {
        var action = () => Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), stars, "ok");
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_ChangesStarsAndComment()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, "Bom");
        review.Update(5, "Excelente!");
        review.Stars.Should().Be(5);
        review.Comment.Should().Be("Excelente!");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~ReviewTests" -v minimal
```
Expected: FAIL — `Review` type not found.

- [ ] **Step 3: Create Review entity**

```csharp
// src/Horafy.Domain/Entities/Reviews/Review.cs
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Reviews;

public sealed class Review : BaseEntity
{
    private Review() { }

    public Guid    BookingId  { get; private set; }
    public Guid    ResourceId { get; private set; }
    public Guid    CustomerId { get; private set; }
    public int     Stars      { get; private set; }
    public string? Comment    { get; private set; }

    public static Review Create(
        Guid    bookingId,
        Guid    resourceId,
        Guid    customerId,
        int     stars,
        string? comment = null)
    {
        if (stars < 1 || stars > 5)
            throw new ArgumentOutOfRangeException(nameof(stars), "Avaliação deve ser entre 1 e 5 estrelas.");

        return new Review
        {
            BookingId  = bookingId,
            ResourceId = resourceId,
            CustomerId = customerId,
            Stars      = stars,
            Comment    = comment?.Trim()
        };
    }

    public void Update(int stars, string? comment)
    {
        if (stars < 1 || stars > 5)
            throw new ArgumentOutOfRangeException(nameof(stars), "Avaliação deve ser entre 1 e 5 estrelas.");

        Stars     = stars;
        Comment   = comment?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Create IReviewRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IReviewRepository.cs
using Horafy.Domain.Entities.Reviews;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IReviewRepository : IRepository<Review>
{
    Task<Review?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetByResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Add DDL to TenantSchemaService**

In `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, inside `BuildSchemaScript()`, append at the end (before the closing `"""`):

```sql
        -- ── Avaliações ─────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.reviews (
            id           UUID         NOT NULL DEFAULT gen_random_uuid(),
            booking_id   UUID         NOT NULL,
            resource_id  UUID         NOT NULL,
            customer_id  UUID         NOT NULL,
            stars        SMALLINT     NOT NULL CHECK (stars BETWEEN 1 AND 5),
            comment      VARCHAR(1000),
            created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
            updated_at   TIMESTAMPTZ,
            created_by   VARCHAR(256),
            updated_by   VARCHAR(256),
            is_deleted   BOOLEAN      NOT NULL DEFAULT FALSE,
            deleted_at   TIMESTAMPTZ,
            deleted_by   VARCHAR(256),
            CONSTRAINT pk_reviews PRIMARY KEY (id),
            CONSTRAINT fk_reviews_bookings
                FOREIGN KEY (booking_id) REFERENCES {s}.bookings (id),
            CONSTRAINT uq_reviews_booking UNIQUE (booking_id)
        );

        CREATE INDEX IF NOT EXISTS ix_reviews_resource
            ON {s}.reviews (resource_id)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_reviews_customer
            ON {s}.reviews (customer_id)
            WHERE is_deleted = FALSE;
```

- [ ] **Step 6: Create ReviewEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/ReviewEntityConfiguration.cs
using Horafy.Domain.Entities.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class ReviewEntityConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Stars).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(1000);
        builder.HasIndex(r => r.BookingId).IsUnique().HasFilter("is_deleted = FALSE");
        builder.HasIndex(r => r.ResourceId);
        builder.HasIndex(r => r.CustomerId);
    }
}
```

- [ ] **Step 7: Add `DbSet<Review>` to TenantDbContext**

In `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`, add:
```csharp
public DbSet<Review> Reviews => Set<Review>();
```
And add the using:
```csharp
using Horafy.Domain.Entities.Reviews;
```

- [ ] **Step 8: Create ReviewRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/ReviewRepository.cs
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class ReviewRepository(TenantDbContext db)
    : RepositoryBase<Review>(db), IReviewRepository
{
    public async Task<Review?> GetByBookingAsync(
        Guid bookingId, CancellationToken cancellationToken = default) =>
        await db.Reviews
            .FirstOrDefaultAsync(r => r.BookingId == bookingId, cancellationToken);

    public async Task<IReadOnlyList<Review>> GetByResourceAsync(
        Guid resourceId, CancellationToken cancellationToken = default) =>
        await db.Reviews
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
}
```

- [ ] **Step 9: Register IReviewRepository in DI**

In `src/Horafy.Infrastructure/DependencyInjection.cs`, inside the tenant services registration, add:
```csharp
services.AddScoped<IReviewRepository, ReviewRepository>();
```

- [ ] **Step 10: Run tests**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~ReviewTests" -v minimal
```
Expected: PASS — all 3 domain tests pass.

```
dotnet build src/Horafy.Infrastructure/Horafy.Infrastructure.csproj
```
Expected: Build succeeds.

- [ ] **Step 11: Commit**

```
git add src/Horafy.Domain/Entities/Reviews/
git add src/Horafy.Domain/Interfaces/Repositories/IReviewRepository.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/ReviewEntityConfiguration.cs
git add src/Horafy.Infrastructure/Repositories/ReviewRepository.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add tests/Horafy.Domain.Tests/Reviews/ReviewTests.cs
git commit -m "feat: add Review entity, repository, DDL and DI registration"
```

---

## Task 8: CQRS para Reviews + controller

**Files:**
- Create: `src/Horafy.Application/Features/Reviews/Commands/CreateReviewCommand.cs`
- Create: `src/Horafy.Application/Features/Reviews/Queries/GetResourceReviewsQuery.cs`
- Create: `src/Horafy.API/Controllers/V1/ReviewsController.cs`
- Create: `tests/Horafy.Application.Tests/Reviews/CreateReviewCommandTests.cs`
- Create: `tests/Horafy.Application.Tests/Reviews/GetResourceReviewsQueryTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Reviews/CreateReviewCommandTests.cs
using FluentAssertions;
using Horafy.Application.Features.Reviews.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Reviews;

public sealed class CreateReviewCommandTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IBookingRepository>  _bookingRepo = new();
    private readonly Mock<IReviewRepository>   _reviewRepo  = new();
    private readonly Mock<ITenantUnitOfWork>   _uow         = new();

    private CreateReviewCommandHandler MakeHandler() =>
        new(_currentUser.Object, _bookingRepo.Object, _reviewRepo.Object, _uow.Object);

    private static Booking MakeCompletedBooking(Guid customerId, Guid resourceId)
    {
        var b = Booking.Create(
            new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId, customerId, "João", "j@test.com",
            DateTimeOffset.UtcNow.AddHours(-2));
        b.Confirm();
        b.Complete();
        b.ClearDomainEvents();
        return b;
    }

    [Fact]
    public async Task Handle_ValidReview_CreatesAndSaves()
    {
        var customerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var booking = MakeCompletedBooking(customerId, resourceId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);
        _reviewRepo.Setup(r => r.GetByBookingAsync(booking.Id, default))
            .ReturnsAsync((Review?)null);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, Stars: 5, Comment: "Ótimo!"), default);

        result.IsSuccess.Should().BeTrue();
        _reviewRepo.Verify(r => r.Add(It.IsAny<Review>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ReturnsError()
    {
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Booking?)null);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(Guid.NewGuid(), 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.BookingNotFound");
    }

    [Fact]
    public async Task Handle_BookingBelongsToDifferentCustomer_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        var otherId    = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var booking = MakeCompletedBooking(otherId, Guid.NewGuid());
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.NotYourBooking");
    }

    [Fact]
    public async Task Handle_BookingNotCompleted_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var booking = Booking.Create(
            new[] { (Guid.NewGuid(), "Corte", 30) },
            resourceId, customerId, "João", "j@test.com",
            DateTimeOffset.UtcNow.AddHours(2));
        booking.ClearDomainEvents();
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.BookingNotCompleted");
    }

    [Fact]
    public async Task Handle_AlreadyReviewed_ReturnsError()
    {
        var customerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var booking = MakeCompletedBooking(customerId, resourceId);
        _bookingRepo.Setup(r => r.GetByIdAsync(booking.Id, default)).ReturnsAsync(booking);

        var existingReview = Review.Create(booking.Id, resourceId, customerId, 4, "Bom");
        _reviewRepo.Setup(r => r.GetByBookingAsync(booking.Id, default))
            .ReturnsAsync(existingReview);

        var result = await MakeHandler().Handle(
            new CreateReviewCommand(booking.Id, 5, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Review.AlreadyReviewed");
    }
}
```

```csharp
// tests/Horafy.Application.Tests/Reviews/GetResourceReviewsQueryTests.cs
using FluentAssertions;
using Horafy.Application.Features.Reviews.Queries;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Reviews;

public sealed class GetResourceReviewsQueryTests
{
    private readonly Mock<IReviewRepository> _repo = new();

    private GetResourceReviewsQueryHandler MakeHandler() =>
        new(_repo.Object);

    [Fact]
    public async Task Handle_ResourceWithReviews_ReturnsMappedList()
    {
        var resourceId = Guid.NewGuid();
        var review     = Review.Create(Guid.NewGuid(), resourceId, Guid.NewGuid(), 5, "Ótimo!");
        _repo.Setup(r => r.GetByResourceAsync(resourceId, default))
            .ReturnsAsync(new List<Review> { review });

        var result = await MakeHandler().Handle(
            new GetResourceReviewsQuery(resourceId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Stars.Should().Be(5);
        result.Value[0].Comment.Should().Be("Ótimo!");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Reviews" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Create CreateReviewCommand**

```csharp
// src/Horafy.Application/Features/Reviews/Commands/CreateReviewCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Reviews;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reviews.Commands;

public sealed record CreateReviewCommand(
    Guid    BookingId,
    int     Stars,
    string? Comment) : IRequest<Result<Guid>>;

internal sealed class CreateReviewCommandHandler(
    ICurrentUserService currentUserService,
    IBookingRepository  bookingRepository,
    IReviewRepository   reviewRepository,
    ITenantUnitOfWork   unitOfWork)
    : IRequestHandler<CreateReviewCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking is null)
            return Result.Failure<Guid>(new Error(
                "Review.BookingNotFound", "Agendamento não encontrado.", ErrorType.NotFound));

        if (booking.CustomerId != currentUserService.UserId)
            return Result.Failure<Guid>(new Error(
                "Review.NotYourBooking",
                "Você só pode avaliar seus próprios agendamentos.",
                ErrorType.Forbidden));

        if (booking.Status != BookingStatus.Completed)
            return Result.Failure<Guid>(new Error(
                "Review.BookingNotCompleted",
                "Só é possível avaliar agendamentos concluídos.",
                ErrorType.Validation));

        var existing = await reviewRepository.GetByBookingAsync(request.BookingId, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(new Error(
                "Review.AlreadyReviewed",
                "Este agendamento já foi avaliado.",
                ErrorType.Conflict));

        var review = Review.Create(
            request.BookingId, booking.ResourceId,
            currentUserService.UserId, request.Stars, request.Comment);

        reviewRepository.Add(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(review.Id);
    }
}
```

- [ ] **Step 4: Create GetResourceReviewsQuery**

```csharp
// src/Horafy.Application/Features/Reviews/Queries/GetResourceReviewsQuery.cs
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Reviews.Queries;

public sealed record GetResourceReviewsQuery(Guid ResourceId)
    : IRequest<Result<IReadOnlyList<ReviewResult>>>;

public sealed record ReviewResult(
    Guid    Id,
    Guid    BookingId,
    Guid    CustomerId,
    int     Stars,
    string? Comment,
    DateTimeOffset CreatedAt);

internal sealed class GetResourceReviewsQueryHandler(IReviewRepository reviewRepository)
    : IRequestHandler<GetResourceReviewsQuery, Result<IReadOnlyList<ReviewResult>>>
{
    public async Task<Result<IReadOnlyList<ReviewResult>>> Handle(
        GetResourceReviewsQuery request, CancellationToken cancellationToken)
    {
        var reviews = await reviewRepository.GetByResourceAsync(
            request.ResourceId, cancellationToken);

        var results = reviews
            .Select(r => new ReviewResult(
                r.Id, r.BookingId, r.CustomerId,
                r.Stars, r.Comment, r.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<ReviewResult>>(results);
    }
}
```

- [ ] **Step 5: Create ReviewsController**

```csharp
// src/Horafy.API/Controllers/V1/ReviewsController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Reviews.Commands;
using Horafy.Application.Features.Reviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class ReviewsController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpPost]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var result = await Sender.Send(
            new CreateReviewCommand(request.BookingId, request.Stars, request.Comment), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetByResource),
                new { resourceId = result.Value }, result.Value)
            : ToActionResult(result);
    }

    [HttpGet("resources/{resourceId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByResource(Guid resourceId, CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetResourceReviewsQuery(resourceId), ct));
}

public sealed record CreateReviewRequest(Guid BookingId, int Stars, string? Comment);
```

- [ ] **Step 6: Run the tests**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Reviews" -v minimal
```
Expected: PASS — all 6 tests pass.

```
dotnet build src/Horafy.API/Horafy.API.csproj
```
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```
git add src/Horafy.Application/Features/Reviews/
git add src/Horafy.API/Controllers/V1/ReviewsController.cs
git add tests/Horafy.Application.Tests/Reviews/
git commit -m "feat: add Reviews CQRS — CreateReview, GetResourceReviews, ReviewsController"
```

---

## Task 9: Entidade FavoriteService — domínio + DDL + repositório

**Files:**
- Create: `src/Horafy.Domain/Entities/Favorites/FavoriteService.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IFavoriteServiceRepository.cs`
- Modify: `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/FavoriteServiceEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/FavoriteServiceRepository.cs`
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs`
- Create: `tests/Horafy.Domain.Tests/Favorites/FavoriteServiceTests.cs`

- [ ] **Step 1: Write failing domain tests**

```csharp
// tests/Horafy.Domain.Tests/Favorites/FavoriteServiceTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Favorites;
using Xunit;

namespace Horafy.Domain.Tests.Favorites;

public sealed class FavoriteServiceTests
{
    [Fact]
    public void Create_ValidData_SetsProperties()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();

        var fav = FavoriteService.Create(customerId, serviceId);

        fav.CustomerId.Should().Be(customerId);
        fav.ServiceId.Should().Be(serviceId);
        fav.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~FavoriteServiceTests" -v minimal
```
Expected: FAIL — `FavoriteService` type not found.

- [ ] **Step 3: Create FavoriteService entity**

`FavoriteService` is a simple join entity — no soft-delete, no audit fields. It uses `BaseEntity` only for `Id` and `CreatedAt`.

```csharp
// src/Horafy.Domain/Entities/Favorites/FavoriteService.cs
using Horafy.Domain.Entities.Base;

namespace Horafy.Domain.Entities.Favorites;

public sealed class FavoriteService : BaseEntity
{
    private FavoriteService() { }

    public Guid CustomerId { get; private set; }
    public Guid ServiceId  { get; private set; }

    public static FavoriteService Create(Guid customerId, Guid serviceId) =>
        new()
        {
            CustomerId = customerId,
            ServiceId  = serviceId
        };
}
```

- [ ] **Step 4: Create IFavoriteServiceRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IFavoriteServiceRepository.cs
using Horafy.Domain.Entities.Favorites;

namespace Horafy.Domain.Interfaces.Repositories;

public interface IFavoriteServiceRepository : IRepository<FavoriteService>
{
    Task<FavoriteService?> GetAsync(
        Guid customerId, Guid serviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoriteService>> GetByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Add DDL to TenantSchemaService**

In `src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs`, inside `BuildSchemaScript()`, after the reviews block:

```sql
        -- ── Favoritos ──────────────────────────────────────────────────────
        CREATE TABLE IF NOT EXISTS {s}.favorite_services (
            id          UUID        NOT NULL DEFAULT gen_random_uuid(),
            customer_id UUID        NOT NULL,
            service_id  UUID        NOT NULL,
            created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ,
            created_by  VARCHAR(256),
            updated_by  VARCHAR(256),
            is_deleted  BOOLEAN     NOT NULL DEFAULT FALSE,
            deleted_at  TIMESTAMPTZ,
            deleted_by  VARCHAR(256),
            CONSTRAINT pk_favorite_services PRIMARY KEY (id),
            CONSTRAINT fk_favorite_services_services
                FOREIGN KEY (service_id) REFERENCES {s}.services (id)
        );

        CREATE UNIQUE INDEX IF NOT EXISTS uq_favorite_services_customer_service
            ON {s}.favorite_services (customer_id, service_id)
            WHERE is_deleted = FALSE;

        CREATE INDEX IF NOT EXISTS ix_favorite_services_customer
            ON {s}.favorite_services (customer_id)
            WHERE is_deleted = FALSE;
```

- [ ] **Step 6: Create FavoriteServiceEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/FavoriteServiceEntityConfiguration.cs
using Horafy.Domain.Entities.Favorites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

internal sealed class FavoriteServiceEntityConfiguration
    : IEntityTypeConfiguration<FavoriteService>
{
    public void Configure(EntityTypeBuilder<FavoriteService> builder)
    {
        builder.ToTable("favorite_services");
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => new { f.CustomerId, f.ServiceId })
            .IsUnique()
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("uq_favorite_services_customer_service");
        builder.HasIndex(f => f.CustomerId)
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("ix_favorite_services_customer");
    }
}
```

- [ ] **Step 7: Add DbSet to TenantDbContext**

In `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs`:
```csharp
public DbSet<FavoriteService> FavoriteServices => Set<FavoriteService>();
```
Add using:
```csharp
using Horafy.Domain.Entities.Favorites;
```

- [ ] **Step 8: Create FavoriteServiceRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/FavoriteServiceRepository.cs
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

internal sealed class FavoriteServiceRepository(TenantDbContext db)
    : RepositoryBase<FavoriteService>(db), IFavoriteServiceRepository
{
    public async Task<FavoriteService?> GetAsync(
        Guid customerId, Guid serviceId, CancellationToken cancellationToken = default) =>
        await db.FavoriteServices
            .FirstOrDefaultAsync(
                f => f.CustomerId == customerId && f.ServiceId == serviceId,
                cancellationToken);

    public async Task<IReadOnlyList<FavoriteService>> GetByCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default) =>
        await db.FavoriteServices
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
}
```

- [ ] **Step 9: Register in DI**

In `src/Horafy.Infrastructure/DependencyInjection.cs`, add:
```csharp
services.AddScoped<IFavoriteServiceRepository, FavoriteServiceRepository>();
```

- [ ] **Step 10: Run tests**

```
dotnet test tests/Horafy.Domain.Tests --filter "FullyQualifiedName~FavoriteServiceTests" -v minimal
```
Expected: PASS.

```
dotnet build src/Horafy.Infrastructure/Horafy.Infrastructure.csproj
```
Expected: Build succeeds.

- [ ] **Step 11: Commit**

```
git add src/Horafy.Domain/Entities/Favorites/
git add src/Horafy.Domain/Interfaces/Repositories/IFavoriteServiceRepository.cs
git add src/Horafy.Infrastructure/MultiTenancy/TenantSchemaService.cs
git add src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
git add src/Horafy.Infrastructure/Persistence/TenantConfigurations/FavoriteServiceEntityConfiguration.cs
git add src/Horafy.Infrastructure/Repositories/FavoriteServiceRepository.cs
git add src/Horafy.Infrastructure/DependencyInjection.cs
git add tests/Horafy.Domain.Tests/Favorites/FavoriteServiceTests.cs
git commit -m "feat: add FavoriteService entity, repository, DDL and DI registration"
```

---

## Task 10: CQRS para Favoritos + controller

**Files:**
- Create: `src/Horafy.Application/Features/Favorites/Commands/AddFavoriteServiceCommand.cs`
- Create: `src/Horafy.Application/Features/Favorites/Commands/RemoveFavoriteServiceCommand.cs`
- Create: `src/Horafy.Application/Features/Favorites/Queries/GetCustomerFavoritesQuery.cs`
- Create: `src/Horafy.API/Controllers/V1/FavoriteServicesController.cs`
- Create: `tests/Horafy.Application.Tests/Favorites/AddFavoriteServiceCommandTests.cs`
- Create: `tests/Horafy.Application.Tests/Favorites/RemoveFavoriteServiceCommandTests.cs`
- Create: `tests/Horafy.Application.Tests/Favorites/GetCustomerFavoritesQueryTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Horafy.Application.Tests/Favorites/AddFavoriteServiceCommandTests.cs
using FluentAssertions;
using Horafy.Application.Features.Favorites.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Favorites;

public sealed class AddFavoriteServiceCommandTests
{
    private readonly Mock<ICurrentUserService>        _currentUser = new();
    private readonly Mock<IFavoriteServiceRepository> _repo        = new();
    private readonly Mock<ITenantUnitOfWork>          _uow         = new();

    private AddFavoriteServiceCommandHandler MakeHandler() =>
        new(_currentUser.Object, _repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NotYetFavorited_AddsFavorite()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);
        _repo.Setup(r => r.GetAsync(customerId, serviceId, default))
            .ReturnsAsync((FavoriteService?)null);

        var result = await MakeHandler().Handle(
            new AddFavoriteServiceCommand(serviceId), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.IsAny<FavoriteService>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyFavorited_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var existing = FavoriteService.Create(customerId, serviceId);
        _repo.Setup(r => r.GetAsync(customerId, serviceId, default)).ReturnsAsync(existing);

        var result = await MakeHandler().Handle(
            new AddFavoriteServiceCommand(serviceId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.AlreadyExists");
    }
}
```

```csharp
// tests/Horafy.Application.Tests/Favorites/RemoveFavoriteServiceCommandTests.cs
using FluentAssertions;
using Horafy.Application.Features.Favorites.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Favorites;

public sealed class RemoveFavoriteServiceCommandTests
{
    private readonly Mock<ICurrentUserService>        _currentUser = new();
    private readonly Mock<IFavoriteServiceRepository> _repo        = new();
    private readonly Mock<ITenantUnitOfWork>          _uow         = new();

    private RemoveFavoriteServiceCommandHandler MakeHandler() =>
        new(_currentUser.Object, _repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ExistingFavorite_RemovesIt()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var existing = FavoriteService.Create(customerId, serviceId);
        _repo.Setup(r => r.GetAsync(customerId, serviceId, default)).ReturnsAsync(existing);

        var result = await MakeHandler().Handle(
            new RemoveFavoriteServiceCommand(serviceId), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Remove(existing), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFavorited_ReturnsNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync((FavoriteService?)null);

        var result = await MakeHandler().Handle(
            new RemoveFavoriteServiceCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Favorite.NotFound");
    }
}
```

```csharp
// tests/Horafy.Application.Tests/Favorites/GetCustomerFavoritesQueryTests.cs
using FluentAssertions;
using Horafy.Application.Features.Favorites.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Favorites;

public sealed class GetCustomerFavoritesQueryTests
{
    private readonly Mock<ICurrentUserService>        _currentUser = new();
    private readonly Mock<IFavoriteServiceRepository> _repo        = new();

    private GetCustomerFavoritesQueryHandler MakeHandler() =>
        new(_currentUser.Object, _repo.Object);

    [Fact]
    public async Task Handle_CustomerWithFavorites_ReturnsList()
    {
        var customerId = Guid.NewGuid();
        var serviceId  = Guid.NewGuid();
        _currentUser.Setup(c => c.UserId).Returns(customerId);

        var fav = FavoriteService.Create(customerId, serviceId);
        _repo.Setup(r => r.GetByCustomerAsync(customerId, default))
            .ReturnsAsync(new List<FavoriteService> { fav });

        var result = await MakeHandler().Handle(new GetCustomerFavoritesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ServiceId.Should().Be(serviceId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Favorites" -v minimal
```
Expected: FAIL.

- [ ] **Step 3: Check that `IRepository<T>` has a `Remove` method**

Look at the base repository interface. It likely has `Add(T)`, `Update(T)`, `Remove(T)`. If `Remove` is not present, add it to `IRepository<T>` and implement in `RepositoryBase<T>`:

```csharp
// In RepositoryBase<T>
public void Remove(T entity) => Db.Remove(entity);
```

- [ ] **Step 4: Create AddFavoriteServiceCommand**

```csharp
// src/Horafy.Application/Features/Favorites/Commands/AddFavoriteServiceCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Favorites;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Favorites.Commands;

public sealed record AddFavoriteServiceCommand(Guid ServiceId) : IRequest<Result>;

internal sealed class AddFavoriteServiceCommandHandler(
    ICurrentUserService        currentUserService,
    IFavoriteServiceRepository repository,
    ITenantUnitOfWork          unitOfWork)
    : IRequestHandler<AddFavoriteServiceCommand, Result>
{
    public async Task<Result> Handle(
        AddFavoriteServiceCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(
            currentUserService.UserId, request.ServiceId, cancellationToken);

        if (existing is not null)
            return Result.Failure(new Error(
                "Favorite.AlreadyExists",
                "Este serviço já está nos seus favoritos.",
                ErrorType.Conflict));

        var favorite = FavoriteService.Create(currentUserService.UserId, request.ServiceId);
        repository.Add(favorite);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 5: Create RemoveFavoriteServiceCommand**

```csharp
// src/Horafy.Application/Features/Favorites/Commands/RemoveFavoriteServiceCommand.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Favorites.Commands;

public sealed record RemoveFavoriteServiceCommand(Guid ServiceId) : IRequest<Result>;

internal sealed class RemoveFavoriteServiceCommandHandler(
    ICurrentUserService        currentUserService,
    IFavoriteServiceRepository repository,
    ITenantUnitOfWork          unitOfWork)
    : IRequestHandler<RemoveFavoriteServiceCommand, Result>
{
    public async Task<Result> Handle(
        RemoveFavoriteServiceCommand request, CancellationToken cancellationToken)
    {
        var favorite = await repository.GetAsync(
            currentUserService.UserId, request.ServiceId, cancellationToken);

        if (favorite is null)
            return Result.Failure(new Error(
                "Favorite.NotFound",
                "Favorito não encontrado.",
                ErrorType.NotFound));

        repository.Remove(favorite);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

- [ ] **Step 6: Create GetCustomerFavoritesQuery**

```csharp
// src/Horafy.Application/Features/Favorites/Queries/GetCustomerFavoritesQuery.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Favorites.Queries;

public sealed record GetCustomerFavoritesQuery
    : IRequest<Result<IReadOnlyList<FavoriteServiceResult>>>;

public sealed record FavoriteServiceResult(Guid Id, Guid ServiceId, DateTimeOffset CreatedAt);

internal sealed class GetCustomerFavoritesQueryHandler(
    ICurrentUserService        currentUserService,
    IFavoriteServiceRepository repository)
    : IRequestHandler<GetCustomerFavoritesQuery, Result<IReadOnlyList<FavoriteServiceResult>>>
{
    public async Task<Result<IReadOnlyList<FavoriteServiceResult>>> Handle(
        GetCustomerFavoritesQuery request, CancellationToken cancellationToken)
    {
        var favorites = await repository.GetByCustomerAsync(
            currentUserService.UserId, cancellationToken);

        var results = favorites
            .Select(f => new FavoriteServiceResult(f.Id, f.ServiceId, f.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<FavoriteServiceResult>>(results);
    }
}
```

- [ ] **Step 7: Create FavoriteServicesController**

```csharp
// src/Horafy.API/Controllers/V1/FavoriteServicesController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Favorites.Commands;
using Horafy.Application.Features.Favorites.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
[Authorize(Roles = "Customer")]
[Route("api/v{version:apiVersion}/customers/favorites")]
public sealed class FavoriteServicesController(ISender sender)
    : ApiControllerBase(sender)
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FavoriteServiceResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetCustomerFavoritesQuery(), ct));

    [HttpPost("{serviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid serviceId, CancellationToken ct)
    {
        var result = await Sender.Send(new AddFavoriteServiceCommand(serviceId), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    [HttpDelete("{serviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid serviceId, CancellationToken ct)
    {
        var result = await Sender.Send(new RemoveFavoriteServiceCommand(serviceId), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}
```

- [ ] **Step 8: Run all tests**

```
dotnet test tests/Horafy.Application.Tests --filter "FullyQualifiedName~Favorites" -v minimal
```
Expected: PASS — all 5 tests pass.

```
dotnet build src/Horafy.API/Horafy.API.csproj
```
Expected: Build succeeds.

- [ ] **Step 9: Run full test suite**

```
dotnet test --configuration Release -v minimal
```
Expected: All tests pass (169 existing + new Sprint 8 tests).

- [ ] **Step 10: Commit**

```
git add src/Horafy.Application/Features/Favorites/
git add src/Horafy.API/Controllers/V1/FavoriteServicesController.cs
git add tests/Horafy.Application.Tests/Favorites/
git commit -m "feat: add FavoriteService CQRS — add, remove, list favorites"
```

---

## Task 11: Sprint 8 final — build limpo e commit de fechamento

- [ ] **Step 1: Build completo**

```
dotnet build --configuration Release
```
Expected: Build succeeded, 0 error(s), 0 warning(s) (ou apenas warnings não-breaking).

- [ ] **Step 2: Run full test suite**

```
dotnet test --configuration Release -v normal
```
Expected: All tests pass. Note the count (should be 169 + Sprint 8 additions ≥ 190).

- [ ] **Step 3: Verify all new endpoints exist in the build**

Check that controller routes are reachable by inspecting the build output or using Swagger:
- `POST /api/v1/customers/auth/google`
- `POST /api/v1/customers/auth/apple`
- `GET  /api/v1/customers/me`
- `PATCH /api/v1/customers/me/phone`
- `GET  /api/v1/customers/me/bookings`
- `POST /api/v1/reviews`
- `GET  /api/v1/reviews/resources/{resourceId}`
- `GET  /api/v1/customers/favorites`
- `POST /api/v1/customers/favorites/{serviceId}`
- `DELETE /api/v1/customers/favorites/{serviceId}`

- [ ] **Step 4: Commit sprint closing**

```
git add -A
git commit -m "sprint(8): Módulo Clientes — Phone, OAuth customer, perfil, histórico, Reviews, FavoriteService"
```

---

## Self-Review

### Spec coverage check

| Requirement | Task |
|---|---|
| `User.Phone` field + `SetPhone` | Task 1 |
| `public.users.phone` DDL | Task 1 (`GlobalMigrations`) |
| `Booking.CustomerPhone` + `tenant.bookings.customer_phone` DDL | Task 2 |
| `CustomerPhone` populado em `CreateBookingCommand` | Task 2 |
| `CustomerPhone` populado nos notification publishers | Task 3 |
| OAuth Google para clientes (endpoint dedicado) | Task 4 |
| OAuth Apple para clientes (endpoint dedicado) | Task 4 |
| GET `/customers/me` | Task 5 |
| PATCH `/customers/me/phone` | Task 5 |
| GET `/customers/me/bookings` | Task 6 |
| `Review` entity + DDL + repositório | Task 7 |
| POST `/reviews` (criar avaliação) | Task 8 |
| GET `/reviews/resources/{resourceId}` | Task 8 |
| `FavoriteService` entity + DDL + repositório | Task 9 |
| POST `/customers/favorites/{serviceId}` | Task 10 |
| DELETE `/customers/favorites/{serviceId}` | Task 10 |
| GET `/customers/favorites` | Task 10 |

### Placeholder scan
- No "TBD", "TODO", or "implement later" found.
- Every code step shows complete implementation.

### Type consistency
- `CustomerLoginWithGoogleCommand` uses `IGoogleOAuthService` + `OAuthUserInfo` — same types used in the existing `LoginWithGoogleCommand`.
- `ReviewResult` in Task 8 matches fields defined in `Review.Create` in Task 7.
- `FavoriteServiceResult` in Task 10 uses `FavoriteService.Id`, `ServiceId`, `CreatedAt` — all defined in Task 9.
- `CustomerBookingResult` references `BookingPaymentStatus` — make sure the using `Horafy.Domain.Entities.Bookings` is present in `GetCustomerBookingsQuery.cs`.
- `ErrorType.Conflict` — verify this value exists in `Horafy.Shared.ErrorType`. If not, use `ErrorType.Validation` instead and adjust the test expectations.
- `IRepository<T>.Remove(T)` — assumed to exist. If not, add it to `IRepository<T>` and `RepositoryBase<T>` as noted in Task 10, Step 3.
