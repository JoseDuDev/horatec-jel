# Admin Booking Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que o admin crie agendamentos manualmente (clientes presenciais ou por telefone) pela página de Agendamentos.

**Architecture:** Novo `AdminCreateBookingCommand` que aceita dados do cliente diretamente (sem depender do `currentUser`) — permite criar reservas para qualquer pessoa sem conta no sistema. Novo endpoint `POST /api/v1/bookings/admin` restrito a TenantOwner/TenantAdmin/TenantStaff. Frontend: modal de 3 passos (Serviço+Recurso → Data+Horário → Cliente) integrado à página de Agendamentos. Também adiciona o botão "Não Compareceu" que já está implementado no handler mas faltava na UI.

**Tech Stack:** .NET 9 / MediatR / FluentValidation / xUnit + Moq + FluentAssertions; Next.js 15 / TypeScript / Shadcn UI / date-fns

---

## Arquivos criados/modificados

| Arquivo | Ação |
|---|---|
| `src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs` | Criar |
| `tests/Horafy.Application.Tests/Bookings/AdminCreateBookingCommandHandlerTests.cs` | Criar |
| `src/Horafy.API/Controllers/V1/BookingsController.cs` | Modificar: novo endpoint + DTO |
| `frontend/lib/types/booking.ts` | Modificar: adicionar `AdminCreateBookingRequest` |
| `frontend/lib/api/bookings.ts` | Modificar: adicionar `adminCreate` |
| `frontend/lib/api/availability.ts` | Modificar: adicionar `getSlots` |
| `frontend/components/bookings/AdminBookingModal.tsx` | Criar |
| `frontend/app/(admin)/admin/agendamentos/page.tsx` | Modificar: botão + modal |
| `frontend/components/bookings/BookingTable.tsx` | Modificar: botão No Show |

---

## Task 1: AdminCreateBookingCommand

**Files:**
- Create: `src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs`
- Create: `tests/Horafy.Application.Tests/Bookings/AdminCreateBookingCommandHandlerTests.cs`

- [ ] **Step 1: Escrever os testes que devem falhar**

Criar `tests/Horafy.Application.Tests/Bookings/AdminCreateBookingCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using Horafy.Application.Features.Bookings;
using Horafy.Application.Features.Bookings.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Bookings;

public sealed class AdminCreateBookingCommandHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly DateTimeOffset FutureSlot = DateTimeOffset.UtcNow.AddDays(1);

    private readonly Mock<IServiceRepository>  _services  = new();
    private readonly Mock<IResourceRepository> _resources = new();
    private readonly Mock<IBookingRepository>  _bookings  = new();
    private readonly Mock<ITenantUnitOfWork>   _uow       = new();

    private AdminCreateBookingCommandHandler MakeHandler() =>
        new(_services.Object, _resources.Object, _bookings.Object, _uow.Object);

    private static Resource MakeResource() =>
        Resource.Create("Sala 1", ResourceType.Professional);

    private static Service MakeService() =>
        Service.Create("Corte", 60, 50m);

    [Fact]
    public async Task Handle_RecursoNaoEncontrado_RetornaFalha()
    {
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Resource?)null);

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [Guid.NewGuid()], ResourceId, FutureSlot,
                "João Silva", null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.ResourceNotFound);
        _services.Verify(
            s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ServicoNaoEncontrado_RetornaFalha()
    {
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResource());
        _services.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service>());

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [Guid.NewGuid()], ResourceId, FutureSlot,
                "João Silva", null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.ServiceNotFound);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HorarioComConflito_RetornaFalha()
    {
        var service = MakeService();
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResource());
        _services.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service> { service });
        _bookings.Setup(b => b.HasConflictAsync(
                ResourceId,
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [service.Id], ResourceId, FutureSlot,
                "João Silva", null, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.Conflict);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DadosValidos_CriaAgendamentoESalva()
    {
        var service = MakeService();
        _resources.Setup(r => r.GetByIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResource());
        _services.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service> { service });
        _bookings.Setup(b => b.HasConflictAsync(
                ResourceId,
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await MakeHandler().Handle(
            new AdminCreateBookingCommand(
                [service.Id], ResourceId, FutureSlot,
                "João Silva", "joao@email.com", "11999999999", "Obs"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _bookings.Verify(b => b.Add(It.IsAny<Booking>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Rodar os testes para confirmar que falham**

```bash
dotnet test tests/Horafy.Application.Tests/Horafy.Application.Tests.csproj --filter "AdminCreateBookingCommandHandlerTests"
```

Expected: falha com `CS0246: The type or namespace name 'AdminCreateBookingCommandHandler' could not be found`

- [ ] **Step 3: Criar o command e o handler**

Criar `src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs`:

```csharp
using FluentValidation;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Shared;
using MediatR;

