using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using Horafy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Horafy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        // Interceptors do EF Core
        // AuditableEntityInterceptor é Scoped (consome ICurrentTenantService que é Scoped)
        // OutboxInterceptor é Scoped também para evitar captive-dependency com a factory
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<OutboxInterceptor>();

        // Registra o DbContext via AddDbContextFactory com lifetime Scoped.
        // Motivo: AuditableEntityInterceptor é Scoped; uma factory Singleton não pode
        // consumir serviços Scoped. Com ServiceLifetime.Scoped, a factory é criada por
        // escopo de requisição e pode resolver os interceptors corretamente.
        // No EF Core 6+, AddDbContextFactory também registra HorafyDbContext para
        // injeção direta (como Scoped), então não é necessário chamar AddDbContext.
        services.AddDbContextFactory<HorafyDbContext>((sp, options) =>
        {
            var auditInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            var outboxInterceptor = sp.GetRequiredService<OutboxInterceptor>();

            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.SetPostgresVersion(16, 0);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "public");
                })
                .UseSnakeCaseNamingConvention()   // converte PascalCase → snake_case em todas as colunas/tabelas
                .AddInterceptors(auditInterceptor, outboxInterceptor);
        }, ServiceLifetime.Scoped);

        // Cache em memória para resolução de tenant
        services.AddMemoryCache();

        // Multi-tenancy
        services.AddScoped<ICurrentTenantService, TenantService>();
        services.AddScoped<TenantMigrationService>();

        // Repositórios
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
