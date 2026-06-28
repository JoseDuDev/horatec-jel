using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public class GetAvailabilityCalendarQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();

    // Relógio fixo ANTES do mês de teste (julho/2026) para que os slots gerados
    // sejam sempre futuros e não sejam filtrados como "passado".
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private const int TestYear  = 2026;
    private const int TestMonth = 7; // julho/2026 tem 31 dias

    private static AvailabilityRule RuleFor(DayOfWeek day) =>
        AvailabilityRule.Create(ResourceId, day, new TimeOnly(8, 0), new TimeOnly(12, 0), 60);

    [Fact]
    public async Task Handle_RuleOnMondaysOnly_OnlyMondaysHaveSlots()
    {
        var rules = new List<AvailabilityRule> { RuleFor(DayOfWeek.Monday) };
        var (handler, _) = BuildHandler(rules);

        var result = await handler.Handle(
            new GetAvailabilityCalendarQuery(ResourceId, TestYear, TestMonth), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(DateTime.DaysInMonth(TestYear, TestMonth));
        result.Value.Where(d => d.Date.DayOfWeek == DayOfWeek.Monday)
            .Should().OnlyContain(d => d.AvailableSlotCount > 0);
        result.Value.Where(d => d.Date.DayOfWeek != DayOfWeek.Monday)
            .Should().OnlyContain(d => d.AvailableSlotCount == 0);
    }

    [Fact]
    public async Task Handle_BlackoutDate_HasZeroSlotsDespiteRule()
    {
        // Regra em todos os dias da semana → isola o efeito do blackout.
        var rules = Enum.GetValues<DayOfWeek>().Select(RuleFor).ToList();
        var blackoutDate = new DateOnly(TestYear, TestMonth, 10);
        var blackouts = new List<TenantBlackoutDate> { TenantBlackoutDate.Create(blackoutDate) };
        var (handler, _) = BuildHandler(rules, blackouts: blackouts);

        var result = await handler.Handle(
            new GetAvailabilityCalendarQuery(ResourceId, TestYear, TestMonth), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Single(d => d.Date == blackoutDate).AvailableSlotCount.Should().Be(0);
        // Um outro dia (sem blackout) ainda tem slots, provando que a regra está ativa.
        result.Value.Single(d => d.Date == new DateOnly(TestYear, TestMonth, 11))
            .AvailableSlotCount.Should().BeGreaterThan(0);
    }

    private static (GetAvailabilityCalendarQueryHandler handler, Mock<IBookingRepository> bookingRepo)
        BuildHandler(
            List<AvailabilityRule> rules,
            List<AvailabilityException>? exceptions = null,
            List<TenantBlackoutDate>? blackouts = null)
    {
        var availRepo   = new Mock<IAvailabilityRepository>();
        var serviceRepo = new Mock<IServiceRepository>();
        var bookingRepo = new Mock<IBookingRepository>();

        availRepo.Setup(r => r.GetRulesByResourceAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);
        availRepo.Setup(r => r.GetExceptionsByResourceAsync(
                ResourceId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exceptions ?? new List<AvailabilityException>());
        availRepo.Setup(r => r.GetBlackoutDatesAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blackouts ?? new List<TenantBlackoutDate>());
        bookingRepo.Setup(r => r.GetByResourceAsync(
                ResourceId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());

        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(c => c.UtcNow).Returns(FixedNow);

        var handler = new GetAvailabilityCalendarQueryHandler(
            availRepo.Object, serviceRepo.Object, bookingRepo.Object, clock.Object);
        return (handler, bookingRepo);
    }
}
