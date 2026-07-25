using Horafy.Application;
using Horafy.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Horafy.Infrastructure.Auth;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.Infrastructure.Persistence;
using Horafy.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    app.UseAuthentication();
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
