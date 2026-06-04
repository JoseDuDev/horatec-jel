using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Auth;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using Horafy.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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

        // Multi-tenancy — TenantDbContext com search_path dinâmico por tenant
        services.AddScoped<ICurrentTenantService, TenantService>();
        services.AddScoped<TenantMigrationService>();
        services.AddScoped<ITenantSchemaService, TenantSchemaService>();

        // TenantDbContext: connection string com Search Path = tenant_{slug},public
        services.AddScoped<TenantDbContext>(sp =>
        {
            var tenantSvc   = sp.GetRequiredService<ICurrentTenantService>();
            var searchPath  = tenantSvc.SchemaName is { } s ? $"{s},public" : "public";
            var tenantConn  = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = searchPath
            }.ConnectionString;

            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(tenantConn, npgsql =>
                {
                    npgsql.SetPostgresVersion(16, 0);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null);
                })
                .UseSnakeCaseNamingConvention()
                .Options;

            return new TenantDbContext(options, sp.GetService<MediatR.IPublisher>());
        });

        // Repositórios globais (public schema)
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositórios de tenant (tenant_{slug} schema)
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();

        // Auth — JWT, OAuth, hashing
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IAppleOAuthService, AppleOAuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // IHttpContextAccessor (necessário para CurrentUserService)
        services.AddHttpContextAccessor();

        // HttpClient para buscar JWKS da Apple
        services.AddHttpClient("apple-jwks", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
