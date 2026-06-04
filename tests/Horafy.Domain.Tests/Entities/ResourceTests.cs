using FluentAssertions;
using Horafy.Domain.Entities.Resources;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public class ResourceTests
{
    [Fact]
    public void Create_Professional_SetsCorrectType()
    {
        var resource = Resource.Create("João Barbeiro", ResourceType.Professional,
            email: "joao@barbearia.com", specialty: "Cabelo e Barba");

        resource.Name.Should().Be("João Barbeiro");
        resource.Type.Should().Be(ResourceType.Professional);
        resource.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_Court_SetsCorrectType()
    {
        var resource = Resource.Create("Quadra 1", ResourceType.Court);

        resource.Type.Should().Be(ResourceType.Court);
        resource.Specialty.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyName_ThrowsArgumentException()
    {
        var act = () => Resource.Create("  ", ResourceType.Professional);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_ActiveResource_SetsIsActiveFalse()
    {
        var resource = Resource.Create("Sala 1", ResourceType.PhysicalSpace);
        resource.Deactivate();
        resource.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_InactiveResource_SetsIsActiveTrue()
    {
        var resource = Resource.Create("Sala 1", ResourceType.PhysicalSpace);
        resource.Deactivate();
        resource.Activate();
        resource.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ChangesAllFields()
    {
        var resource = Resource.Create("Nome Antigo", ResourceType.Professional, email: "old@email.com");
        resource.Update("Nome Novo", "new@email.com", "11999999999", "Nova Especialidade", "Bio nova", null);

        resource.Name.Should().Be("Nome Novo");
        resource.Email.Should().Be("new@email.com");
        resource.Specialty.Should().Be("Nova Especialidade");
    }
}
