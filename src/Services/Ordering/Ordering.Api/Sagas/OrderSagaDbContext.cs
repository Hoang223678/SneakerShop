using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Ordering.Api.Sagas;

public class OrderSagaDbContext(DbContextOptions<OrderSagaDbContext> options) : SagaDbContext(options)
{
    protected override IEnumerable<ISagaClassMap> Configurations =>
        new ISagaClassMap[] { new OrderStateMap() };
}
