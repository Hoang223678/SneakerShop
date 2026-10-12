using Microsoft.EntityFrameworkCore;
using MassTransit;
using Ordering.Api.Domain;

namespace Ordering.Api.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.IsPaid).IsRequired();
            e.Property(o => o.PaymentId);
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.Property(o => o.PaymentExpiresAt).IsRequired();
            e.Property(o => o.ShippingName).HasMaxLength(150).IsRequired();
            e.Property(o => o.ShippingPhone).HasMaxLength(30).IsRequired();
            e.Property(o => o.ShippingAddress).HasMaxLength(500).IsRequired();
            e.Property(o => o.FailureReason).HasMaxLength(500);
            e.Property(o => o.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.Property(o => o.RowVersion).IsRowVersion();
            e.HasIndex(o => o.UserId).HasDatabaseName("IX_Orders_UserId");
            e.HasIndex(o => o.Status).HasDatabaseName("IX_Orders_Status");
            e.HasIndex(o => new { o.UserId, o.IdempotencyKey }).IsUnique()
                .HasDatabaseName("UQ_Orders_UserId_IdempotencyKey");
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.Property(i => i.SneakerName).HasMaxLength(200).IsRequired();
            e.Property(i => i.Sku).HasMaxLength(64).IsRequired();
            e.Property(i => i.Size).HasPrecision(4, 1);
            e.Property(i => i.Color).HasMaxLength(50).IsRequired();
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
