using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Payment.Api.Domain;
using Payment.Api.Providers;
using Xunit;

namespace Payment.Api.Tests;

public class VnPayProviderTests
{
    private const string Secret = "test-secret";

    [Fact]
    public async Task CreatePayment_builds_signed_sandbox_url()
    {
        var attempt = new PaymentAttempt { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddMinutes(15) };
        var payment = new PaymentRecord
        {
            OrderId = Guid.NewGuid(),
            Amount = 129.99m,
            Currency = "VND",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        var provider = CreateProvider();

        var url = await provider.CreatePaymentAsync(attempt, payment, "127.0.0.1", default);

        Assert.StartsWith("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?", url);
        Assert.Contains($"vnp_TxnRef={attempt.Id:N}", url);
        Assert.Contains("vnp_TmnCode=TEST01", url);
        Assert.Contains("vnp_OrderInfo=Thanh+toan+don+hang+", url);
        Assert.Contains("vnp_SecureHash=", url);
        var hash = url.Split("vnp_SecureHash=", 2)[1];
        Assert.Equal(128, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
    }

    [Fact]
    public void Verifies_signed_success_and_parses_authoritative_attempt_and_amount()
    {
        var attemptId = Guid.NewGuid();
        var payload = SignedPayload(new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = attemptId.ToString("N"),
            ["vnp_TransactionNo"] = "123456",
            ["vnp_Amount"] = "1234500",
            ["vnp_TmnCode"] = "TEST01",
            ["vnp_CurrCode"] = "VND",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        });
        var provider = CreateProvider();

        var valid = provider.TryVerifyWebhook(payload, out var result);

        Assert.True(valid);
        Assert.Equal(attemptId, result.AttemptId);
        Assert.Equal("123456", result.TransactionId);
        Assert.Equal(12345m, result.Amount);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rejects_invalid_signature()
    {
        var payload = SignedPayload(new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = Guid.NewGuid().ToString("N"),
            ["vnp_TransactionNo"] = "123456",
            ["vnp_Amount"] = "1234500",
            ["vnp_TmnCode"] = "TEST01",
            ["vnp_CurrCode"] = "VND",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        });
        var provider = CreateProvider();
        payload["vnp_SecureHash"] = new string('0', 128);

        Assert.False(provider.TryVerifyWebhook(payload, out _));
    }

    [Fact]
    public void Verifies_sandbox_return_payload_without_currency_field()
    {
        var attemptId = Guid.Parse("c505e564-b399-4697-8c0f-9aef68baf0b4");
        var payload = new Dictionary<string, string>
        {
            ["vnp_Amount"] = "1099900000",
            ["vnp_BankCode"] = "NCB",
            ["vnp_BankTranNo"] = "VNP15707070",
            ["vnp_CardType"] = "ATM",
            ["vnp_OrderInfo"] = "Thanh toan don hang 71c1d490703c4c64aa233abfed27d5c0",
            ["vnp_PayDate"] = "20261017025755",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TmnCode"] = "VZYCD771",
            ["vnp_TransactionNo"] = "15707070",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = "c505e564b39946978c0f9aef68baf0b4",
            ["vnp_SecureHash"] = "a0b5637c32656799899d6a79ffa54f502e91db8794f02c5ae4e6f1e8aaac9de7bcf319a2f5bd2b78b096dc8d1d43b913fd531c3300e810c57c936e62de7f9406"
        };
        var provider = new VnPayProvider(new HttpClient(), Options.Create(new VnPayOptions
        {
            TmnCode = "VZYCD771",
            HashSecret = "CXGWXWMVHKMMUJPSGOPYESTPADJUZMLI"
        }));

        var valid = provider.TryVerifyWebhook(payload, out var result);

        Assert.True(valid);
        Assert.Equal(attemptId, result.AttemptId);
        Assert.True(result.Succeeded);
        Assert.Equal(10_999_000m, result.Amount);
    }

    [Fact]
    public void Rejects_correctly_signed_payload_for_wrong_currency()
    {
        var payload = SignedPayload(new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = Guid.NewGuid().ToString("N"),
            ["vnp_TransactionNo"] = "123456",
            ["vnp_Amount"] = "1234500",
            ["vnp_TmnCode"] = "TEST01",
            ["vnp_CurrCode"] = "USD",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        });
        var provider = CreateProvider();

        Assert.False(provider.TryVerifyWebhook(payload, out _));
    }

    private static VnPayProvider CreateProvider() =>
        new(new HttpClient(), Options.Create(new VnPayOptions
        {
            PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ReturnUrl = "http://localhost:5000/api/payments/vnpay/return",
            TmnCode = "TEST01",
            HashSecret = Secret
        }));

    private static Dictionary<string, string> SignedPayload(Dictionary<string, string> payload)
    {
        var query = string.Join("&", new SortedDictionary<string, string>(payload, StringComparer.Ordinal)
            .Select(pair => $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));
        payload["vnp_SecureHashType"] = "HmacSHA512";
        payload["vnp_SecureHash"] = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(query)))
            .ToLowerInvariant();
        return payload;
    }
}
