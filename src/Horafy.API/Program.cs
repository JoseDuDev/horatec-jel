using Horafy.Application;
using Horafy.Application.Interfaces;
using Horafy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Horafy.Infrastructure.Auth;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Horafy.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Asp.Versioning;
using Scalar.AspNetCore;
using System.Text;

// ── Serilog bootstrap (antes de qualquer coisa para capturar erros de startup)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando Horafy API...");

    var builder = WebApplication.CreateBuilder(args);

    // Desativa ValidateOnBuild para evitar que o DI tente instanciar serviços
    // (e portanto conectar ao banco) durante o build do ServiceProvider.
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = builder.Environment.IsDevelopment();
        options.ValidateOnBuild = false;
    });

    // ── Serilog configurado via appsettings
    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithMachineName()
          .Enrich.WithEnvironmentName());

    // ── Camadas da aplicação
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── API Versioning
    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"));
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

    // ── Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // ── Swagger + Scalar
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Horafy API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new()
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Insira o token JWT no formato: Bearer {token}"
        });
        options.AddSecurityRequirement(new()
        {
            {
                new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
                []
            }
        });
    });

    // ── CORS
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:3000"];

    // Cada tenant é servido em um subdomínio (cliente.agenda.mjml.com.br), então
    // o browser manda o host DO TENANT no header Origin — não o da marca. Listar
    // origem por origem é impossível: a lista cresceria a cada cliente novo.
    var platformDomains = (builder.Configuration.GetSection("Platform:Domains").Get<string[]>() ?? [])
        .Where(d => !string.IsNullOrWhiteSpace(d))
        .Select(d => d.Trim().ToLowerInvariant())
        .ToArray();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("HorafyCors", policy =>
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                        return true;

                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        return false;

                    // Subdomínio de tenant só via HTTPS: em dev, o localhost
                    // entra pela lista explícita de AllowedOrigins.
                    if (uri.Scheme != Uri.UriSchemeHttps)
                        return false;

                    var host = uri.Host.ToLowerInvariant();
                    return platformDomains.Any(d => host.EndsWith($".{d}", StringComparison.Ordinal));
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // ── JWT Authentication
    var jwtOpts = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("Configuração Jwt não encontrada.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Secret)),
                ValidateIssuer          = true,
                ValidIssuer             = jwtOpts.Issuer,
                ValidateAudience        = true,
                ValidAudience           = jwtOpts.Audience,
                ValidateLifetime        = true,
                ClockSkew               = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    // ── Rate Limiting — mínimo, escopado só ao fluxo novo de "esqueci minha senha"
    // (login/register/refresh ficam de fora; tratados separadamente pelo plano
    // 011-rate-limiting-auth.md já cadastrado no backlog do próprio repo).
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Limite por IP: 5 solicitações a cada 5 minutos.
        options.AddFixedWindowLimiter("auth-forgot-password", limiterOptions =>
        {
            limiterOptions.Window = TimeSpan.FromMinutes(5);
            limiterOptions.PermitLimit = 5;
            limiterOptions.QueueLimit = 0;
            limiterOptions.AutoReplenishment = true;
        });
    });

    // ── Health Checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgresql",
            tags: ["db", "ready"]);

    var app = builder.Build();

    // ── Middlewares (ordem importa!)
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "[{RequestMethod}] {RequestPath} → {StatusCode} em {Elapsed:0.0}ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Horafy API v1"));
        app.MapScalarApiReference();
    }

    app.UseExceptionHandlingMiddleware();
    app.UseCors("HorafyCors");
    app.UseHttpsRedirection();

    // ── Imagens enviadas pelos clientes (fotos de item e de serviço)
    // Servidas antes da autenticação: a vitrine do tenant é pública, e a foto
    // precisa carregar para quem ainda não entrou. O nome do arquivo é um GUID,
    // então nunca muda de conteúdo — daí o cache longo.
    var imageStorageOptions = builder.Configuration
        .GetSection(ImageStorageOptions.SectionName).Get<ImageStorageOptions>()
        ?? new ImageStorageOptions();

    Directory.CreateDirectory(imageStorageOptions.RootPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(imageStorageOptions.RootPath)),
        RequestPath  = imageStorageOptions.RequestPath,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
            ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
        }
    });
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseMiddleware<TenantMiddleware>();
    // Hardening: vincula o tenant_id do JWT ao tenant resolvido (impede replay entre tenants).
    app.UseMiddleware<Horafy.API.Middleware.TenantBindingMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    using (var scope = app.Services.CreateScope())
    {
        var db     = scope.ServiceProvider.GetRequiredService<HorafyDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        if (!app.Environment.IsProduction())
            await db.Database.MigrateAsync();
        await GlobalMigrations.RunAsync(db, logger);
        await TenantGlobalMigrations.RunAsync(db, logger);

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await PlatformAdminSeeder.RunAsync(db, passwordHasher, builder.Configuration, logger);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException é lançado intencionalmente pelo dotnet-ef ao descobrir
    // o DbContext durante 'migrations add/update'. Não é uma falha real — ignorar.
    Log.Fatal(ex, "Falha crítica ao iniciar a aplicação.");
}
finally
{
    Log.CloseAndFlush();
}

// Necessário para testes de integração com WebApplicationFactory
public partial class Program { }