namespace Horafy.Application.Features.Bookings.Commands;

public sealed record AdminCreateBookingCommand(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class AdminCreateBookingCommandValidator
    : AbstractValidator<AdminCreateBookingCommand>
{
    public AdminCreateBookingCommandValidator()
    {
        RuleFor(x => x.ServiceIds)
            .NotEmpty().WithMessage("Pelo menos um serviço é obrigatório.");
        RuleForEach(x => x.ServiceIds).NotEmpty();
        RuleFor(x => x.ResourceId).NotEmpty();
        RuleFor(x => x.ScheduledAt)
            .Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("O horário deve ser futuro.");
        RuleFor(x => x.CustomerName)
            .NotEmpty().MaximumLength(200);
    }
}

internal sealed class AdminCreateBookingCommandHandler(
    IServiceRepository  serviceRepository,
    IResourceRepository resourceRepository,
    IBookingRepository  bookingRepository,
    ITenantUnitOfWork   unitOfWork)
    : IRequestHandler<AdminCreateBookingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        AdminCreateBookingCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceRepository.GetByIdAsync(
            request.ResourceId, cancellationToken);
        if (resource is null)
            return Result.Failure<Guid>(BookingErrors.ResourceNotFound);

        var fetchedServices = await serviceRepository.GetByIdsAsync(
            request.ServiceIds, cancellationToken);
        var serviceMap = fetchedServices.ToDictionary(s => s.Id);

        var services = new List<(Guid ServiceId, string ServiceName, int DurationMinutes)>();
        foreach (var serviceId in request.ServiceIds)
        {
            if (!serviceMap.TryGetValue(serviceId, out var service))
                return Result.Failure<Guid>(BookingErrors.ServiceNotFound);
            services.Add((service.Id, service.Name, service.DurationMinutes));
        }

        var totalDuration = services.Sum(s => s.DurationMinutes);
        var endsAt = request.ScheduledAt.AddMinutes(totalDuration);

        var hasConflict = await bookingRepository.HasConflictAsync(
            request.ResourceId, request.ScheduledAt, endsAt,
            cancellationToken: cancellationToken);
        if (hasConflict)
            return Result.Failure<Guid>(BookingErrors.Conflict);

        var booking = Booking.Create(
            services,
            request.ResourceId,
            customerId:    Guid.NewGuid(),
            customerName:  request.CustomerName,
            customerEmail: request.CustomerEmail ?? string.Empty,
            scheduledAt:   request.ScheduledAt,
            customerPhone: request.CustomerPhone,
            notes:         request.Notes);

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(booking.Id);
    }
}
```

- [ ] **Step 4: Rodar os testes para confirmar que passam**

```bash
dotnet test tests/Horafy.Application.Tests/Horafy.Application.Tests.csproj --filter "AdminCreateBookingCommandHandlerTests"
```

Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Compilar o projeto completo**

```bash
dotnet build src/Horafy.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/Horafy.Application/Features/Bookings/Commands/AdminCreateBookingCommand.cs
git add tests/Horafy.Application.Tests/Bookings/AdminCreateBookingCommandHandlerTests.cs
git commit -m "feat: add AdminCreateBookingCommand for manual admin booking creation"
```

---

## Task 2: Endpoint no BookingsController

**Files:**
- Modify: `src/Horafy.API/Controllers/V1/BookingsController.cs`

- [ ] **Step 1: Adicionar o DTO e o endpoint**

Em `src/Horafy.API/Controllers/V1/BookingsController.cs`, adicionar o record do DTO ao final do arquivo (após `CreateRecurringBookingRequest`):

```csharp
public sealed record AdminCreateBookingRequest(
    IReadOnlyList<Guid> ServiceIds,
    Guid ResourceId,
    DateTimeOffset ScheduledAt,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? Notes);
