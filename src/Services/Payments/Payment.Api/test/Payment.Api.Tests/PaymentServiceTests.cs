using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Payment.Api.Data;
using Payment.Api.Domain;
using Payment.Api.Providers;
using Payment.Api.Services;
using SneakerShop.BuildingBlocks;
using SneakerShop.Contracts;
using Xunit;

namespace Payment.Api.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task Create_uses_order_amount_and_reuses_pending_attempt()
    {
        var userId = Guid.NewGuid();
        var order = ConfirmedOrder(userId);
        await using var fixture = await TestFixture.CreateAsync(order, isDevelopment: true);

        var first = await fixture.Service.CreateAsync(order.OrderId, userId, "Fake", "127.0.0.1", default);
        var second = await fixture.Service.CreateAsync(order.OrderId, userId, "fake", "127.0.0.1", default);

        Assert.Equal(125000m, first.Amount);
        Assert.Equal(PaymentStatus.Pending, first.Status);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.AttemptId, second.AttemptId);
        Assert.Equal(1, await fixture.Db.Payments.CountAsync());
        Assert.Equal(1, await fixture.Db.PaymentAttempts.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_order_that_is_not_confirmed()
    {
        var userId = Guid.NewGuid();
        var order = ConfirmedOrder(userId) with { Status = "Pending" };
        await using var fixture = await TestFixture.CreateAsync(order);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(order.OrderId, userId, "Fake", "127.0.0.1", default));

        Assert.Equal(409, exception.StatusCode);
        Assert.Empty(await fixture.Db.Payments.ToListAsync());
    }

    [Fact]
    public async Task Create_does_not_disclose_another_users_order()
    {
        var order = ConfirmedOrder(Guid.NewGuid());
        await using var fixture = await TestFixture.CreateAsync(order);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(order.OrderId, Guid.NewGuid(), "Fake", "127.0.0.1", default));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Fake_success_marks_payment_and_publishes_success_event()
    {
        var userId = Guid.NewGuid();
        var order = ConfirmedOrder(userId);
        await using var fixture = await TestFixture.CreateAsync(order, isDevelopment: true);
        var created = await fixture.Service.CreateAsync(order.OrderId, userId, "Fake", "127.0.0.1", default);

        await fixture.Service.SimulateSuccessAsync(created.AttemptId!.Value, userId, default);

        var payment = await fixture.Db.Payments.Include(x => x.Attempts).SingleAsync();
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(payment.Attempts).Status);
        Assert.True(await fixture.Harness.Published.Any<PaymentSucceeded>(x =>
            x.Context.Message.OrderId == order.OrderId &&
            x.Context.Message.PaymentId == created.Id &&
            x.Context.Message.Amount == order.Amount));
    }

    [Fact]
    public async Task Simulate_success_is_not_available_outside_development()
    {
        var userId = Guid.NewGuid();
        var order = ConfirmedOrder(userId);
        await using var fixture = await TestFixture.CreateAsync(order, isDevelopment: true);
        var created = await fixture.Service.CreateAsync(order.OrderId, userId, "Fake", "127.0.0.1", default);
        fixture.Environment.EnvironmentName = "Production";

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SimulateSuccessAsync(created.AttemptId!.Value, userId, default));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Refund_rejects_amount_above_remaining_balance()
    {
        var payment = SucceededPayment();
        await using var fixture = await TestFixture.CreateAsync(ConfirmedOrder(payment.UserId));
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RequestRefundAsync(payment.Id, Guid.NewGuid(), payment.Amount + 1, default));

        Assert.Equal(409, exception.StatusCode);
        Assert.Empty(await fixture.Db.PaymentRefunds.ToListAsync());
    }

    [Fact]
    public async Task Refund_publishes_idempotent_refund_request()
    {
        var payment = SucceededPayment();
        await using var fixture = await TestFixture.CreateAsync(ConfirmedOrder(payment.UserId));
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();
        var refundId = Guid.NewGuid();

        await fixture.Service.RequestRefundAsync(payment.Id, refundId, 25000m, default);
        await fixture.Service.RequestRefundAsync(payment.Id, refundId, 25000m, default);

        var refund = Assert.Single(await fixture.Db.PaymentRefunds.ToListAsync());
        Assert.Equal(refundId, refund.Id);
        Assert.Equal(25000m, refund.Amount);
        Assert.True(await fixture.Harness.Published.Any<RefundRequested>(x =>
            x.Context.Message.RefundId == refundId &&
            x.Context.Message.PaymentId == payment.Id &&
            x.Context.Message.Amount == 25000m));
    }

    [Fact]
    public async Task Verified_webhook_marks_payment_succeeded_and_publishes_event()
    {
        var payment = SucceededPayment();
        payment.Status = PaymentStatus.Pending;
        var attempt = Assert.Single(payment.Attempts);
        attempt.Status = PaymentStatus.Pending;
        attempt.Provider = "Fake";
        attempt.ProviderTransactionId = null;
        await using var fixture = await TestFixture.CreateAsync(ConfirmedOrder(payment.UserId));
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();
        fixture.Provider.Webhook = new VerifiedPaymentWebhook(
            attempt.Id, "transaction-1", payment.Amount, true);

        await fixture.Service.ProcessWebhookAsync("Fake", new Dictionary<string, string>
        {
            ["signed"] = "payload"
        }, default);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("transaction-1", attempt.ProviderTransactionId);
        Assert.True(await fixture.Harness.Published.Any<PaymentSucceeded>(x =>
            x.Context.Message.PaymentId == payment.Id));
    }

    [Fact]
    public async Task Webhook_amount_mismatch_is_rejected_without_changing_payment()
    {
        var payment = SucceededPayment();
        payment.Status = PaymentStatus.Pending;
        var attempt = Assert.Single(payment.Attempts);
        attempt.Status = PaymentStatus.Pending;
        attempt.Provider = "Fake";
        attempt.ProviderTransactionId = null;
        await using var fixture = await TestFixture.CreateAsync(ConfirmedOrder(payment.UserId));
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();
        fixture.Provider.Webhook = new VerifiedPaymentWebhook(
            attempt.Id, "transaction-1", payment.Amount - 1, true);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.ProcessWebhookAsync("Fake", new Dictionary<string, string>
            {
                ["signed"] = "payload"
            }, default));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    private static OrderPaymentDetails ConfirmedOrder(Guid userId) =>
        new(Guid.NewGuid(), userId, "Confirmed", 125000m, DateTime.UtcNow.AddMinutes(10));

    private static PaymentRecord SucceededPayment()
    {
        var payment = new PaymentRecord
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 125000m,
            Status = PaymentStatus.Succeeded,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        payment.Attempts.Add(new PaymentAttempt
        {
            PaymentId = payment.Id,
            Payment = payment,
            Provider = "Fake",
            Status = PaymentStatus.Succeeded,
            ProviderTransactionId = "fake-transaction",
            ExpiresAt = payment.ExpiresAt
        });
        return payment;
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider root;
        private readonly AsyncServiceScope scope;

        private TestFixture(
            ServiceProvider root,
            AsyncServiceScope scope,
            ITestHarness harness,
            TestEnvironment environment)
        {
            this.root = root;
            this.scope = scope;
            Harness = harness;
            Environment = environment;
            Provider = (StubPaymentProvider)scope.ServiceProvider.GetRequiredService<IPaymentProvider>();
            Db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            Service = new PaymentService(
                Db,
                scope.ServiceProvider.GetRequiredService<IOrderClient>(),
                scope.ServiceProvider.GetServices<IPaymentProvider>(),
                scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(),
                Environment,
                NullLogger<PaymentService>.Instance);
        }

        public PaymentDbContext Db { get; }
        public IPaymentService Service { get; }
        public ITestHarness Harness { get; }
        public TestEnvironment Environment { get; }
        public StubPaymentProvider Provider { get; }

        public static async Task<TestFixture> CreateAsync(
            OrderPaymentDetails order,
            bool isDevelopment = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<PaymentDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddSingleton<IOrderClient>(new StubOrderClient(order));
            services.AddSingleton<IPaymentProvider, StubPaymentProvider>();
            var environment = new TestEnvironment
            {
                EnvironmentName = isDevelopment ? "Development" : "Production"
            };
            services.AddSingleton<IWebHostEnvironment>(environment);
            services.AddMassTransitTestHarness();

            var root = services.BuildServiceProvider();
            var harness = root.GetRequiredService<ITestHarness>();
            await harness.Start();
            var scope = root.CreateAsyncScope();
            return new TestFixture(root, scope, harness, environment);
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await root.DisposeAsync();
        }
    }

    private sealed class StubOrderClient(OrderPaymentDetails order) : IOrderClient
    {
        public Task<OrderPaymentDetails> GetPaymentDetailsAsync(Guid orderId, CancellationToken ct) =>
            orderId == order.OrderId
                ? Task.FromResult(order)
                : throw new KeyNotFoundException();
    }

    private sealed class StubPaymentProvider : IPaymentProvider
    {
        public string Name => "Fake";
        public VerifiedPaymentWebhook? Webhook { get; set; }
        public void EnsureReady() { }

        public Task<string?> CreatePaymentAsync(
            PaymentAttempt attempt, PaymentRecord payment, string clientIp, CancellationToken ct) =>
            Task.FromResult<string?>($"https://payments.test/{attempt.Id}");

        public bool TryVerifyWebhook(
            IReadOnlyDictionary<string, string> payload, out VerifiedPaymentWebhook webhook)
        {
            webhook = Webhook!;
            return Webhook is not null;
        }

        public Task<ProviderPaymentStatus?> QueryStatusAsync(PaymentAttempt attempt, CancellationToken ct) =>
            Task.FromResult<ProviderPaymentStatus?>(null);

        public Task RefundAsync(PaymentAttempt attempt, Guid refundId, decimal amount, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Payment.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
