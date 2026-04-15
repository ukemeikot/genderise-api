using HngStageOne.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HngStageOne.Api.Data.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasDefaultValueSql("randomblob(16)");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.NormalizedName)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(p => p.NormalizedName)
            .IsUnique();

        builder.Property(p => p.Gender)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.GenderProbability)
            .HasPrecision(18, 4);

        builder.Property(p => p.SampleSize)
            .IsRequired();

        builder.Property(p => p.Age)
            .IsRequired();

        builder.Property(p => p.AgeGroup)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CountryId)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(p => p.CountryProbability)
            .HasPrecision(18, 4);

        builder.Property(p => p.CreatedAt)
            .IsRequired();
    }
}
