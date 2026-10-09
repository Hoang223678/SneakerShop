using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Sneaker> Sneakers => Set<Sneaker>();
    public DbSet<SneakerVariant> Variants => Set<SneakerVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>(e =>
        {
            e.ToTable("Brands");
            e.Property(b => b.Name).HasMaxLength(100).IsRequired();
            e.Property(b => b.Slug).HasMaxLength(120).IsRequired();
            e.HasIndex(b => b.Name).IsUnique().HasDatabaseName("UQ_Brands_Name");
        });

        modelBuilder.Entity<Sneaker>(e =>
        {
            e.ToTable("Sneakers");
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(s => s.Slug).IsUnique().HasDatabaseName("UQ_Sneakers_Slug");
            e.Property(s => s.Description).HasMaxLength(2000);
            e.Property(s => s.Gender).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Price).HasPrecision(18, 2);
            e.Property(s => s.ImageUrl).HasMaxLength(500);
            e.HasIndex(s => s.BrandId).HasDatabaseName("IX_Sneakers_BrandId");
            e.HasOne(s => s.Brand).WithMany(b => b.Sneakers).HasForeignKey(s => s.BrandId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SneakerVariant>(e =>
        {
            e.ToTable("SneakerVariants");
            e.Property(v => v.Sku).HasMaxLength(64).IsRequired();
            e.HasIndex(v => v.Sku).IsUnique().HasDatabaseName("UQ_SneakerVariants_Sku");
            e.Property(v => v.Size).HasPrecision(4, 1);
            e.Property(v => v.Color).HasMaxLength(50).IsRequired();
            e.HasIndex(v => new { v.SneakerId, v.Size, v.Color }).IsUnique().HasDatabaseName("UQ_SneakerVariants_Sneaker_Size_Color");
            e.HasOne(v => v.Sneaker).WithMany(s => s.Variants).HasForeignKey(v => v.SneakerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