```

E adicionar o endpoint dentro da classe `BookingsController`, após o método `Create`:

```csharp
[HttpPost("admin")]
[Authorize(Roles = "TenantOwner,TenantAdmin,TenantStaff,PlatformAdmin")]
[ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> AdminCreate(
    [FromBody] AdminCreateBookingRequest request,
    CancellationToken cancellationToken)
{
    var result = await Sender.Send(
        new AdminCreateBookingCommand(
            request.ServiceIds,
            request.ResourceId,
            request.ScheduledAt,
            request.CustomerName,
            request.CustomerEmail,
            request.CustomerPhone,
            request.Notes),
        cancellationToken);

    if (result.IsFailure) return ToActionResult(result);
    return CreatedAtRoute("GetBookingById", new { id = result.Value }, result.Value);
}
```

- [ ] **Step 2: Compilar para garantir sem erros**

```bash
dotnet build src/Horafy.sln
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Horafy.API/Controllers/V1/BookingsController.cs
git commit -m "feat: add POST /api/v1/bookings/admin endpoint for manual booking creation"
```

---

## Task 3: Frontend — tipos e extensões da API client

**Files:**
- Modify: `frontend/lib/types/booking.ts`
- Modify: `frontend/lib/api/bookings.ts`
- Modify: `frontend/lib/api/availability.ts`

- [ ] **Step 1: Adicionar `AdminCreateBookingRequest` aos tipos**

Em `frontend/lib/types/booking.ts`, adicionar ao final do arquivo:

```typescript
export interface AdminCreateBookingRequest {
  serviceIds: string[]
  resourceId: string
  scheduledAt: string   // ISO 8601 — ex: "2026-06-10T14:00:00Z"
  customerName: string
  customerEmail?: string
  customerPhone?: string
  notes?: string
}
```

- [ ] **Step 2: Adicionar `adminCreate` ao bookingsApi**

Em `frontend/lib/api/bookings.ts`, adicionar após o método `noShow`:

```typescript
  adminCreate: (data: AdminCreateBookingRequest) =>
    apiFetch<string>('/api/v1/bookings/admin', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
```

E adicionar o import do novo tipo no topo do arquivo:

```typescript
import type { Booking, BookingStatus, AdminCreateBookingRequest } from '../types/booking'
```

- [ ] **Step 3: Adicionar `getSlots` ao availabilityApi**

Em `frontend/lib/api/availability.ts`, adicionar após o método `deleteResourceException`:

```typescript
  getSlots: (resourceId: string, date: string, serviceId?: string) => {
    const qs = new URLSearchParams({
      date,
      ...(serviceId ? { serviceId } : {}),
    }).toString()
    return apiFetch<string[]>(
      `/api/v1/availability/resources/${resourceId}/slots?${qs}`
    )
  },
```

- [ ] **Step 4: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit 2>&1 | grep -v "__tests__" | head -20
```

Expected: `0 errors`

- [ ] **Step 5: Commit**

```bash
git add frontend/lib/types/booking.ts
git add frontend/lib/api/bookings.ts
git add frontend/lib/api/availability.ts
git commit -m "feat: add adminCreate to bookingsApi and getSlots to availabilityApi"
```

---

## Task 4: AdminBookingModal component

**Files:**
- Create: `frontend/components/bookings/AdminBookingModal.tsx`

- [ ] **Step 1: Criar o componente**

Criar `frontend/components/bookings/AdminBookingModal.tsx`:

```tsx
'use client'

import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { servicesApi } from '@/lib/api/services'
import { resourcesApi } from '@/lib/api/resources'
import { availabilityApi } from '@/lib/api/availability'
import { bookingsApi } from '@/lib/api/bookings'
import type { Service } from '@/lib/types/service'
import type { Resource } from '@/lib/types/resource'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type Step = 1 | 2 | 3

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated: () => void
}

export function AdminBookingModal({ open, onOpenChange, onCreated }: Props) {
  const [step, setStep] = useState<Step>(1)
  const [services, setServices] = useState<Service[]>([])
  const [resources, setResources] = useState<Resource[]>([])

  // Passo 1
  const [serviceId, setServiceId] = useState('')
  const [resourceId, setResourceId] = useState('')

  // Passo 2
  const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [slots, setSlots] = useState<string[]>([])
  const [selectedSlot, setSelectedSlot] = useState('')
  const [loadingSlots, setLoadingSlots] = useState(false)

  // Passo 3
  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!open) return
    Promise.all([servicesApi.list(), resourcesApi.list()]).then(([s, r]) => {
      setServices(s)
      setResources(r)
    })
  }, [open])

  useEffect(() => {
    if (!resourceId || !date) return
    setLoadingSlots(true)
    setSlots([])
    setSelectedSlot('')
    availabilityApi
      .getSlots(resourceId, date, serviceId || undefined)
      .then(setSlots)
      .finally(() => setLoadingSlots(false))
  }, [resourceId, date, serviceId])

  const reset = () => {
    setStep(1)
    setServiceId('')
    setResourceId('')
    setDate(format(new Date(), 'yyyy-MM-dd'))
    setSlots([])
    setSelectedSlot('')
    setCustomerName('')
    setCustomerEmail('')
    setCustomerPhone('')
    setNotes('')
  }

  const handleOpenChange = (open: boolean) => {
    if (!open) reset()
    onOpenChange(open)
  }

  const handleConfirm = async () => {
    if (!selectedSlot || !customerName.trim()) return
    setSaving(true)
    try {
      await bookingsApi.adminCreate({
        serviceIds: [serviceId],
        resourceId,
        scheduledAt: selectedSlot,
        customerName: customerName.trim(),
        customerEmail: customerEmail || undefined,
        customerPhone: customerPhone || undefined,
        notes: notes || undefined,
      })
      reset()
      onOpenChange(false)
      onCreated()
    } finally {
      setSaving(false)
    }
  }

  const filteredResources = serviceId
    ? resources.filter(r => r.serviceIds.includes(serviceId))
    : resources

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Nova Reserva — Passo {step} de 3</DialogTitle>
        </DialogHeader>

        {step === 1 && (
          <div className="space-y-4">
            <div>
              <Label>Serviço</Label>
              <Select
                value={serviceId}
                onValueChange={v => {
                  setServiceId(v ?? '')
                  setResourceId('')
                }}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Selecione um serviço..." />
                </SelectTrigger>
                <SelectContent>
                  {services
                    .filter(s => s.isActive)
                    .map(s => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label>Recurso</Label>
              <Select
                value={resourceId}
                onValueChange={v => setResourceId(v ?? '')}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Selecione um recurso..." />
                </SelectTrigger>
                <SelectContent>
                  {filteredResources
                    .filter(r => r.isActive)
                    .map(r => (
                      <SelectItem key={r.id} value={r.id}>
                        {r.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex justify-end">
              <Button
                onClick={() => setStep(2)}
                disabled={!serviceId || !resourceId}
              >
                Próximo
              </Button>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-4">
            <div>
              <Label>Data</Label>
              <Input
                type="date"
                value={date}
                min={format(new Date(), 'yyyy-MM-dd')}
                onChange={e => setDate(e.target.value)}
                className="mt-1"
              />
            </div>

            {loadingSlots && (
              <p className="text-sm text-slate-500">Carregando horários...</p>
            )}

            {!loadingSlots && date && slots.length === 0 && (
              <p className="text-sm text-slate-400">
                Nenhum horário disponível nesta data.
              </p>
            )}

            {slots.length > 0 && (
              <div>
                <Label>Horário disponível</Label>
                <div className="grid grid-cols-4 gap-2 mt-2">
                  {slots.map(slot => (
                    <button
                      key={slot}
                      type="button"
                      onClick={() => setSelectedSlot(slot)}
                      className={`rounded border px-2 py-1.5 text-sm transition-colors ${
                        selectedSlot === slot
                          ? 'bg-slate-900 text-white border-slate-900'
                          : 'border-slate-200 hover:border-slate-400'
                      }`}
                    >
                      {format(new Date(slot), 'HH:mm')}
                    </button>
                  ))}
                </div>
              </div>
            )}

            <div className="flex justify-between">
              <Button variant="outline" onClick={() => setStep(1)}>
                Voltar
              </Button>
              <Button onClick={() => setStep(3)} disabled={!selectedSlot}>
                Próximo
              </Button>
            </div>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-4">
            <div>
              <Label>Nome do cliente *</Label>
              <Input
                value={customerName}
                onChange={e => setCustomerName(e.target.value)}
                placeholder="Nome completo"
                className="mt-1"
              />
            </div>
            <div>
              <Label>E-mail</Label>
              <Input
                type="email"
                value={customerEmail}
                onChange={e => setCustomerEmail(e.target.value)}
                placeholder="email@exemplo.com"
                className="mt-1"
              />
            </div>
            <div>
              <Label>Telefone</Label>
              <Input
                value={customerPhone}
                onChange={e => setCustomerPhone(e.target.value)}
                placeholder="(11) 99999-9999"
                className="mt-1"
              />
            </div>
            <div>
              <Label>Observações</Label>
              <Input
                value={notes}
                onChange={e => setNotes(e.target.value)}
                placeholder="Opcional"
                className="mt-1"
              />
            </div>
            <div className="flex justify-between">
              <Button variant="outline" onClick={() => setStep(2)}>
                Voltar
              </Button>
              <Button
                onClick={handleConfirm}
                disabled={saving || !customerName.trim()}
              >
                {saving ? 'Criando...' : 'Confirmar Reserva'}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
```

- [ ] **Step 2: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit 2>&1 | grep -v "__tests__" | head -20
```

Expected: `0 errors`

- [ ] **Step 3: Commit**

```bash
git add frontend/components/bookings/AdminBookingModal.tsx
git commit -m "feat: add AdminBookingModal 3-step dialog component"
```

---

## Task 5: Integrar modal na página e adicionar botão No Show

**Files:**
- Modify: `frontend/app/(admin)/admin/agendamentos/page.tsx`
- Modify: `frontend/components/bookings/BookingTable.tsx`

- [ ] **Step 1: Atualizar a página de agendamentos**

Substituir o conteúdo completo de `frontend/app/(admin)/admin/agendamentos/page.tsx`:

```tsx
'use client'

import { useEffect, useState, useCallback } from 'react'
import { format, subDays } from 'date-fns'
import { bookingsApi } from '@/lib/api/bookings'
import { BookingTable } from '@/components/bookings/BookingTable'
import { AdminBookingModal } from '@/components/bookings/AdminBookingModal'
import type { Booking, BookingStatus } from '@/lib/types/booking'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Plus } from 'lucide-react'

export default function AgendamentosPage() {
  const [bookings, setBookings]   = useState<Booking[]>([])
  const [loading, setLoading]     = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [from, setFrom] = useState(format(subDays(new Date(), 7), 'yyyy-MM-dd'))
  const [to, setTo]     = useState(format(new Date(), 'yyyy-MM-dd'))
  const [status, setStatus] = useState<BookingStatus | ''>('')

  const load = useCallback(() => {
    setLoading(true)
    bookingsApi
      .list({
        from: `${from}T00:00:00`,
        to:   `${to}T23:59:59`,
        ...(status ? { status } : {}),
      })
      .then(setBookings)
      .finally(() => setLoading(false))
  }, [from, to, status])

  useEffect(() => { load() }, [load])

  const handleAction = async (
    action: 'confirm' | 'cancel' | 'complete' | 'noshow',
    id: string
  ) => {
    if (action === 'confirm')  await bookingsApi.confirm(id)
    else if (action === 'cancel')   await bookingsApi.cancel(id)
    else if (action === 'complete') await bookingsApi.complete(id)
    else if (action === 'noshow')   await bookingsApi.noShow(id)
    load()
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Agendamentos</h1>
        <Button onClick={() => setModalOpen(true)}>
          <Plus className="h-4 w-4 mr-2" /> Nova Reserva
        </Button>
      </div>

      <div className="flex gap-4 flex-wrap">
        <Input
          type="date"
          value={from}
          onChange={e => setFrom(e.target.value)}
          className="w-40"
        />
        <Input
          type="date"
          value={to}
          onChange={e => setTo(e.target.value)}
          className="w-40"
        />
        <Select
          value={status}
          onValueChange={v => setStatus(v as BookingStatus | '')}
        >
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Todos os status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Todos</SelectItem>
            <SelectItem value="Pending">Pendente</SelectItem>
            <SelectItem value="Confirmed">Confirmado</SelectItem>
            <SelectItem value="Completed">Concluído</SelectItem>
            <SelectItem value="Cancelled">Cancelado</SelectItem>
            <SelectItem value="NoShow">Não Compareceu</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {loading ? (
        <p className="text-slate-500">Carregando...</p>
      ) : (
        <BookingTable bookings={bookings} onAction={handleAction} />
      )}

      <AdminBookingModal
        open={modalOpen}
        onOpenChange={setModalOpen}
        onCreated={load}
      />
    </div>
  )
}
```

- [ ] **Step 2: Adicionar botão No Show no BookingTable**

Em `frontend/components/bookings/BookingTable.tsx`, localizar o bloco de botões dentro do `<TableCell>` de Ações e adicionar o botão "Não Compareceu" para agendamentos confirmados.

Substituir o bloco de ações (linhas 64–79):

```tsx
            <TableCell>
              <div className="flex gap-2 flex-wrap">
                {b.status === 'Pending' && (
                  <Button size="sm" onClick={() => onAction('confirm', b.id)}>
                    Confirmar
                  </Button>
                )}
                {(b.status === 'Pending' || b.status === 'Confirmed') && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => onAction('cancel', b.id)}
                  >
                    Cancelar
                  </Button>
                )}
                {b.status === 'Confirmed' && (
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => onAction('complete', b.id)}
                  >
                    Concluir
                  </Button>
                )}
                {b.status === 'Confirmed' && (
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={() => onAction('noshow', b.id)}
                  >
                    Não Compareceu
                  </Button>
                )}
              </div>
            </TableCell>
