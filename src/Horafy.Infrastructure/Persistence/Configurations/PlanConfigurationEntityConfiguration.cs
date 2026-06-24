using Horafy.Domain.Entities.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Horafy.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração da tabela plan_configurations (schema public) — limites editáveis por plano.
/// </summary>
public sealed class PlanConfigurationEntityConfiguration : IEntityTypeConfiguration<PlanConfiguration>
{
    public void Configure(EntityTypeBuilder<PlanConfiguration> builder)
    {
        builder.ToTable("plan_configurations", "public");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Plan)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(p => p.Plan)
            .IsUnique()
            .HasDatabaseName("ix_plan_configurations_plan");

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
    }
}
