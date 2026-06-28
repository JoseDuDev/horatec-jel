using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Entities.Resources;
using Horafy.Domain.Entities.Services;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public class GetAvailableSlotsQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private static readonly DateOnly TestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    private static AvailabilityRule BuildRule(
        TimeOnly start, TimeOnly end, int slotMinutes, int breakMinutes = 0) =>
        AvailabilityRule.Create(ResourceId, TestDate.DayOfWeek, start, end, slotMinutes, breakMinutes);

    [Fact]
    public async Task Handle_NoRule_ReturnsEmptyList()
    {
        var (handler, availRepo, _, _) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync((AvailabilityRule?)null);

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_BlockedDay_ReturnsEmptyList()
    {
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(12, 0), 60);
        var exception = AvailabilityException.CreateBlock(ResourceId, TestDate);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync(exception);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_GlobalBlackoutDate_ReturnsEmptyList()
    {
        // Há regra e nenhuma exceção do recurso, mas a data é bloqueio global do tenant.
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(12, 0), 60);
        var (handler, availRepo, _, _) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.IsBlackoutAsync(TestDate, default))
            .ReturnsAsync(true);

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoExceptionNoBookings_ReturnsAllSlots()
    {
        // 08:00–10:00, slot 60min, sem break → espera 2 slots: 08:00 e 09:00
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(10, 0), 60);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].TimeOfDay.Should().Be(new TimeSpan(8, 0, 0));
        result.Value[1].TimeOfDay.Should().Be(new TimeSpan(9, 0, 0));
    }

    [Fact]
    public async Task Handle_WithBreak_ReturnsCorrectSlots()
    {
        // 08:00–10:30, slot 60min, break 15min → slots: 08:00, 09:15
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(10, 30), 60, breakMinutes: 15);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].TimeOfDay.Should().Be(new TimeSpan(8, 0, 0));
        result.Value[1].TimeOfDay.Should().Be(new TimeSpan(9, 15, 0));
    }

    [Fact]
    public async Task Handle_SlotOccupiedByBooking_ExcludesSlot()
    {
        // 08:00–10:00, slot 60min, booking às 08:00 → apenas 09:00 disponível
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(10, 0), 60);
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        var slotStart = new DateTimeOffset(TestDate.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc));
        var existingBooking = Booking.Create(
            Guid.NewGuid(), ResourceId, Guid.NewGuid(),
            "Cliente", "cliente@test.com",
            scheduledAt: slotStart,
            durationMinutes: 60);

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking> { existingBooking });

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].TimeOfDay.Should().Be(new TimeSpan(9, 0, 0));
    }

    [Fact]
    public async Task Handle_TodayPastSlots_AreExcluded()
    {
        // Relógio fixo ao meio-dia → determinístico (não depende da hora real do CI).
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        // Regra cobrindo o dia inteiro → slots da manhã (antes do "agora") devem sair.
        var rule = AvailabilityRule.Create(
            ResourceId, today.DayOfWeek, new TimeOnly(0, 0), new TimeOnly(23, 0), 30);
        var (handler, availRepo, _, bookingRepo) = BuildHandler(now);

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, today.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, today, default))
            .ReturnsAsync((AvailabilityException?)null);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, today, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();                       // há slots à tarde
        result.Value.Should().OnlyContain(s => s > now);          // nenhum slot vencido
        result.Value.Should().NotContain(s => s.TimeOfDay < new TimeSpan(12, 0, 0)); // manhã excluída
    }

    [Fact]
    public async Task Handle_CustomHoursException_UsesExceptionTimes()
    {
        // Regra: 08:00–12:00, mas exceção define 10:00–12:00 → 2 slots de 60min
        var rule = BuildRule(new TimeOnly(8, 0), new TimeOnly(12, 0), 60);
        var exception = AvailabilityException.CreateCustomHours(
            ResourceId, TestDate, new TimeOnly(10, 0), new TimeOnly(12, 0));
        var (handler, availRepo, _, bookingRepo) = BuildHandler();

        availRepo.Setup(r => r.GetRuleAsync(ResourceId, TestDate.DayOfWeek, default))
            .ReturnsAsync(rule);
        availRepo.Setup(r => r.GetExceptionAsync(ResourceId, TestDate, default))
            .ReturnsAsync(exception);
        bookingRepo.Setup(r => r.GetByResourceAsync(ResourceId, It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<Booking>());

        var result = await handler.Handle(
            new GetAvailableSlotsQuery(ResourceId, TestDate, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].TimeOfDay.Should().Be(new TimeSpan(10, 0, 0));
    }

    private static (GetAvailableSlotsQueryHandler handler,
        Mock<IAvailabilityRepository> availRepo,
        Mock<IServiceRepository> serviceRepo,
        Mock<IBookingRepository> bookingRepo) BuildHandler(DateTimeOffset? now = null)
    {
        var availRepo   = new Mock<IAvailabilityRepository>();
        var serviceRepo = new Mock<IServiceRepository>();
        var bookingRepo = new Mock<IBookingRepository>();

        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(now ?? DateTimeOffset.UtcNow);

        var handler = new GetAvailableSlotsQueryHandler(
            availRepo.Object, serviceRepo.Object, bookingRepo.Object, clock.Object);
        return (handler, availRepo, serviceRepo, bookingRepo);
    }
}
