using HngStageOne.Api.Data.Configurations;
using HngStageOne.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HngStageOne.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ProfileConfiguration());
    }
}
