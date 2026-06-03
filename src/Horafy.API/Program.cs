using Horafy.Application;
using Horafy.Infrastructure;
using Horafy.Infrastructure.MultiTenancy;
using Horafy.API.Middleware;
using Serilog;
using Asp.Versioning;
using Scalar.AspNetCore;

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
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("HorafyCors", policy =>
        {
            policy
                .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
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
    app.UseAuthentication();
    app.UseMiddleware<TenantMiddleware>();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

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
