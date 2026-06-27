using Horafy.Application.Interfaces;
using Horafy.Domain.Interfaces;
using Horafy.Domain.Interfaces.Repositories;
using Horafy.Infrastructure.Auth;
using Horafy.Infrastructure.Email;
using Horafy.Infrastructure.Gateways;
using Horafy.Infrastructure.Messaging;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Persistence.Interceptors;
using Horafy.Infrastructure.Repositories;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quartz;

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
        services.AddSingleton<IDateTimeProvider, Time.SystemDateTimeProvider>();
        services.AddScoped<ICurrentTenantService, TenantService>();
        services.AddScoped<ITenantPlanService, TenantPlanService>();
        services.AddScoped<IPlanLimitsService, PlanLimitsService>();
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
        services.AddScoped<IPlanConfigurationRepository, PlanConfigurationRepository>();
        services.AddScoped<IIntegrationApiKeyRepository, IntegrationApiKeyRepository>();
        services.AddScoped<IIntegrationWebhookRepository, IntegrationWebhookRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Webhooks de saída (write-back) — cliente HTTP de entrega.
        // O dispatcher (tipo interno do Application) é registrado em AddApplication.
        services.AddHttpClient("integration-webhook", c => c.Timeout = TimeSpan.FromSeconds(10));

        // Repositórios de tenant (tenant_{slug} schema)
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IFavoriteServiceRepository, FavoriteServiceRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IRentableItemRepository, RentableItemRepository>();
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

        services.Configure<MercadoPagoOptions>(configuration.GetSection(MercadoPagoOptions.SectionName));
        if (Environment.GetEnvironmentVariable("PAYMENT_GATEWAY") == "fake")
        {
            services.AddScoped<IPaymentGateway, FakePaymentGateway>();
        }
        else
        {
            services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
            {
                client.BaseAddress = new Uri("https://api.mercadopago.com");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddHttpMessageHandler(sp =>
            {
                var token = configuration["MercadoPago:AccessToken"] ?? string.Empty;
                return new BearerTokenHandler(token);
            });
        }

        // Evolution API — WhatsApp
        services.Configure<EvolutionApiOptions>(
            configuration.GetSection(EvolutionApiOptions.SectionName));
        services.AddHttpClient<IWhatsAppService, EvolutionApiWhatsAppService>(client =>
        {
            var baseUrl = configuration[$"{EvolutionApiOptions.SectionName}:BaseUrl"];
            if (!string.IsNullOrEmpty(baseUrl))
                client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("apikey",
                configuration[$"{EvolutionApiOptions.SectionName}:ApiKey"] ?? string.Empty);
        });

        // SMTP e-mail
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        // MassTransit + RabbitMQ
        var rabbitOpts = configuration.GetSection(RabbitMqOptions.SectionName)
                             .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumers(typeof(DependencyInjection).Assembly);

            x.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();

                var jobKey = new JobKey("booking-reminder");
                q.AddJob<Horafy.Infrastructure.Messaging.Jobs.BookingReminderJob>(
                    opts => opts.WithIdentity(jobKey));
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("booking-reminder-trigger")
                    .WithCronSchedule("0 0 * * * ?"));
            });
            x.AddQuartzConsumers();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitOpts.Host, rabbitOpts.Port, rabbitOpts.VirtualHost, h =>
                {
                    h.Username(rabbitOpts.Username);
                    h.Password(rabbitOpts.Password);
                });

                cfg.UseMessageRetry(r => r.Exponential(3,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(125),
                    TimeSpan.FromSeconds(5)));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddHostedService<OutboxProcessorService>();

        return services;
    }
}
