using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Payment.Api.Providers;
using Xunit;

namespace Payment.Api.Tests;

public class VnPayProviderTests
{
    private const string Secret = "test-secret";

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
            TmnCode = "TEST01",
            HashSecret = Secret
        }));

    private static Dictionary<string, string> SignedPayload(Dictionary<string, string> payload)
    {
        var query = string.Join("&", new SortedDictionary<string, string>(payload, StringComparer.Ordinal)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        payload["vnp_SecureHashType"] = "HmacSHA512";
        payload["vnp_SecureHash"] = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(query)));
        return payload;
    }
}
