using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Inventory.Api.Data;
using Inventory.Api.Domain;
using Inventory.Api.Consumers;
using Ordering.Api.Data;
using Ordering.Api.Domain;
using Ordering.Api.Sagas;
using Ordering.Api.Services;
using SneakerShop.Contracts;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Ordering.Api.Tests;

public class OrderSagaIntegrationTests
{
    [Fact]
    public async Task Sql_server_outboxes_and_rabbitmq_complete_order_and_stock_reservation()
    {
        await using var sqlServer = new MsSqlBuilder()
            .WithPassword("Sneaker_Test123!")
            .Build();
        await using var rabbitMq = new RabbitMqBuilder()
            .WithUsername("sneakertest")
            .WithPassword("Sneaker_Test123!")
            .Build();

        await Task.WhenAll(sqlServer.StartAsync(), rabbitMq.StartAsync());

        var connectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "OrderingIntegration"
        }.ConnectionString;
        var inventoryConnectionString = new SqlConnectionStringBuilder(sqlServer.GetConnectionString())
        {
            InitialCatalog = "InventoryIntegration"
        }.ConnectionString;

        using var host = BuildHost(connectionString, inventoryConnectionString, rabbitMq);
        var provider = host.Services;
        var orderId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        while (FaultCaptureConsumer<OrderCreated>.Errors.TryDequeue(out _)) { }
        while (FaultCaptureConsumer<StockReserved>.Errors.TryDequeue(out _)) { }
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var orders = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var sagaFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrderSagaDbContext>>();
                var inventoryFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InventoryDbContext>>();
                await using var sagas = await sagaFactory.CreateDbContextAsync();
                await using var inventory = await inventoryFactory.CreateDbContextAsync();
                await orders.Database.MigrateAsync();
                await sagas.Database.MigrateAsync();
                await inventory.Database.MigrateAsync();

                var userId = Guid.NewGuid();
                orders.Orders.Add(new Order
                {
                    Id = orderId,
                    UserId = userId,
                    IdempotencyKey = Guid.NewGuid().ToString("N"),
                    ShippingName = "Integration",
                    ShippingPhone = "0900000000",
                    ShippingAddress = "Test address"
                });
                inventory.StockItems.Add(new StockItem
                {
                    VariantId = variantId,
                    Sku = "TEST-1",
                    Quantity = 5,
                    Reserved = 0
                });

