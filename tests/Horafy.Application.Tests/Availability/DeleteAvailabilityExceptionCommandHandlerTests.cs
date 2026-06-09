using FluentAssertions;
using Horafy.Application.Features.Availability;
using Horafy.Application.Features.Availability.Commands;
using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Availability;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Availability;

public sealed class DeleteAvailabilityExceptionCommandHandlerTests
{
    private static readonly Guid ResourceId = Guid.NewGuid();
    private readonly Mock<IAvailabilityRepository> _repo = new();
    private readonly Mock<ITenantUnitOfWork> _uow = new();

    private DeleteAvailabilityExceptionCommandHandler MakeHandler() =>
        new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_ExcecaoExiste_RemoveERetornaSucesso()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var excecao = AvailabilityException.CreateBlock(ResourceId, date);
        _repo.Setup(r => r.GetExceptionAsync(ResourceId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(excecao);

        var result = await MakeHandler().Handle(
            new DeleteAvailabilityExceptionCommand(ResourceId, date), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Remove(excecao), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExcecaoNaoEncontrada_RetornaFalha()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        _repo.Setup(r => r.GetExceptionAsync(ResourceId, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AvailabilityException?)null);

        var result = await MakeHandler().Handle(
            new DeleteAvailabilityExceptionCommand(ResourceId, date), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AvailabilityErrors.ExceptionNotFound);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
