using FluentAssertions;
using Horafy.Domain.Entities.Users;
using Xunit;

namespace Horafy.Domain.Tests.Users;

public sealed class UserPhoneTests
{
    private static User MakeUser() =>
        User.CreateWithGoogle("test@test.com", "google_id", "Test User", null, null, UserRole.Customer);

    [Fact]
    public void SetPhone_ValidPhone_SetsProperty()
    {
        var user = MakeUser();
        user.SetPhone("+5511999998888");
        user.Phone.Should().Be("+5511999998888");
    }

    [Fact]
    public void SetPhone_Null_ClearsProperty()
    {
        var user = MakeUser();
        user.SetPhone("+5511999998888");
        user.SetPhone(null);
        user.Phone.Should().BeNull();
    }

    [Fact]
    public void SetPhone_TooLong_Throws()
    {
        var user = MakeUser();
        var action = () => user.SetPhone(new string('1', 21));
        action.Should().Throw<ArgumentException>();
    }
}
