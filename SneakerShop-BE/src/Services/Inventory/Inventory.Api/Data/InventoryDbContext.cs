using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace Inventory.Api.Data;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();
    public DbSet<OrderCancellation> OrderCancellations => Set<OrderCancellation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("StockItems");
            e.HasKey(s => s.VariantId);
            e.Property(s => s.VariantId).ValueGeneratedNever();
            e.Property(s => s.SneakerId).IsRequired();
            e.HasIndex(s => s.SneakerId).HasDatabaseName("IX_StockItems_SneakerId");
            e.Property(s => s.Sku).HasMaxLength(64);
            e.Property(s => s.RowVersion).IsRowVersion();
            e.Ignore(s => s.Available);
        });

        modelBuilder.Entity<StockReservation>(e =>
        {
            e.ToTable("StockReservations");
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.RowVersion).IsRowVersion();
            e.HasIndex(r => r.OrderId).HasDatabaseName("IX_StockReservations_OrderId");
            e.HasIndex(r => r.VariantId).HasDatabaseName("IX_StockReservations_VariantId");
            e.HasIndex(r => new { r.Status, r.ExpiresAt })
                .HasDatabaseName("IX_StockReservations_Status_ExpiresAt");
        });

        modelBuilder.Entity<OrderCancellation>(e =>
        {
            e.ToTable("OrderCancellations");
            e.HasKey(x => x.OrderId);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