```

- [ ] **Step 3: Verificar tipos**

```bash
cd frontend && npx tsc --noEmit 2>&1 | grep -v "__tests__" | head -20
```

Expected: `0 errors`

- [ ] **Step 4: Commit**

```bash
git add frontend/app/(admin)/admin/agendamentos/page.tsx
git add frontend/components/bookings/BookingTable.tsx
git commit -m "feat: add Nova Reserva modal and No Show button to agendamentos page"
```

---

## Checklist de cobertura

- [x] Backend: novo command `AdminCreateBookingCommand` — sem dependência de `currentUser` (Task 1)
- [x] Backend: testes para resource not found, service not found, conflict, success (Task 1)
- [x] Backend: endpoint `POST /api/v1/bookings/admin` com auth TenantOwner/TenantAdmin/TenantStaff (Task 2)
- [x] Frontend: tipo `AdminCreateBookingRequest` (Task 3)
- [x] Frontend: `bookingsApi.adminCreate` (Task 3)
- [x] Frontend: `availabilityApi.getSlots` para carregar slots disponíveis por recurso+data (Task 3)
- [x] Frontend: modal 3 passos — Serviço+Recurso → Data+Horário → Cliente (Task 4)
- [x] Frontend: filtra recursos pelo serviço selecionado no passo 1 (Task 4)
- [x] Frontend: slot grid com highlight de seleção (Task 4)
- [x] Frontend: botão "Nova Reserva" na página de agendamentos (Task 5)
- [x] Frontend: filtro "Não Compareceu" no dropdown de status (Task 5)
- [x] Frontend: botão "Não Compareceu" no BookingTable para agendamentos Confirmed (Task 5)
