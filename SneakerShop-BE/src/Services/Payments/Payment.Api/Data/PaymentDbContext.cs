using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Domain;

namespace Payment.Api.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.ExpiresAt });
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.AmountRefunded).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasMany(x => x.Attempts).WithOne(x => x.Payment)
                .HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Refunds).WithOne().HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentAttempt>(entity =>
        {
            entity.ToTable("PaymentAttempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ProviderTransactionId).HasMaxLength(100);
            entity.Property(x => x.PaymentUrl).HasMaxLength(2048);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.Provider, x.ProviderTransactionId })
                .IsUnique()
                .HasFilter("[ProviderTransactionId] IS NOT NULL");
            entity.HasIndex(x => new { x.PaymentId, x.CreatedAt });
        });

        modelBuilder.Entity<PaymentRefund>(entity =>
        {
            entity.ToTable("PaymentRefunds");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => x.PaymentId);
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
