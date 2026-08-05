using FluentAssertions;
using Horafy.Application.Features.Leads.Commands;
using Horafy.Domain.Entities.Leads;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Horafy.Application.Tests.Leads;

public sealed class CreateLeadCommandHandlerTests
{
    private readonly Mock<IPlatformLeadRepository> _repo = new();
    private readonly Mock<IUnitOfWork>             _uow  = new();

    private CreateLeadCommandHandler MakeHandler() => new(_repo.Object, _uow.Object);

    private static CreateLeadCommand MakeCommand() => new(
        "Maria da Silva", "(11) 98888-7777", "maria@exemplo.com",
        "Barbearia", "Quero saber mais", "agenda");

    [Fact]
    public async Task Handle_NewPhone_PersistsLeadAndSaves()
    {
        _repo.Setup(r => r.RecentPhoneExistsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(false);

        var result = await MakeHandler().Handle(MakeCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _repo.Verify(r => r.Add(It.Is<PlatformLead>(l =>
            l.Name == "Maria da Silva" &&
            l.Phone == "(11) 98888-7777" &&
            l.Brand == "agenda")), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_RecentDuplicatePhone_ReturnsSuccessWithoutInserting()
    {
        _repo.Setup(r => r.RecentPhoneExistsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(true);

        var result = await MakeHandler().Handle(MakeCommand(), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Add(It.IsAny<PlatformLead>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Theory]
    [InlineData("", "(11) 98888-7777", "agenda", false)]      // nome vazio
    [InlineData("Maria", "", "agenda", false)]                // telefone vazio
    [InlineData("Maria", "9 8888", "agenda", false)]          // menos de 10 dígitos
    [InlineData("Maria", "abc11988887777", "agenda", false)]  // letras no telefone
    [InlineData("Maria", "(11) 98888-7777", "", false)]       // marca vazia
    [InlineData("Maria", "(11) 98888-7777", "agenda", true)]
    public void Validator_EnforcesRequiredFields(string name, string phone, string brand, bool expectedValid)
    {
        var command = new CreateLeadCommand(name, phone, null, null, null, brand);

        var result = new CreateLeadCommandValidator().Validate(command);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Validator_InvalidEmail_Fails()
    {
        var command = new CreateLeadCommand(
            "Maria", "(11) 98888-7777", "nao-e-email", null, null, "agenda");

        new CreateLeadCommandValidator().Validate(command).IsValid.Should().BeFalse();
    }
}
