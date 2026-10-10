using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ordering.Api.Sagas;

public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(
        EntityTypeBuilder<OrderState> entity,
        ModelBuilder model)
    {
        entity.ToTable("OrderState");
        entity.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        entity.Property(x => x.ReservationDeadline).IsRequired();
        entity.Property(x => x.PaymentAccepted).IsRequired();
        entity.HasIndex(x => new { x.CurrentState, x.ReservationDeadline })
            .HasDatabaseName("IX_OrderState_CurrentState_ReservationDeadline");
    }
}
