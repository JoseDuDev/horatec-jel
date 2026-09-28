using System.Reflection;
using FluentAssertions;
using Horafy.API.Controllers.Base;
using Horafy.Domain.Entities.Users;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Horafy.Infrastructure.Tests.Api;

/// <summary>
/// Todo papel citado num <c>[Authorize(Roles = ...)]</c> tem que existir em
/// <see cref="UserRole"/>. Um nome que não existe não dá erro de compilação nem de
/// startup: o endpoint só passa a responder 403 para todo mundo. Foi o caso do
/// <c>"Admin"</c> na carteira e nos vouchers — a tela Carteira &amp; Vouchers nunca
/// funcionou, até 28/09/2026.
/// </summary>
public sealed class AuthorizeRolesTests
{
    [Fact]
    public void EveryAuthorizeRole_IsAnExistingUserRole()
    {
        var known = Enum.GetNames<UserRole>().ToHashSet(StringComparer.Ordinal);

        var controllers = typeof(ApiControllerBase).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ApiControllerBase).IsAssignableFrom(t))
            .ToList();

        controllers.Should().NotBeEmpty();

        var unknown =
            from type in controllers
            from member in new MemberInfo[] { type }.Concat(type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            from attr in member.GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            where !string.IsNullOrWhiteSpace(attr.Roles)
            from role in attr.Roles!.Split(',', StringSplitOptions.TrimEntries)
            where !known.Contains(role)
            select $"{type.Name}.{member.Name}: \"{role}\"";

        unknown.Should().BeEmpty("papéis válidos são {0}", string.Join(", ", known));
    }
}
