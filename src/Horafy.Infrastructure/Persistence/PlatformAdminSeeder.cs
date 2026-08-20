using Horafy.Application.Interfaces;
using Horafy.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Persistence;

/// <summary>
/// Bootstrap do primeiro PlatformAdmin (superadmin) — hoje não existe nenhum
/// caminho que crie um usuário com esse role e senha utilizável em
/// <c>/platform/login</c>. Idempotente: só cria se ainda não existir NENHUM
/// PlatformAdmin. Lê <c>PLATFORM_ADMIN_EMAIL</c>/<c>PLATFORM_ADMIN_PASSWORD</c>
/// de configuração/env var; se qualquer um estiver ausente, apenas loga um
/// aviso e segue (não bloqueia o boot da aplicação).
/// </summary>
public static class PlatformAdminSeeder
{
    public static async Task RunAsync(
        HorafyDbContext db,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.PlatformAdmin, ct))
            return;

        var email    = configuration["PLATFORM_ADMIN_EMAIL"];
        var password = configuration["PLATFORM_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Nenhum PlatformAdmin encontrado e PLATFORM_ADMIN_EMAIL/PLATFORM_ADMIN_PASSWORD " +
                "não configurados — pulando bootstrap do superadmin.");
            return;
        }

        var admin = User.CreateWithEmail(
            email, passwordHasher.Hash(password), "Admin", tenantId: null, UserRole.PlatformAdmin);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("PlatformAdmin bootstrap criado para {Email}.", email);
    }
}
