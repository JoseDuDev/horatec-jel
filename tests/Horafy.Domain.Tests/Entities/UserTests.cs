using FluentAssertions;
using Horafy.Domain.Entities.Users;
using Horafy.Domain.Events.Users;
using Xunit;

namespace Horafy.Domain.Tests.Entities;

public class UserTests
{
    // ── CreateWithEmail ───────────────────────────────────────────────
    [Fact]
    public void CreateWithEmail_ValidData_ReturnsUser()
    {
        var user = User.CreateWithEmail("JOSE@GMAIL.COM", "hash123", "José", null, UserRole.Customer);

        user.Email.Should().Be("jose@gmail.com"); // normaliza lowercase
        user.Name.Should().Be("José");
        user.Role.Should().Be(UserRole.Customer);
        user.IsEmailVerified.Should().BeFalse();
        user.PasswordHash.Should().Be("hash123");
    }

    [Fact]
    public void CreateWithEmail_RaisesUserCreatedEvent()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserCreatedEvent>();
    }

    [Fact]
    public void CreateWithEmail_AssignsDefaultCustomerPermissions()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);

        user.Permissions.Should().Contain(UserPermission.CreateBooking);
        user.Permissions.Should().Contain(UserPermission.CancelBooking);
        user.Permissions.Should().Contain(UserPermission.ViewBookings);
        user.Permissions.Should().NotContain(UserPermission.ManageTenant);
    }

    // ── CreateWithGoogle ──────────────────────────────────────────────
    [Fact]
    public void CreateWithGoogle_SetsEmailVerifiedTrue()
    {
        var user = User.CreateWithGoogle(
            "jose@gmail.com", "google-123", "José", null, null, UserRole.PlatformAdmin);

        user.IsEmailVerified.Should().BeTrue();
        user.GoogleId.Should().Be("google-123");
    }

    // ── CreateWithApple ───────────────────────────────────────────────
    [Fact]
    public void CreateWithApple_SetsEmailVerifiedTrue()
    {
        var user = User.CreateWithApple(
            "jose@icloud.com", "apple-456", "José", null, UserRole.Customer);

        user.IsEmailVerified.Should().BeTrue();
        user.AppleId.Should().Be("apple-456");
    }

    // ── TenantOwner permissions ───────────────────────────────────────
    [Fact]
    public void CreateWithEmail_TenantOwnerRole_HasAllManagePermissions()
    {
        var tenantId = Guid.NewGuid();
        var user = User.CreateWithEmail("owner@tenant.com", "hash", "Owner", tenantId, UserRole.TenantOwner);

        user.Permissions.Should().Contain(UserPermission.ManageTenant);
        user.Permissions.Should().Contain(UserPermission.ManageStaff);
        user.Permissions.Should().Contain(UserPermission.ManageBilling);
        user.Permissions.Should().Contain(UserPermission.ExportReports);
    }

    // ── GrantPermission ───────────────────────────────────────────────
    [Fact]
    public void GrantPermission_NewPermission_IsAddedToCollection()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.TenantStaff);
        user.GrantPermission(UserPermission.ViewReports);

        user.HasPermission(UserPermission.ViewReports).Should().BeTrue();
    }

    [Fact]
    public void GrantPermission_DuplicatePermission_IsNotAddedTwice()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.TenantStaff);
        user.GrantPermission(UserPermission.ViewReports);
        user.GrantPermission(UserPermission.ViewReports);

        user.Permissions.Count(p => p == UserPermission.ViewReports).Should().Be(1);
    }

    // ── RevokePermission ──────────────────────────────────────────────
    [Fact]
    public void RevokePermission_ExistingPermission_IsRemoved()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        user.RevokePermission(UserPermission.CreateBooking);

        user.HasPermission(UserPermission.CreateBooking).Should().BeFalse();
    }

    // ── LinkGoogle ────────────────────────────────────────────────────
    [Fact]
    public void LinkGoogle_SetsGoogleId()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        user.LinkGoogle("new-google-id");

        user.GoogleId.Should().Be("new-google-id");
    }

    // ── RecordLogin ───────────────────────────────────────────────────
    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        var user = User.CreateWithEmail("jose@gmail.com", "hash", "José", null, UserRole.Customer);
        user.RecordLogin();

        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── PlatformAdmin ─────────────────────────────────────────────────
    [Fact]
    public void PlatformAdmin_HasAllPermissions()
    {
        var user = User.CreateWithEmail("admin@horafy.com", "hash", "Admin", null, UserRole.PlatformAdmin);
        var allPermissions = Enum.GetValues<UserPermission>();

        foreach (var perm in allPermissions)
            user.HasPermission(perm).Should().BeTrue(because: $"PlatformAdmin deve ter {perm}");
    }
}
