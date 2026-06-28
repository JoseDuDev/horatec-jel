using FluentAssertions;
using Horafy.Application.Features.Availability.Queries;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Entities.Bookings;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public class GetAvailableDaysQueryHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();

    // Relógio fixo ANTES do intervalo de teste (julho/2026) → slots sempre futuros.
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly From = new(2026, 7, 1);
    private static readonly DateOnly To   = new(2026, 7, 28); // 27 dias (≤ 31)

    private static AvailabilityRule RuleFor(DayOfWeek day) =>
        AvailabilityRule.Create(ResourceId, day, new TimeOnly(8, 0), new TimeOnly(12, 0), 60);

    [Fact]
    public async Task Handle_RuleOnSpecificWeekdays_ReturnsOnlyThoseWeekdays()
    {
        var rules = new List<AvailabilityRule>
        {
            RuleFor(DayOfWeek.Tuesday),
            RuleFor(DayOfWeek.Thursday),
        };
        var (handler, _) = BuildHandler(rules);

        var result = await handler.Handle(
            new GetAvailableDaysQuery(ResourceId, From, To, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().OnlyContain(d =>
            d.DayOfWeek == DayOfWeek.Tuesday || d.DayOfWeek == DayOfWeek.Thursday);
        // Todos os terças/quintas do intervalo devem estar presentes.
        var expected = EnumerateRange()
            .Where(d => d.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday)
            .ToList();
        result.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Handle_BlockedExceptionDay_IsExcluded()
    {
        // Regra todos os dias; uma exceção bloqueia um dia específico → excluído.
        var rules = Enum.GetValues<DayOfWeek>().Select(RuleFor).ToList();
        var blockedDate = new DateOnly(2026, 7, 15);
        var exceptions = new List<AvailabilityException>
        {
            AvailabilityException.CreateBlock(ResourceId, blockedDate),
        };
        var (handler, _) = BuildHandler(rules, exceptions: exceptions);

        var result = await handler.Handle(
            new GetAvailableDaysQuery(ResourceId, From, To, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain(blockedDate);
        // Os demais dias do intervalo (com regra) permanecem disponíveis.
        result.Value.Should().Contain(new DateOnly(2026, 7, 16));
    }

    private static IEnumerable<DateOnly> EnumerateRange()
    {
        for (var d = From; d <= To; d = d.AddDays(1))
            yield return d;
    }

    private static (GetAvailableDaysQueryHandler handler, Mock<IBookingRepository> bookingRepo)
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

        var handler = new GetAvailableDaysQueryHandler(
            availRepo.Object, serviceRepo.Object, bookingRepo.Object, clock.Object);
        return (handler, bookingRepo);
    }
}