                await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()
                    .Publish(new OrderCreated(orderId, userId, new[] { new OrderLine(variantId, 2) }));
                await orders.SaveChangesAsync();
                await inventory.SaveChangesAsync();
            }

            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT_BIG(*) FROM OutboxMessage WHERE MessageType LIKE '%OrderCreated%'";
                Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) > 0,
                    "OrderCreated was not persisted to the EF bus outbox with the order.");
            }

            await host.StartAsync();
            try
            {
                await WaitForAsync(async () =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    var orders = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                    return await orders.Orders.AnyAsync(x =>
                        x.Id == orderId && x.Status == OrderStatus.Confirmed);
                });
            }
            catch (TimeoutException exception)
            {
                var errors = FaultCaptureConsumer<OrderCreated>.Errors
                    .Concat(FaultCaptureConsumer<StockReserved>.Errors);
                throw new InvalidOperationException($"The order did not confirm. {string.Join(" | ", errors)}", exception);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var inventoryFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InventoryDbContext>>();
                await using var inventory = await inventoryFactory.CreateDbContextAsync();
                var reservation = await inventory.Reservations.SingleAsync(x => x.OrderId == orderId);
                var stock = await inventory.StockItems.SingleAsync(x => x.VariantId == variantId);
                Assert.Equal(ReservationStatus.Reserved, reservation.Status);
                Assert.Equal(2, stock.Reserved);
                Assert.InRange(reservation.ExpiresAt - reservation.CreatedAt,
                    TimeSpan.FromMinutes(14.9), TimeSpan.FromMinutes(15.1));
            }

            await provider.GetRequiredService<IBus>()
                .Publish(new OrderPaymentReceived(orderId));
            await WaitForAsync(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var inventoryFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InventoryDbContext>>();
                await using var inventory = await inventoryFactory.CreateDbContextAsync();
                var orders = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                return await orders.Orders.AnyAsync(x => x.Id == orderId && x.IsPaid) &&
                       await inventory.Reservations.AnyAsync(x =>
                           x.OrderId == orderId && x.Status == ReservationStatus.Paid);
            });

            await using (var scope = provider.CreateAsyncScope())
            {
                var orders = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var order = await orders.Orders.SingleAsync(x => x.Id == orderId);
                Assert.True(order.TryCancel());
                await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()
                    .Publish(new OrderCancelled(orderId));
                await orders.SaveChangesAsync();
            }

            await WaitForAsync(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var inventoryFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InventoryDbContext>>();
                await using var inventory = await inventoryFactory.CreateDbContextAsync();
                return await inventory.Reservations.AnyAsync(x =>
                    x.OrderId == orderId && x.Status == ReservationStatus.Released);
            });

            var timedOutOrderId = Guid.NewGuid();
            await using (var scope = provider.CreateAsyncScope())
            {
                var orders = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var sagaFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OrderSagaDbContext>>();
                await using var sagas = await sagaFactory.CreateDbContextAsync();
                orders.Orders.Add(new Order
                {
                    Id = timedOutOrderId,
                    UserId = Guid.NewGuid(),
                    IdempotencyKey = Guid.NewGuid().ToString("N"),
                    ShippingName = "Timed out",
                    ShippingPhone = "0900000000",
                    ShippingAddress = "Test address"
                });
                sagas.Set<OrderState>().Add(new OrderState
                {
                    CorrelationId = timedOutOrderId,
                    CurrentState = nameof(OrderStateMachine.AwaitingReservation),
                    ReservationDeadline = DateTime.UtcNow.AddMinutes(-1)
                });
                await orders.SaveChangesAsync();
                await sagas.SaveChangesAsync();
            }

            await WaitForAsync(async () =>
            {
                await using var scope = provider.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var inventoryFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<InventoryDbContext>>();
                await using var inventory = await inventoryFactory.CreateDbContextAsync();
                return await orders.Orders.AnyAsync(x =>
                           x.Id == timedOutOrderId && x.Status == OrderStatus.Cancelled) &&
                       await inventory.OrderCancellations.AnyAsync(x => x.OrderId == timedOutOrderId);
            });
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException("The order saga did not reach the expected state.");
    }

    private static IHost BuildHost(
        string connectionString,
        string inventoryConnectionString,
        RabbitMqContainer rabbitMq)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContextFactory<OrderDbContext>(options =>
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.MigrationsHistoryTable("__EFMigrationsHistory_Ordering");
                        sql.EnableRetryOnFailure();
                    }));
                services.AddDbContextFactory<OrderSagaDbContext>(options =>
                    options.UseSqlServer(connectionString, sql =>
                    {
                        sql.MigrationsHistoryTable("__EFMigrationsHistory_OrderSaga");
                        sql.EnableRetryOnFailure();
                    }));
                services.AddDbContextFactory<InventoryDbContext>(options =>
                    options.UseSqlServer(inventoryConnectionString, sql => sql.EnableRetryOnFailure()));
                services.AddHostedService<OrderSagaTimeoutService>();
                services.AddMassTransit(x =>
                {
                    x.SetKebabCaseEndpointNameFormatter();
                    x.AddEntityFrameworkOutbox<OrderDbContext>(outbox =>
                    {
                        outbox.UseSqlServer();
                        outbox.UseBusOutbox();
                    });
                    x.AddEntityFrameworkOutbox<InventoryDbContext>(outbox =>
                    {
                        outbox.UseSqlServer();
                    });
                    x.AddConsumer<OrderCreatedConsumer>();
                    x.AddConsumer<OrderPaidConsumer>();
                    x.AddConsumer<OrderCancelledConsumer>();
                    x.AddConsumer<FaultCaptureConsumer<OrderCreated>>();
                    x.AddConsumer<FaultCaptureConsumer<StockReserved>>();
                    x.AddConfigureEndpointsCallback((context, name, endpoint) =>
                    {
                        if (name is "order-created" or "order-paid" or "order-cancelled")
                            endpoint.UseEntityFrameworkOutbox<InventoryDbContext>(context);
                        else
                            endpoint.UseEntityFrameworkOutbox<OrderDbContext>(context);
                    });
                    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
                        .EntityFrameworkRepository(repository =>
                        {
                            repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
                            repository.AddDbContext<DbContext, OrderSagaDbContext>((_, options) =>
                                options.UseSqlServer(connectionString, sql =>
                                {
                                    sql.MigrationsHistoryTable("__EFMigrationsHistory_OrderSaga");
                                    sql.EnableRetryOnFailure();
                                }));
                        });
                    x.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.UseMessageRetry(retry => retry.Immediate(1));
                        cfg.Host(rabbitMq.Hostname, (ushort)rabbitMq.GetMappedPublicPort(5672), "/", host =>
                        {
                            host.Username("sneakertest");
                            host.Password("Sneaker_Test123!");
                        });
                        cfg.ConfigureEndpoints(context);
                    });
                });
            })
            .Build();
    }
}
