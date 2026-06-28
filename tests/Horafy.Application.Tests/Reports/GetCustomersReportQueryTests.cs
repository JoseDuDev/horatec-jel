using FluentAssertions;
using Horafy.Application.Features.Reports.Queries;
using Horafy.Application.Interfaces;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Reports;

public sealed class GetCustomersReportQueryTests
{
    [Fact]
    public async Task Handle_ReturnsCustomersFromReader()
    {
        var reader = new Mock<ICustomerListReader>();
        var record = new CustomerExportRecord(
            Guid.NewGuid(), "Ana", "ana@test.com", "1199999",
            BookingCount: 3, LastBookingAt: DateTimeOffset.UtcNow, TotalSpent: 150m);

        reader.Setup(r => r.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerExportRecord> { record });

        var handler = new GetCustomersReportQueryHandler(reader.Object);
        var result  = await handler.Handle(new GetCustomersReportQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Name.Should().Be("Ana");
        result.Value[0].TotalSpent.Should().Be(150m);
    }
}
