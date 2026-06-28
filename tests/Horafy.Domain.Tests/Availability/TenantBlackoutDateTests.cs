using FluentAssertions;
using Horafy.Domain.Entities.Availability;
using Xunit;

namespace Horafy.Domain.Tests.Availability;

public sealed class TenantBlackoutDateTests
{
    [Fact]
    public void Create_TrimsReasonAndSetsDate()
    {
        var date = new DateOnly(2026, 12, 25);

        var blackout = TenantBlackoutDate.Create(date, "  Natal  ");

        blackout.Date.Should().Be(date);
        blackout.Reason.Should().Be("Natal");
    }

    [Fact]
    public void Create_NullReason_IsAllowed()
    {
        var blackout = TenantBlackoutDate.Create(new DateOnly(2026, 1, 1));
        blackout.Reason.Should().BeNull();
    }

    [Fact]
    public void UpdateReason_ChangesReason()
    {
        var blackout = TenantBlackoutDate.Create(new DateOnly(2026, 1, 1), "Ano novo");
        blackout.UpdateReason("Reforma");
        blackout.Reason.Should().Be("Reforma");
    }
}
