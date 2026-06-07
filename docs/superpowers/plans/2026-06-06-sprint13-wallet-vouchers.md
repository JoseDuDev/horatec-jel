# Sprint 13 — Wallet de Créditos & Vouchers

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar o sistema de Wallet de créditos por cliente e Vouchers de desconto para os tenants — permitindo ao admin adicionar créditos a clientes e criar cupons de desconto, e ao cliente visualizar seu saldo na área logada.

**Architecture:** Wallet e Voucher são entidades do schema do tenant (TenantDbContext). Wallet é 1-para-1 com User (criada on-demand no primeiro crédito); WalletTransaction é uma entidade separada com FK para Wallet. Voucher armazena código único por tenant, tipo de desconto (Percentual ou Fixo), contagem de usos e expiração. O ValidateVoucher é um query público (sem side-effects); a integração com o checkout do booking será feita na Sprint 14.

**Tech Stack:** .NET 8 + MediatR + EF Core (backend), Next.js 16 + shadcn/ui (frontend), xUnit + Moq + FluentAssertions (testes).

---

## File Map

```
# Backend — Domain
src/Horafy.Domain/Entities/Wallet/Wallet.cs
src/Horafy.Domain/Entities/Wallet/WalletTransaction.cs
src/Horafy.Domain/Entities/Wallet/WalletTransactionType.cs
src/Horafy.Domain/Entities/Wallet/WalletErrors.cs
src/Horafy.Domain/Entities/Vouchers/Voucher.cs
src/Horafy.Domain/Entities/Vouchers/VoucherDiscountType.cs
src/Horafy.Domain/Entities/Vouchers/VoucherErrors.cs
src/Horafy.Domain/Interfaces/Repositories/IWalletRepository.cs
src/Horafy.Domain/Interfaces/Repositories/IVoucherRepository.cs

# Backend — Application
src/Horafy.Application/Features/Wallet/Queries/GetWallet/GetWalletQuery.cs
src/Horafy.Application/Features/Wallet/Commands/AddCredits/AddCreditsCommand.cs
src/Horafy.Application/Features/Vouchers/Queries/GetVouchers/GetVouchersQuery.cs
src/Horafy.Application/Features/Vouchers/Queries/ValidateVoucher/ValidateVoucherQuery.cs
src/Horafy.Application/Features/Vouchers/Commands/CreateVoucher/CreateVoucherCommand.cs
src/Horafy.Application/Features/Vouchers/Commands/DeactivateVoucher/DeactivateVoucherCommand.cs

# Backend — Infrastructure (modify)
src/Horafy.Infrastructure/Persistence/TenantDbContext.cs
src/Horafy.Infrastructure/Persistence/TenantConfigurations/WalletEntityConfiguration.cs
src/Horafy.Infrastructure/Persistence/TenantConfigurations/WalletTransactionEntityConfiguration.cs
src/Horafy.Infrastructure/Persistence/TenantConfigurations/VoucherEntityConfiguration.cs
src/Horafy.Infrastructure/Repositories/WalletRepository.cs
src/Horafy.Infrastructure/Repositories/VoucherRepository.cs
src/Horafy.Infrastructure/DependencyInjection.cs  (modify — register repos)

# Backend — API
src/Horafy.API/Controllers/V1/WalletController.cs
src/Horafy.API/Controllers/V1/VouchersController.cs

# Backend — Tests
tests/Horafy.Application.Tests/Wallet/AddCreditsCommandHandlerTests.cs
tests/Horafy.Application.Tests/Vouchers/VoucherEntityTests.cs
tests/Horafy.Application.Tests/Vouchers/CreateVoucherCommandHandlerTests.cs

# Frontend
frontend/lib/types/wallet.ts
frontend/lib/api/wallet.ts
frontend/app/(admin)/admin/carteira/page.tsx
frontend/__tests__/CarteirAdmin.test.tsx
frontend/app/(portal)/[slug]/minha-conta/page.tsx  (modify — add wallet section)
```

---

### Task 1: Backend Domain — Wallet entities

**Files:**
- Create: `src/Horafy.Domain/Entities/Wallet/WalletTransactionType.cs`
- Create: `src/Horafy.Domain/Entities/Wallet/WalletErrors.cs`
- Create: `src/Horafy.Domain/Entities/Wallet/WalletTransaction.cs`
- Create: `src/Horafy.Domain/Entities/Wallet/Wallet.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IWalletRepository.cs`

- [ ] **Step 1: Criar enum e erros**

```csharp
// src/Horafy.Domain/Entities/Wallet/WalletTransactionType.cs
namespace Horafy.Domain.Entities.Wallet;
public enum WalletTransactionType { CreditAdded, BookingPayment, BookingRefund, LoyaltyBonus }
```

```csharp
// src/Horafy.Domain/Entities/Wallet/WalletErrors.cs
using Horafy.Shared;
namespace Horafy.Domain.Entities.Wallet;
public static class WalletErrors
{
    public static readonly Error InvalidAmount      = new("Wallet.InvalidAmount",      "O valor deve ser maior que zero.",  ErrorType.Validation);
    public static readonly Error DescriptionRequired = new("Wallet.DescriptionRequired","A descrição é obrigatória.",        ErrorType.Validation);
    public static readonly Error InsufficientBalance = new("Wallet.InsufficientBalance","Saldo insuficiente.",               ErrorType.Validation);
}
```

- [ ] **Step 2: Criar WalletTransaction**

```csharp
// src/Horafy.Domain/Entities/Wallet/WalletTransaction.cs
using Horafy.Domain.Entities.Base;
namespace Horafy.Domain.Entities.Wallet;

public sealed class WalletTransaction : BaseEntity
{
    private WalletTransaction() { }

    public Guid WalletId { get; private set; }
    public WalletTransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public Guid? BookingId { get; private set; }

    internal static WalletTransaction Create(
        Guid walletId,
        WalletTransactionType type,
        decimal amount,
        string description,
        Guid? bookingId = null) =>
        new()
        {
            WalletId = walletId,
            Type = type,
            Amount = amount,
            Description = description,
            BookingId = bookingId,
        };
}
```

- [ ] **Step 3: Criar Wallet**

```csharp
// src/Horafy.Domain/Entities/Wallet/Wallet.cs
using Horafy.Domain.Entities.Base;
using Horafy.Shared;
namespace Horafy.Domain.Entities.Wallet;

public sealed class Wallet : BaseEntity
{
    private readonly List<WalletTransaction> _transactions = new();
    private Wallet() { }

    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public IReadOnlyList<WalletTransaction> Transactions => _transactions.AsReadOnly();

    public static Wallet Create(Guid userId) => new() { UserId = userId, Balance = 0 };

    public Result AddCredits(decimal amount, string description)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        if (string.IsNullOrWhiteSpace(description)) return Result.Failure(WalletErrors.DescriptionRequired);
        Balance += amount;
        _transactions.Add(WalletTransaction.Create(Id, WalletTransactionType.CreditAdded, amount, description));
        return Result.Success();
    }

    public Result RefundFromBooking(decimal amount, string description, Guid bookingId)
    {
        if (amount <= 0) return Result.Failure(WalletErrors.InvalidAmount);
        Balance += amount;
        _transactions.Add(WalletTransaction.Create(Id, WalletTransactionType.BookingRefund, amount, description, bookingId));
        return Result.Success();
    }
}
```

