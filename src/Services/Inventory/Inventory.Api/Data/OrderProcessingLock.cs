using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

public static class OrderProcessingLock
{
    public static async Task AcquireAsync(InventoryDbContext db, Guid orderId, CancellationToken ct)
    {
        var resource = $"inventory-order-{orderId:N}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DECLARE @lockResult int;
             EXEC @lockResult = sys.sp_getapplock
                 @Resource = {resource},
                 @LockMode = 'Exclusive',
                 @LockOwner = 'Transaction',
                 @LockTimeout = 10000;
             IF @lockResult < 0
                 THROW 50001, 'Could not acquire the inventory order lock.', 1;
             """,
            ct);
    }
}