- [ ] **Step 4: Criar IWalletRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IWalletRepository.cs
using Horafy.Domain.Entities.Wallet;
namespace Horafy.Domain.Interfaces.Repositories;

public interface IWalletRepository : IRepository<Wallet>
{
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
```

- [ ] **Step 5: Build para verificar compilação**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet build src/Horafy.Domain 2>&1 | Select-Object -Last 5
```

Expected: `0 Erro(s)`.

- [ ] **Step 6: Commit**

```powershell
git add src/Horafy.Domain/Entities/Wallet/ src/Horafy.Domain/Interfaces/Repositories/IWalletRepository.cs
git commit -m "feat: add Wallet domain entity, WalletTransaction and IWalletRepository"
```

---

### Task 2: Backend Domain — Voucher entities

**Files:**
- Create: `src/Horafy.Domain/Entities/Vouchers/VoucherDiscountType.cs`
- Create: `src/Horafy.Domain/Entities/Vouchers/VoucherErrors.cs`
- Create: `src/Horafy.Domain/Entities/Vouchers/Voucher.cs`
- Create: `src/Horafy.Domain/Interfaces/Repositories/IVoucherRepository.cs`

- [ ] **Step 1: Criar enum e erros**

```csharp
// src/Horafy.Domain/Entities/Vouchers/VoucherDiscountType.cs
namespace Horafy.Domain.Entities.Vouchers;
public enum VoucherDiscountType { Percentage, Fixed }
```

```csharp
// src/Horafy.Domain/Entities/Vouchers/VoucherErrors.cs
using Horafy.Shared;
namespace Horafy.Domain.Entities.Vouchers;
public static class VoucherErrors
{
    public static readonly Error NotFound            = new("Voucher.NotFound",           "Voucher não encontrado.",                      ErrorType.NotFound);
    public static readonly Error CodeAlreadyExists   = new("Voucher.CodeAlreadyExists",  "Este código já está em uso.",                  ErrorType.Conflict);
    public static readonly Error Inactive            = new("Voucher.Inactive",           "Este voucher está inativo.",                   ErrorType.Validation);
    public static readonly Error Expired             = new("Voucher.Expired",            "Este voucher está expirado.",                  ErrorType.Validation);
    public static readonly Error MaxUsesReached      = new("Voucher.MaxUsesReached",     "Este voucher atingiu o limite de usos.",        ErrorType.Validation);
    public static readonly Error InvalidDiscountValue = new("Voucher.InvalidDiscount",   "O valor do desconto deve ser maior que zero.", ErrorType.Validation);
    public static readonly Error InvalidPercentage   = new("Voucher.InvalidPercentage",  "A porcentagem não pode exceder 100.",          ErrorType.Validation);
}
```

- [ ] **Step 2: Criar Voucher**

```csharp
// src/Horafy.Domain/Entities/Vouchers/Voucher.cs
using Horafy.Domain.Entities.Base;
using Horafy.Shared;
namespace Horafy.Domain.Entities.Vouchers;

public sealed class Voucher : BaseEntity
{
    private Voucher() { }

    public string Code { get; private set; } = default!;
    public VoucherDiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public int? MaxUses { get; private set; }
    public int UsedCount { get; private set; }
    public bool IsActive { get; private set; }

    public static Voucher Create(
        string code,
        VoucherDiscountType discountType,
        decimal discountValue,
        string? description,
        DateTimeOffset? expiresAt,
        int? maxUses) =>
        new()
        {
            Code = code.ToUpperInvariant(),
            DiscountType = discountType,
            DiscountValue = discountValue,
            Description = description,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            IsActive = true,
            UsedCount = 0,
        };

    public Result<decimal> CalculateDiscount(decimal totalPrice)
    {
        if (!IsActive)
            return Result.Failure<decimal>(VoucherErrors.Inactive);
        if (ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow)
            return Result.Failure<decimal>(VoucherErrors.Expired);
        if (MaxUses.HasValue && UsedCount >= MaxUses.Value)
            return Result.Failure<decimal>(VoucherErrors.MaxUsesReached);

        var discount = DiscountType == VoucherDiscountType.Percentage
            ? totalPrice * (DiscountValue / 100m)
            : Math.Min(DiscountValue, totalPrice);

        return Result.Success(Math.Round(discount, 2));
    }

    public void Redeem() => UsedCount++;
    public void Deactivate() => IsActive = false;
}
```

- [ ] **Step 3: Criar IVoucherRepository**

```csharp
// src/Horafy.Domain/Interfaces/Repositories/IVoucherRepository.cs
using Horafy.Domain.Entities.Vouchers;
namespace Horafy.Domain.Interfaces.Repositories;

public interface IVoucherRepository : IRepository<Voucher>
{
    Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
}
```

- [ ] **Step 4: Build**

```powershell
dotnet build src/Horafy.Domain 2>&1 | Select-Object -Last 5
```

Expected: `0 Erro(s)`.

- [ ] **Step 5: Commit**

```powershell
git add src/Horafy.Domain/Entities/Vouchers/ src/Horafy.Domain/Interfaces/Repositories/IVoucherRepository.cs
git commit -m "feat: add Voucher domain entity and IVoucherRepository"
```

---

### Task 3: Backend Application — Wallet (query + command + tests)

**Files:**
- Create: `src/Horafy.Application/Features/Wallet/Queries/GetWallet/GetWalletQuery.cs`
- Create: `src/Horafy.Application/Features/Wallet/Commands/AddCredits/AddCreditsCommand.cs`
- Create: `tests/Horafy.Application.Tests/Wallet/AddCreditsCommandHandlerTests.cs`

- [ ] **Step 1: Criar GetWalletQuery**

```csharp
// src/Horafy.Application/Features/Wallet/Queries/GetWallet/GetWalletQuery.cs
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Wallet;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Wallet.Queries.GetWallet;

public sealed record GetWalletQuery : IRequest<Result<WalletResult>>;

public sealed record WalletResult(
    Guid   WalletId,
    decimal Balance,
    IReadOnlyList<WalletTransactionResult> Transactions);

public sealed record WalletTransactionResult(
    Guid                  Id,
    WalletTransactionType Type,
    decimal               Amount,
    string                Description,
    Guid?                 BookingId,
    DateTimeOffset        CreatedAt);

internal sealed class GetWalletQueryHandler(
    IWalletRepository walletRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetWalletQuery, Result<WalletResult>>
{
    public async Task<Result<WalletResult>> Handle(GetWalletQuery request, CancellationToken ct)
    {
        if (!currentUser.UserId.HasValue)
            return Result.Failure<WalletResult>(Error.Unauthorized);

        var wallet = await walletRepository.GetByUserIdAsync(currentUser.UserId.Value, ct);

        if (wallet is null)
            return Result.Success(new WalletResult(Guid.Empty, 0, Array.Empty<WalletTransactionResult>()));

        var transactions = wallet.Transactions
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new WalletTransactionResult(t.Id, t.Type, t.Amount, t.Description, t.BookingId, t.CreatedAt))
            .ToList();

        return Result.Success(new WalletResult(wallet.Id, wallet.Balance, transactions));
    }
}
```

- [ ] **Step 2: Criar AddCreditsCommand**

```csharp
// src/Horafy.Application/Features/Wallet/Commands/AddCredits/AddCreditsCommand.cs
using Horafy.Domain.Entities.Wallet;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Wallet.Commands.AddCredits;

public sealed record AddCreditsCommand(Guid UserId, decimal Amount, string Description)
    : IRequest<Result>;

internal sealed class AddCreditsCommandHandler(
    IWalletRepository walletRepository,
    ITenantUnitOfWork unitOfWork)
    : IRequestHandler<AddCreditsCommand, Result>
{
    public async Task<Result> Handle(AddCreditsCommand request, CancellationToken ct)
    {
        var wallet = await walletRepository.GetByUserIdAsync(request.UserId, ct);

        if (wallet is null)
        {
            wallet = Wallet.Create(request.UserId);
            walletRepository.Add(wallet);
        }

        var result = wallet.AddCredits(request.Amount, request.Description);
        if (result.IsFailure) return result;

        walletRepository.Update(wallet);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 3: Escrever testes**

```csharp
// tests/Horafy.Application.Tests/Wallet/AddCreditsCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Wallet.Commands.AddCredits;
using Horafy.Domain.Entities.Wallet;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Wallet;

public sealed class AddCreditsCommandHandlerTests
{
    private readonly Mock<IWalletRepository>  _repo = new();
    private readonly Mock<ITenantUnitOfWork>  _uow  = new();

    private AddCreditsCommandHandler MakeHandler() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NewUser_CreatesWalletAndAddsCredits()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((Wallet?)null);

        var result = await MakeHandler().Handle(
            new AddCreditsCommand(userId, 50m, "Bônus de boas-vindas"), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.Is<Wallet>(w => w.UserId == userId && w.Balance == 50m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingWallet_AccumulatesBalance()
    {
        var userId = Guid.NewGuid();
        var wallet = Wallet.Create(userId);
        wallet.AddCredits(30m, "Crédito inicial");

        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(wallet);

        var result = await MakeHandler().Handle(
            new AddCreditsCommand(userId, 20m, "Crédito extra"), default);

        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_NegativeAmount_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((Wallet?)null);

        var result = await MakeHandler().Handle(
            new AddCreditsCommand(userId, -10m, "Inválido"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InvalidAmount");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
```

- [ ] **Step 4: Executar testes (esperar FAIL — handlers existem, repos não registrados)**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet test tests/Horafy.Application.Tests --filter "AddCreditsCommandHandlerTests" 2>&1 | Select-Object -Last 10
```

Expected: PASS (os mocks isolam os handlers).

- [ ] **Step 5: Build da Application layer**

```powershell
dotnet build src/Horafy.Application 2>&1 | Select-Object -Last 5
```

Expected: `0 Erro(s)`.

- [ ] **Step 6: Commit**

```powershell
git add src/Horafy.Application/Features/Wallet/ tests/Horafy.Application.Tests/Wallet/
git commit -m "feat: add GetWalletQuery, AddCreditsCommand and handler tests"
```

---

### Task 4: Backend Application — Vouchers (queries + commands + tests)

**Files:**
- Create: `src/Horafy.Application/Features/Vouchers/Queries/GetVouchers/GetVouchersQuery.cs`
- Create: `src/Horafy.Application/Features/Vouchers/Queries/ValidateVoucher/ValidateVoucherQuery.cs`
- Create: `src/Horafy.Application/Features/Vouchers/Commands/CreateVoucher/CreateVoucherCommand.cs`
- Create: `src/Horafy.Application/Features/Vouchers/Commands/DeactivateVoucher/DeactivateVoucherCommand.cs`
- Create: `tests/Horafy.Application.Tests/Vouchers/VoucherEntityTests.cs`
- Create: `tests/Horafy.Application.Tests/Vouchers/CreateVoucherCommandHandlerTests.cs`

- [ ] **Step 1: Criar GetVouchersQuery**

```csharp
// src/Horafy.Application/Features/Vouchers/Queries/GetVouchers/GetVouchersQuery.cs
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Queries.GetVouchers;

public sealed record GetVouchersQuery : IRequest<Result<IReadOnlyList<VoucherSummary>>>;

public sealed record VoucherSummary(
    Guid                 Id,
    string               Code,
    VoucherDiscountType  DiscountType,
    decimal              DiscountValue,
    string?              Description,
    DateTimeOffset?      ExpiresAt,
    int?                 MaxUses,
    int                  UsedCount,
    bool                 IsActive,
    DateTimeOffset       CreatedAt);

internal sealed class GetVouchersQueryHandler(
    IVoucherRepository voucherRepository)
    : IRequestHandler<GetVouchersQuery, Result<IReadOnlyList<VoucherSummary>>>
{
    public async Task<Result<IReadOnlyList<VoucherSummary>>> Handle(GetVouchersQuery request, CancellationToken ct)
    {
        var vouchers = await voucherRepository.GetAllAsync(ct);
        var result = vouchers
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VoucherSummary(
                v.Id, v.Code, v.DiscountType, v.DiscountValue,
                v.Description, v.ExpiresAt, v.MaxUses, v.UsedCount, v.IsActive, v.CreatedAt))
            .ToList();
        return Result.Success<IReadOnlyList<VoucherSummary>>(result);
    }
}
```

- [ ] **Step 2: Criar ValidateVoucherQuery**

```csharp
// src/Horafy.Application/Features/Vouchers/Queries/ValidateVoucher/ValidateVoucherQuery.cs
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Queries.ValidateVoucher;

public sealed record ValidateVoucherQuery(string Code, decimal TotalPrice)
    : IRequest<Result<VoucherValidationResult>>;

public sealed record VoucherValidationResult(
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    decimal             DiscountAmount,
    decimal             FinalPrice,
    string?             Description);

internal sealed class ValidateVoucherQueryHandler(
    IVoucherRepository voucherRepository)
    : IRequestHandler<ValidateVoucherQuery, Result<VoucherValidationResult>>
{
    public async Task<Result<VoucherValidationResult>> Handle(ValidateVoucherQuery request, CancellationToken ct)
    {
        var voucher = await voucherRepository.GetByCodeAsync(request.Code.ToUpperInvariant(), ct);
        if (voucher is null)
            return Result.Failure<VoucherValidationResult>(VoucherErrors.NotFound);

        var discountResult = voucher.CalculateDiscount(request.TotalPrice);
        if (discountResult.IsFailure)
            return Result.Failure<VoucherValidationResult>(discountResult.Error);

        return Result.Success(new VoucherValidationResult(
            voucher.Code,
            voucher.DiscountType,
            voucher.DiscountValue,
            discountResult.Value,
            request.TotalPrice - discountResult.Value,
            voucher.Description));
    }
}
```

- [ ] **Step 3: Criar CreateVoucherCommand**

```csharp
// src/Horafy.Application/Features/Vouchers/Commands/CreateVoucher/CreateVoucherCommand.cs
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Commands.CreateVoucher;

public sealed record CreateVoucherCommand(
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    string?             Description,
    DateTimeOffset?     ExpiresAt,
    int?                MaxUses)
    : IRequest<Result<Guid>>;

internal sealed class CreateVoucherCommandHandler(
    IVoucherRepository voucherRepository,
    ITenantUnitOfWork  unitOfWork)
    : IRequestHandler<CreateVoucherCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateVoucherCommand request, CancellationToken ct)
    {
        if (request.DiscountValue <= 0)
            return Result.Failure<Guid>(VoucherErrors.InvalidDiscountValue);
        if (request.DiscountType == VoucherDiscountType.Percentage && request.DiscountValue > 100)
            return Result.Failure<Guid>(VoucherErrors.InvalidPercentage);

        var exists = await voucherRepository.CodeExistsAsync(request.Code.ToUpperInvariant(), ct);
        if (exists) return Result.Failure<Guid>(VoucherErrors.CodeAlreadyExists);

        var voucher = Voucher.Create(
            request.Code, request.DiscountType, request.DiscountValue,
            request.Description, request.ExpiresAt, request.MaxUses);

        voucherRepository.Add(voucher);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(voucher.Id);
    }
}
```

- [ ] **Step 4: Criar DeactivateVoucherCommand**

```csharp
// src/Horafy.Application/Features/Vouchers/Commands/DeactivateVoucher/DeactivateVoucherCommand.cs
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Vouchers.Commands.DeactivateVoucher;

public sealed record DeactivateVoucherCommand(Guid Id) : IRequest<Result>;

internal sealed class DeactivateVoucherCommandHandler(
    IVoucherRepository voucherRepository,
    ITenantUnitOfWork  unitOfWork)
    : IRequestHandler<DeactivateVoucherCommand, Result>
{
    public async Task<Result> Handle(DeactivateVoucherCommand request, CancellationToken ct)
    {
        var voucher = await voucherRepository.GetByIdAsync(request.Id, ct);
        if (voucher is null) return Result.Failure(VoucherErrors.NotFound);

        voucher.Deactivate();
        voucherRepository.Update(voucher);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

- [ ] **Step 5: Escrever VoucherEntityTests**

```csharp
// tests/Horafy.Application.Tests/Vouchers/VoucherEntityTests.cs
using FluentAssertions;
using Horafy.Domain.Entities.Vouchers;
using Xunit;

namespace Horafy.Application.Tests.Vouchers;

public sealed class VoucherEntityTests
{
    [Fact]
    public void CalculateDiscount_Percentage_ReturnsCorrectAmount()
    {
        var voucher = Voucher.Create("TEST10", VoucherDiscountType.Percentage, 10m, null, null, null);
        var result = voucher.CalculateDiscount(200m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(20m);
    }

    [Fact]
    public void CalculateDiscount_Fixed_CapsAtTotalPrice()
    {
        var voucher = Voucher.Create("FLAT50", VoucherDiscountType.Fixed, 50m, null, null, null);
        var result = voucher.CalculateDiscount(30m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(30m); // discount capped at total price
    }

    [Fact]
    public void CalculateDiscount_AfterDeactivate_ReturnsFailure()
    {
        var voucher = Voucher.Create("OLD", VoucherDiscountType.Fixed, 10m, null, null, null);
        voucher.Deactivate();

        var result = voucher.CalculateDiscount(100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.Inactive");
    }

    [Fact]
    public void CalculateDiscount_Expired_ReturnsFailure()
    {
        var voucher = Voucher.Create("EXP", VoucherDiscountType.Fixed, 10m, null,
            DateTimeOffset.UtcNow.AddDays(-1), null);

        var result = voucher.CalculateDiscount(100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.Expired");
    }

    [Fact]
    public void CalculateDiscount_MaxUsesReached_ReturnsFailure()
    {
        var voucher = Voucher.Create("LIMITED", VoucherDiscountType.Fixed, 10m, null, null, maxUses: 1);
        voucher.Redeem();

        var result = voucher.CalculateDiscount(100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.MaxUsesReached");
    }
}
```

- [ ] **Step 6: Escrever CreateVoucherCommandHandlerTests**

```csharp
// tests/Horafy.Application.Tests/Vouchers/CreateVoucherCommandHandlerTests.cs
using FluentAssertions;
using Horafy.Application.Features.Vouchers.Commands.CreateVoucher;
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Vouchers;

public sealed class CreateVoucherCommandHandlerTests
{
    private readonly Mock<IVoucherRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork>  _uow  = new();

    private CreateVoucherCommandHandler MakeHandler() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesVoucher()
    {
        _repo.Setup(r => r.CodeExistsAsync("PROMO20", default)).ReturnsAsync(false);

        var result = await MakeHandler().Handle(
            new CreateVoucherCommand("PROMO20", VoucherDiscountType.Percentage, 20m, null, null, null),
            default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.Is<Voucher>(v => v.Code == "PROMO20" && v.DiscountValue == 20m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateCode_ReturnsConflict()
    {
        _repo.Setup(r => r.CodeExistsAsync("PROMO20", default)).ReturnsAsync(true);

        var result = await MakeHandler().Handle(
            new CreateVoucherCommand("PROMO20", VoucherDiscountType.Fixed, 10m, null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.CodeAlreadyExists");
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_PercentageOver100_ReturnsValidationError()
    {
        var result = await MakeHandler().Handle(
            new CreateVoucherCommand("FULL", VoucherDiscountType.Percentage, 150m, null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Voucher.InvalidPercentage");
    }
}
```

- [ ] **Step 7: Executar todos os testes do Task 3 e 4**

```powershell
dotnet test tests/Horafy.Application.Tests --filter "Wallet|Voucher" 2>&1 | Select-Object -Last 10
```

Expected: `Aprovado! – Com falha: 0, Aprovado: 11, Ignorado: 0`.

- [ ] **Step 8: Commit**

```powershell
git add src/Horafy.Application/Features/Vouchers/ tests/Horafy.Application.Tests/Vouchers/
git commit -m "feat: add Voucher queries, commands and domain tests"
```

---

### Task 5: Backend Infrastructure — Repositories + EF Configs + Migration

**Files:**
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/WalletEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/WalletTransactionEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Persistence/TenantConfigurations/VoucherEntityConfiguration.cs`
- Create: `src/Horafy.Infrastructure/Repositories/WalletRepository.cs`
- Create: `src/Horafy.Infrastructure/Repositories/VoucherRepository.cs`
- Modify: `src/Horafy.Infrastructure/Persistence/TenantDbContext.cs` (add DbSets)
- Modify: `src/Horafy.Infrastructure/DependencyInjection.cs` (register repos)

- [ ] **Step 1: Ler TenantDbContext para saber onde inserir os DbSets**

```powershell
Get-Content "C:\Projetos\JEL\JEL\Horafy\src\Horafy.Infrastructure\Persistence\TenantDbContext.cs" | Select-Object -First 40
```

Localizar a seção de DbSets e adicionar após o último DbSet existente:

```csharp
public DbSet<Horafy.Domain.Entities.Wallet.Wallet> Wallets => Set<Horafy.Domain.Entities.Wallet.Wallet>();
public DbSet<Horafy.Domain.Entities.Wallet.WalletTransaction> WalletTransactions => Set<Horafy.Domain.Entities.Wallet.WalletTransaction>();
public DbSet<Horafy.Domain.Entities.Vouchers.Voucher> Vouchers => Set<Horafy.Domain.Entities.Vouchers.Voucher>();
```

- [ ] **Step 2: Criar WalletEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/WalletEntityConfiguration.cs
using Horafy.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

public sealed class WalletEntityConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.UserId).IsRequired();
        builder.Property(w => w.Balance).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(w => w.UserId);

        builder.HasMany(w => w.Transactions)
               .WithOne()
               .HasForeignKey(t => t.WalletId)
               .OnDelete(DeleteBehavior.Cascade);

        // Configura o backing field privado _transactions
        builder.Navigation(w => w.Transactions)
               .HasField("_transactions")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 3: Criar WalletTransactionEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/WalletTransactionEntityConfiguration.cs
using Horafy.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

public sealed class WalletTransactionEntityConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.WalletId).IsRequired();
        builder.Property(t => t.Type).IsRequired();
        builder.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
        builder.Property(t => t.BookingId);
        builder.HasIndex(t => t.WalletId);
    }
}
```

- [ ] **Step 4: Criar VoucherEntityConfiguration**

```csharp
// src/Horafy.Infrastructure/Persistence/TenantConfigurations/VoucherEntityConfiguration.cs
using Horafy.Domain.Entities.Vouchers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.TenantConfigurations;

public sealed class VoucherEntityConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Code).HasMaxLength(50).IsRequired();
        builder.Property(v => v.DiscountType).IsRequired();
        builder.Property(v => v.DiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(v => v.Description).HasMaxLength(500);
        builder.Property(v => v.ExpiresAt);
        builder.Property(v => v.MaxUses);
        builder.Property(v => v.UsedCount).IsRequired();
        builder.Property(v => v.IsActive).IsRequired();
        builder.HasIndex(v => v.Code);
        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
```

- [ ] **Step 5: Criar WalletRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/WalletRepository.cs
using Horafy.Domain.Entities.Wallet;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

public sealed class WalletRepository(TenantDbContext context)
    : BaseRepository<Wallet, TenantDbContext>(context), IWalletRepository
{
    public async Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await Context.Wallets
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);
}
```

- [ ] **Step 6: Criar VoucherRepository**

```csharp
// src/Horafy.Infrastructure/Repositories/VoucherRepository.cs
using Horafy.Domain.Entities.Vouchers;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Horafy.Infrastructure.Repositories;

public sealed class VoucherRepository(TenantDbContext context)
    : BaseRepository<Voucher, TenantDbContext>(context), IVoucherRepository
{
    public async Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        await Context.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == code, ct);

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        await Context.Vouchers.AnyAsync(v => v.Code == code, ct);
}
```

- [ ] **Step 7: Registrar repositórios em DependencyInjection.cs**

Ler `src/Horafy.Infrastructure/DependencyInjection.cs`. Localizar o bloco de registro de repositórios (deve conter linhas como `services.AddScoped<IBookingRepository, BookingRepository>()`). Adicionar:

```csharp
services.AddScoped<IWalletRepository, WalletRepository>();
services.AddScoped<IVoucherRepository, VoucherRepository>();
```

- [ ] **Step 8: Build para verificar compilação**

```powershell
dotnet build src/Horafy.Infrastructure 2>&1 | Select-Object -Last 5
```

Expected: `0 Erro(s)`. Se houver erros de namespace nas configurações EF, verificar se os namespaces batem com os existentes no projeto.

- [ ] **Step 9: Gerar migration**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet ef migrations add AddWalletAndVouchers `
  --context TenantDbContext `
  --project src/Horafy.Infrastructure `
  --startup-project src/Horafy.API `
  2>&1 | Select-Object -Last 10
```

Expected: `Build succeeded.` e `Done. To undo this action, use 'ef migrations remove'`.

Se o comando falhar com "context not found", verificar o namespace correto de `TenantDbContext` e usar `--context Horafy.Infrastructure.Persistence.TenantDbContext`.

- [ ] **Step 10: Commit**

```powershell
git add src/Horafy.Infrastructure/
git commit -m "feat: add Wallet and Voucher EF configs, repositories and migration"
```

---

### Task 6: Backend API — WalletController + VouchersController

**Files:**
- Create: `src/Horafy.API/Controllers/V1/WalletController.cs`
- Create: `src/Horafy.API/Controllers/V1/VouchersController.cs`

- [ ] **Step 1: Criar WalletController**

```csharp
// src/Horafy.API/Controllers/V1/WalletController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Wallet.Commands.AddCredits;
using Horafy.Application.Features.Wallet.Queries.GetWallet;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class WalletController(ISender sender) : ApiControllerBase(sender)
{
    /// <summary>Retorna o saldo e extrato da carteira do usuário autenticado.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(WalletResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWallet(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetWalletQuery(), ct));

    /// <summary>Adiciona créditos à carteira de um cliente (TenantOwner/Admin only).</summary>
    [HttpPost("users/{userId:guid}/credits")]
    [Authorize(Roles = "TenantOwner,TenantAdmin,PlatformAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddCredits(
        Guid userId,
        [FromBody] AddCreditsRequest request,
        CancellationToken ct)
    {
        var result = await Sender.Send(new AddCreditsCommand(userId, request.Amount, request.Description), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record AddCreditsRequest(decimal Amount, string Description);
```

- [ ] **Step 2: Criar VouchersController**

```csharp
// src/Horafy.API/Controllers/V1/VouchersController.cs
using Asp.Versioning;
using Horafy.API.Controllers.Base;
using Horafy.Application.Features.Vouchers.Commands.CreateVoucher;
using Horafy.Application.Features.Vouchers.Commands.DeactivateVoucher;
using Horafy.Application.Features.Vouchers.Queries.GetVouchers;
using Horafy.Application.Features.Vouchers.Queries.ValidateVoucher;
using Horafy.Domain.Entities.Vouchers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.V1;

[ApiVersion(1)]
public sealed class VouchersController(ISender sender) : ApiControllerBase(sender)
{
    /// <summary>Lista todos os vouchers do tenant (admin).</summary>
    [HttpGet]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<VoucherSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        ToActionResult(await Sender.Send(new GetVouchersQuery(), ct));

    /// <summary>Valida um código de voucher e retorna o desconto calculado.</summary>
    [HttpGet("validate")]
    [Authorize]
    [ProducesResponseType(typeof(VoucherValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Validate(
        [FromQuery] string code,
        [FromQuery] decimal totalPrice,
        CancellationToken ct) =>
        ToActionResult(await Sender.Send(new ValidateVoucherQuery(code, totalPrice), ct));

    /// <summary>Cria um novo voucher de desconto (admin).</summary>
    [HttpPost]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVoucherRequest request,
        CancellationToken ct)
    {
        var result = await Sender.Send(new CreateVoucherCommand(
            request.Code, request.DiscountType, request.DiscountValue,
            request.Description, request.ExpiresAt, request.MaxUses), ct);

        if (result.IsFailure) return ToActionResult(result);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Desativa um voucher (admin).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "TenantOwner,TenantAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new DeactivateVoucherCommand(id), ct);
        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }
}

public sealed record CreateVoucherRequest(
    string              Code,
    VoucherDiscountType DiscountType,
    decimal             DiscountValue,
    string?             Description,
    DateTimeOffset?     ExpiresAt,
    int?                MaxUses);
```

- [ ] **Step 3: Build da API**

```powershell
dotnet build src/Horafy.API 2>&1 | Select-Object -Last 5
```

Expected: `0 Erro(s)`.

- [ ] **Step 4: Commit**

```powershell
git add src/Horafy.API/Controllers/V1/WalletController.cs src/Horafy.API/Controllers/V1/VouchersController.cs
git commit -m "feat: add WalletController and VouchersController endpoints"
```

---

### Task 7: Frontend Types + API client

**Files:**
- Create: `frontend/lib/types/wallet.ts`
- Create: `frontend/lib/api/wallet.ts`

- [ ] **Step 1: Criar `frontend/lib/types/wallet.ts`**

```typescript
// frontend/lib/types/wallet.ts
export type WalletTransactionType = 'CreditAdded' | 'BookingPayment' | 'BookingRefund' | 'LoyaltyBonus'
export type VoucherDiscountType = 'Percentage' | 'Fixed'

export interface WalletTransaction {
  id: string
  type: WalletTransactionType
  amount: number
  description: string
  bookingId?: string
  createdAt: string
}

export interface WalletBalance {
  walletId: string
  balance: number
  transactions: WalletTransaction[]
}

export interface VoucherSummary {
  id: string
  code: string
  discountType: VoucherDiscountType
  discountValue: number
  description?: string
  expiresAt?: string
  maxUses?: number
  usedCount: number
  isActive: boolean
  createdAt: string
}

export interface VoucherValidation {
  code: string
  discountType: VoucherDiscountType
  discountValue: number
  discountAmount: number
  finalPrice: number
  description?: string
}
```

- [ ] **Step 2: Criar `frontend/lib/api/wallet.ts`**

```typescript
// frontend/lib/api/wallet.ts
import { apiFetch } from './client'
import type { WalletBalance, VoucherSummary, VoucherValidation, VoucherDiscountType } from '../types/wallet'

// Usa apiFetch (access_token) — para contexto admin
export const walletApi = {
  addCredits: (userId: string, amount: number, description: string) =>
    apiFetch<void>(`/api/v1/wallet/users/${userId}/credits`, {
      method: 'POST',
      body: JSON.stringify({ amount, description }),
    }),

  getVouchers: () =>
    apiFetch<VoucherSummary[]>('/api/v1/vouchers'),

  createVoucher: (data: {
    code: string
    discountType: VoucherDiscountType
    discountValue: number
    description?: string
    expiresAt?: string
    maxUses?: number
  }) => apiFetch<string>('/api/v1/vouchers', {
    method: 'POST',
    body: JSON.stringify(data),
  }),

  deactivateVoucher: (id: string) =>
    apiFetch<void>(`/api/v1/vouchers/${id}`, { method: 'DELETE' }),
}

// Usa portal_access_token — para contexto cliente portal
const PORTAL_API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'

async function portalWalletFetch<T>(path: string, token: string): Promise<T> {
  const res = await fetch(`${PORTAL_API}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  })
  if (res.status === 204) return undefined as T
  if (!res.ok) throw new Error(`HTTP ${res.status}`)
  return res.json()
}

export const portalWalletApi = {
  getMyWallet: (token: string) =>
    portalWalletFetch<WalletBalance>('/api/v1/wallet', token),

  validateVoucher: (token: string, code: string, totalPrice: number) =>
    portalWalletFetch<VoucherValidation>(
      `/api/v1/vouchers/validate?code=${encodeURIComponent(code)}&totalPrice=${totalPrice}`,
      token
    ),
}
```

- [ ] **Step 3: Commit**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git add frontend/lib/types/wallet.ts frontend/lib/api/wallet.ts
git commit -m "feat: add wallet types and API client"
```

---

### Task 8: Frontend Admin — Página Carteira

**Files:**
- Create: `frontend/app/(admin)/admin/carteira/page.tsx`
- Create: `frontend/__tests__/CarteiraAdmin.test.tsx`
- Modify: `frontend/components/admin/Sidebar.tsx` (adicionar link Carteira)

- [ ] **Step 1: Escrever teste**

```typescript
// frontend/__tests__/CarteiraAdmin.test.tsx
import { render, screen, waitFor } from '@testing-library/react'
import { vi } from 'vitest'

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }))

vi.mock('@/lib/api/wallet', () => ({
  walletApi: {
    getVouchers: vi.fn().mockResolvedValue([
      { id: '1', code: 'PROMO20', discountType: 'Percentage', discountValue: 20,
        usedCount: 3, isActive: true, createdAt: '2026-06-01T00:00:00Z' },
    ]),
    createVoucher: vi.fn().mockResolvedValue('new-id'),
    deactivateVoucher: vi.fn().mockResolvedValue(undefined),
    addCredits: vi.fn().mockResolvedValue(undefined),
  },
}))

import CarteiraPage from '@/app/(admin)/admin/carteira/page'

describe('CarteiraPage', () => {
  it('renders voucher code after loading', async () => {
    render(<CarteiraPage />)
    await waitFor(() => {
      expect(screen.getByText('PROMO20')).toBeInTheDocument()
    })
  })

  it('shows voucher discount value', async () => {
    render(<CarteiraPage />)
    await waitFor(() => {
      expect(screen.getByText(/20%/i)).toBeInTheDocument()
    })
  })
})
```

- [ ] **Step 2: Executar para verificar FAIL**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npm run test:run -- __tests__/CarteiraAdmin.test.tsx 2>&1 | Select-Object -Last 5
```

Expected: FAIL — `CarteiraPage` não existe.

- [ ] **Step 3: Criar `frontend/app/(admin)/admin/carteira/page.tsx`**

```typescript
// frontend/app/(admin)/admin/carteira/page.tsx
'use client'

import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { walletApi } from '@/lib/api/wallet'
import type { VoucherSummary, VoucherDiscountType } from '@/lib/types/wallet'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { format } from 'date-fns'
import { ptBR } from 'date-fns/locale'

type Tab = 'vouchers' | 'creditos'

// ── Vouchers Tab ─────────────────────────────────────────────────────────────

function VouchersTab() {
  const [vouchers, setVouchers] = useState<VoucherSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [creating, setCreating] = useState(false)
  const { register, handleSubmit, setValue, reset, formState: { errors } } = useForm<{
    code: string; discountType: VoucherDiscountType; discountValue: string
    description: string; maxUses: string; expiresAt: string
  }>({ defaultValues: { discountType: 'Percentage' } })

  useEffect(() => {
    walletApi.getVouchers().then(setVouchers).catch(() => {}).finally(() => setLoading(false))
  }, [])

  const onSubmit = async (data: {
    code: string; discountType: VoucherDiscountType; discountValue: string
    description: string; maxUses: string; expiresAt: string
  }) => {
    setCreating(true)
    try {
      await walletApi.createVoucher({
        code: data.code,
        discountType: data.discountType,
        discountValue: parseFloat(data.discountValue),
        description: data.description || undefined,
        maxUses: data.maxUses ? parseInt(data.maxUses) : undefined,
        expiresAt: data.expiresAt || undefined,
      })
      const updated = await walletApi.getVouchers()
      setVouchers(updated)
      reset()
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro ao criar voucher')
    } finally {
      setCreating(false)
    }
  }

  const handleDeactivate = async (id: string) => {
    if (!confirm('Desativar este voucher?')) return
    await walletApi.deactivateVoucher(id)
    setVouchers(vs => vs.map(v => v.id === id ? { ...v, isActive: false } : v))
  }

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      {/* Create form */}
      <Card>
        <CardHeader><CardTitle className="text-base">Novo Voucher</CardTitle></CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
            <div>
              <Label>Código</Label>
              <Input {...register('code', { required: 'Obrigatório' })} placeholder="PROMO20" className="uppercase" />
              {errors.code && <p className="text-xs text-red-500 mt-1">{errors.code.message}</p>}
            </div>
            <div>
              <Label>Tipo de Desconto</Label>
              <Select defaultValue="Percentage" onValueChange={v => setValue('discountType', (v || 'Percentage') as VoucherDiscountType)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="Percentage">Percentual (%)</SelectItem>
                  <SelectItem value="Fixed">Valor Fixo (R$)</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Valor do Desconto</Label>
              <Input {...register('discountValue', { required: 'Obrigatório' })} type="number" step="0.01" placeholder="20" />
              {errors.discountValue && <p className="text-xs text-red-500 mt-1">{errors.discountValue.message}</p>}
            </div>
            <div>
              <Label>Descrição (opcional)</Label>
              <Input {...register('description')} placeholder="Desconto de boas-vindas" />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Máx. usos</Label>
                <Input {...register('maxUses')} type="number" placeholder="Ilimitado" />
              </div>
              <div>
                <Label>Expira em</Label>
                <Input {...register('expiresAt')} type="datetime-local" />
              </div>
            </div>
            <Button type="submit" className="w-full" disabled={creating}>
              {creating ? 'Criando...' : 'Criar Voucher'}
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Voucher list */}
      <Card>
        <CardHeader><CardTitle className="text-base">Vouchers Ativos</CardTitle></CardHeader>
        <CardContent>
          {loading ? <p className="text-slate-500 text-sm">Carregando...</p> : (
            <div className="space-y-2">
              {vouchers.length === 0 && <p className="text-slate-400 text-sm">Nenhum voucher criado.</p>}
              {vouchers.map(v => (
                <div key={v.id} className="flex items-center justify-between p-3 border rounded-lg">
                  <div>
                    <span className="font-mono font-bold text-sm">{v.code}</span>
                    <span className="ml-2 text-xs text-slate-500">
                      {v.discountType === 'Percentage' ? `${v.discountValue}%` : `R$ ${v.discountValue}`}
                    </span>
                    <p className="text-xs text-slate-400">{v.usedCount} usos{v.maxUses ? ` / ${v.maxUses}` : ''}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`text-xs px-2 py-0.5 rounded-full ${v.isActive ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'}`}>
                      {v.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                    {v.isActive && (
                      <Button size="sm" variant="outline" onClick={() => handleDeactivate(v.id)}>
                        Desativar
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

// ── Credits Tab ───────────────────────────────────────────────────────────────

function CreditosTab() {
  const [loading, setLoading] = useState(false)
  const [success, setSuccess] = useState(false)
  const { register, handleSubmit, reset, formState: { errors } } = useForm<{
    userId: string; amount: string; description: string
  }>()

  const onSubmit = async (data: { userId: string; amount: string; description: string }) => {
    setLoading(true)
    setSuccess(false)
    try {
      await walletApi.addCredits(data.userId, parseFloat(data.amount), data.description)
      setSuccess(true)
      reset()
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Erro ao adicionar créditos')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Card className="max-w-md">
      <CardHeader><CardTitle className="text-base">Adicionar Créditos a Cliente</CardTitle></CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <Label>ID do Cliente (UUID)</Label>
            <Input {...register('userId', { required: 'Obrigatório' })} placeholder="xxxxxxxx-xxxx-..." />
            {errors.userId && <p className="text-xs text-red-500 mt-1">{errors.userId.message}</p>}
          </div>
          <div>
            <Label>Valor (R$)</Label>
            <Input {...register('amount', { required: 'Obrigatório' })} type="number" step="0.01" placeholder="50.00" />
            {errors.amount && <p className="text-xs text-red-500 mt-1">{errors.amount.message}</p>}
          </div>
          <div>
            <Label>Motivo</Label>
            <Input {...register('description', { required: 'Obrigatório' })} placeholder="Ex: Compensação por cancelamento" />
            {errors.description && <p className="text-xs text-red-500 mt-1">{errors.description.message}</p>}
          </div>
          {success && (
            <div className="p-3 bg-green-50 text-green-700 rounded-lg text-sm">
              Créditos adicionados com sucesso.
            </div>
          )}
          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? 'Adicionando...' : 'Adicionar Créditos'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function CarteiraPage() {
  const [tab, setTab] = useState<Tab>('vouchers')

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-slate-900">Carteira & Vouchers</h1>
        <p className="text-slate-500 text-sm">Gerencie créditos de clientes e cupons de desconto</p>
      </div>

      <div className="flex gap-2 mb-6">
        {(['vouchers', 'creditos'] as Tab[]).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
              tab === t ? 'bg-indigo-600 text-white' : 'bg-white border text-slate-600 hover:bg-slate-50'
            }`}
          >
            {t === 'vouchers' ? 'Vouchers' : 'Créditos'}
          </button>
        ))}
      </div>

      {tab === 'vouchers' ? <VouchersTab /> : <CreditosTab />}
    </div>
  )
}
```

- [ ] **Step 4: Adicionar link no Sidebar admin**

Ler `frontend/components/admin/Sidebar.tsx`. Localizar o array NAV e adicionar antes de Settings:

```typescript
{ href: '/admin/carteira', label: 'Carteira', icon: Wallet2 },
```

E adicionar o import:
```typescript
import { ..., Wallet2 } from 'lucide-react'
```

- [ ] **Step 5: Executar testes**

```powershell
npm run test:run -- __tests__/CarteiraAdmin.test.tsx 2>&1 | Select-Object -Last 10
```

Expected: 2 testes passando.

- [ ] **Step 6: Commit**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git add "frontend/app/(admin)/admin/carteira/page.tsx" frontend/__tests__/CarteiraAdmin.test.tsx frontend/components/admin/Sidebar.tsx
git commit -m "feat: add admin Carteira page with vouchers and credits management"
```

---

### Task 9: Frontend Portal — Wallet balance em minha-conta

**Files:**
- Modify: `frontend/app/(portal)/[slug]/minha-conta/page.tsx`

- [ ] **Step 1: Ler `frontend/app/(portal)/[slug]/minha-conta/page.tsx`**

Ler o arquivo para identificar onde inserir o widget de saldo e como a autenticação do portal funciona (cookie `portal_access_token`).

- [ ] **Step 2: Adicionar fetch de saldo e seção de Carteira**

Após a linha de imports, adicionar:

```typescript
import { portalWalletApi } from '@/lib/api/wallet'
import type { WalletBalance } from '@/lib/types/wallet'
```

Dentro do componente, adicionar state e useEffect para buscar o saldo:

```typescript
const [wallet, setWallet] = useState<WalletBalance | null>(null)

useEffect(() => {
  // Lê o token do cookie do portal (definido no login via Google OAuth)
  const token = document.cookie
    .split('; ')
    .find(r => r.startsWith('portal_access_token='))
    ?.split('=')[1]
  if (!token) return
  portalWalletApi.getMyWallet(token).then(setWallet).catch(() => {})
}, [])
```

E antes da seção de agendamentos, adicionar:

```tsx
{/* Carteira */}
<section className="mb-8">
  <h2 className="text-xl font-bold mb-4">Carteira de Créditos</h2>
  <div className="p-4 border rounded-xl bg-indigo-50 inline-block">
    <p className="text-sm text-indigo-600 font-medium">Saldo disponível</p>
    <p className="text-3xl font-bold text-indigo-900">
      R$ {(wallet?.balance ?? 0).toFixed(2)}
    </p>
  </div>
  {wallet && wallet.transactions.length > 0 && (
    <div className="mt-4 space-y-2">
      {wallet.transactions.slice(0, 5).map(t => (
        <div key={t.id} className="flex items-center justify-between text-sm py-2 border-b last:border-0">
          <span className="text-slate-600">{t.description}</span>
          <span className={t.type === 'BookingPayment' ? 'text-red-600' : 'text-green-600'}>
            {t.type === 'BookingPayment' ? '-' : '+'}R$ {t.amount.toFixed(2)}
          </span>
        </div>
      ))}
    </div>
  )}
</section>
```

**Atenção:** Se o componente for Server Component (não tiver `'use client'`), o `useEffect`/`useState` não funcionam diretamente. Nesse caso, encapsular a seção de carteira num sub-componente client:

```typescript
// Criar frontend/components/portal/WalletWidget.tsx
'use client'
import { useEffect, useState } from 'react'
import { portalWalletApi } from '@/lib/api/wallet'
import type { WalletBalance } from '@/lib/types/wallet'

export function WalletWidget() {
  const [wallet, setWallet] = useState<WalletBalance | null>(null)

  useEffect(() => {
    const token = document.cookie
      .split('; ')
      .find(r => r.startsWith('portal_access_token='))
      ?.split('=')[1]
    if (!token) return
    portalWalletApi.getMyWallet(token).then(setWallet).catch(() => {})
  }, [])

  return (
    <section className="mb-8">
      <h2 className="text-xl font-bold mb-4">Carteira de Créditos</h2>
      <div className="p-4 border rounded-xl bg-indigo-50 inline-block">
        <p className="text-sm text-indigo-600 font-medium">Saldo disponível</p>
        <p className="text-3xl font-bold text-indigo-900">
          R$ {(wallet?.balance ?? 0).toFixed(2)}
        </p>
      </div>
      {wallet && wallet.transactions.length > 0 && (
        <div className="mt-4 space-y-2">
          {wallet.transactions.slice(0, 5).map(t => (
            <div key={t.id} className="flex items-center justify-between text-sm py-2 border-b last:border-0">
              <span className="text-slate-600">{t.description}</span>
              <span className={t.type === 'BookingPayment' ? 'text-red-600' : 'text-green-600'}>
                {t.type === 'BookingPayment' ? '-' : '+'}R$ {t.amount.toFixed(2)}
              </span>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
```

E importar `<WalletWidget />` no `minha-conta/page.tsx`.

- [ ] **Step 3: Build para verificar tipos**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npx tsc --noEmit 2>&1 | Select-Object -Last 10
```

Expected: sem erros de tipo.

- [ ] **Step 4: Commit**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
git add frontend/components/portal/WalletWidget.tsx "frontend/app/(portal)/[slug]/minha-conta/page.tsx"
git commit -m "feat: add wallet balance widget to portal minha-conta"
```

---

### Task 10: Full Suite + Build Final

- [ ] **Step 1: Backend — todos os testes**

```powershell
cd C:\Projetos\JEL\JEL\Horafy
dotnet test 2>&1 | Select-Object -Last 5
```

Expected: `Com falha: 0` — todos os testes passando.

- [ ] **Step 2: Frontend — toda a suíte**

```powershell
cd C:\Projetos\JEL\JEL\Horafy\frontend
npm run test:run 2>&1 | Select-Object -Last 5
```

Expected: 20+ arquivos, 40+ testes passando.

- [ ] **Step 3: Build de produção**

```powershell
npm run build 2>&1 | Select-Object -Last 25
```

Expected: build sem erros. Rotas novas esperadas:
- `○ /admin/carteira`

Se houver erro `'use client'` em WalletWidget sendo importado de Server Component, verificar se a página `minha-conta` é server ou client e ajustar conforme descrito no Task 9.

---

## Self-Review

### Spec Coverage

| Requisito | Task | Status |
|-----------|------|--------|
| Wallet de créditos — entidade + saldo | Tasks 1, 3 | ✅ |
| Admin adiciona créditos a cliente | Tasks 3, 6, 8 | ✅ |
| Cliente vê saldo na área logada | Tasks 7, 9 | ✅ |
| Voucher — criar/desativar (admin) | Tasks 2, 4, 6, 8 | ✅ |
| Voucher — validar código (query) | Tasks 4, 6 | ✅ |
| Voucher — tipo Percentage e Fixed | Task 2 (CalculateDiscount) | ✅ |
| Integração voucher no checkout | — | ⏭ Sprint 14 |
| Créditos aplicados no checkout | — | ⏭ Sprint 14 |
| Programa de fidelidade (pontos) | — | ⏭ Sprint 14 |

### Placeholder Scan

Sem TBDs. Todo código é concreto.

Task 9 tem variação condicional (server vs client component) — ambos os caminhos estão documentados com código completo.

### Type Consistency

- `WalletBalance.walletId: string` — retornado como `Guid Id` do backend (serializado como string), mapeado corretamente
- `walletApi.addCredits(userId, amount, description)` — chamado em `CreditosTab` com exatamente esses 3 argumentos
- `walletApi.createVoucher(data)` — shape com `code, discountType, discountValue, description?, expiresAt?, maxUses?` — bate com `CreateVoucherRequest` no backend
- `VoucherSummary.discountType: VoucherDiscountType` — `'Percentage' | 'Fixed'` — bate com o enum C# serializado como string
- `portalWalletApi.getMyWallet(token)` — definido em `wallet.ts`, importado em `WalletWidget.tsx` com a mesma assinatura
