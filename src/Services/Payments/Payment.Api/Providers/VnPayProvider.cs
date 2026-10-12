using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Payment.Api.Domain;

namespace Payment.Api.Providers;

public sealed class VnPayProvider(HttpClient http, IOptions<VnPayOptions> options) : IPaymentProvider
{
    private readonly VnPayOptions settings = options.Value;
    public string Name => "VnPay";

    public Task<string?> CreatePaymentAsync(PaymentAttempt attempt, PaymentRecord payment, string clientIp, CancellationToken ct)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = ToMinorUnits(payment.Amount),
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = ToVnPayDate(DateTime.UtcNow),
            ["vnp_CurrCode"] = payment.Currency,
            ["vnp_ExpireDate"] = ToVnPayDate(payment.ExpiresAt),
            ["vnp_IpAddr"] = NormalizeIp(clientIp),
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"Payment for order {payment.OrderId:N}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = Required(settings.ReturnUrl, nameof(settings.ReturnUrl)),
            ["vnp_TmnCode"] = Required(settings.TmnCode, nameof(settings.TmnCode)),
            ["vnp_TxnRef"] = attempt.Id.ToString("N"),
            ["vnp_Version"] = "2.1.0"
        };
        values["vnp_SecureHash"] = Sign(values);
        var queryValues = values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        return Task.FromResult<string?>(QueryHelpers.AddQueryString(
            Required(settings.PaymentUrl, nameof(settings.PaymentUrl)), queryValues));
    }

    public bool TryVerifyWebhook(IReadOnlyDictionary<string, string> payload, out VerifiedPaymentWebhook webhook)
    {
        webhook = default!;
        var hashType = payload.GetValueOrDefault("vnp_SecureHashType");
        if (!payload.TryGetValue("vnp_SecureHash", out var suppliedHash) ||
            !Guid.TryParseExact(payload.GetValueOrDefault("vnp_TxnRef"), "N", out var attemptId) ||
            string.IsNullOrWhiteSpace(payload.GetValueOrDefault("vnp_TransactionNo")) ||
            payload["vnp_TransactionNo"].Length > 100 ||
            payload.GetValueOrDefault("vnp_TmnCode") != settings.TmnCode ||
            payload.GetValueOrDefault("vnp_CurrCode") != "VND" ||
            (hashType is not null && hashType != "HmacSHA512") ||
            !long.TryParse(payload.GetValueOrDefault("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var minorUnits))
            return false;

        var unsigned = new SortedDictionary<string, string>(payload
            .Where(pair => pair.Key.StartsWith("vnp_", StringComparison.Ordinal) &&
                           pair.Key is not ("vnp_SecureHash" or "vnp_SecureHashType"))
            .ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal);
        if (!IsValidSignature(unsigned, suppliedHash))
            return false;

        var responseCode = payload.GetValueOrDefault("vnp_ResponseCode");
        var transactionStatus = payload.GetValueOrDefault("vnp_TransactionStatus");
        var succeeded = responseCode == "00" && transactionStatus == "00";
        if (responseCode == "00" && transactionStatus is not ("00" or "02" or "04" or "06" or "07" or "09"))
            return false;
        webhook = new VerifiedPaymentWebhook(
            attemptId,
            payload["vnp_TransactionNo"],
            minorUnits / 100m,
            succeeded);
        return true;
    }

    public async Task<ProviderPaymentStatus?> QueryStatusAsync(PaymentAttempt attempt, CancellationToken ct)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Command"] = "querydr",
            ["vnp_CreateDate"] = ToVnPayDate(DateTime.UtcNow),
            ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_OrderInfo"] = $"Query payment {attempt.PaymentId:N}",
            ["vnp_RequestId"] = Guid.NewGuid().ToString("N"),
            ["vnp_TmnCode"] = Required(settings.TmnCode, nameof(settings.TmnCode)),
            ["vnp_TransactionDate"] = ToVnPayDate(attempt.CreatedAt),
            ["vnp_TxnRef"] = attempt.Id.ToString("N"),
            ["vnp_Version"] = "2.1.0"
        };
        values["vnp_SecureHash"] = Sign(values);
        using var response = await http.PostAsJsonAsync(
            Required(settings.QueryUrl, nameof(settings.QueryUrl)), values, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("VNPay returned an empty query response.");
        if (body.GetValueOrDefault("vnp_ResponseCode") != "00" ||
            body.GetValueOrDefault("vnp_TxnRef") != attempt.Id.ToString("N") ||
            body.GetValueOrDefault("vnp_TmnCode") != settings.TmnCode ||
            body.GetValueOrDefault("vnp_CurrCode") != "VND" ||
            !long.TryParse(body.GetValueOrDefault("vnp_Amount"), NumberStyles.None,
                CultureInfo.InvariantCulture, out var minorUnits) ||
            string.IsNullOrWhiteSpace(body.GetValueOrDefault("vnp_TransactionNo")))
            return null;
        var succeeded = body.GetValueOrDefault("vnp_TransactionStatus") switch
        {
            "00" => true,
            "02" or "04" or "06" or "07" or "09" => false,
            _ => (bool?)null
        };
        return succeeded is null
            ? null
            : new ProviderPaymentStatus(body["vnp_TransactionNo"], minorUnits / 100m, succeeded.Value);
    }

    public async Task RefundAsync(PaymentAttempt attempt, Guid refundId, decimal amount, CancellationToken ct)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = ToMinorUnits(amount),
            ["vnp_Command"] = "refund",
            ["vnp_CreateDate"] = ToVnPayDate(DateTime.UtcNow),
            ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_OrderInfo"] = $"Refund payment {attempt.PaymentId:N}",
            ["vnp_RequestId"] = refundId.ToString("N"),
            ["vnp_TmnCode"] = Required(settings.TmnCode, nameof(settings.TmnCode)),
            ["vnp_TransactionDate"] = ToVnPayDate(attempt.CreatedAt),
            ["vnp_TransactionNo"] = attempt.ProviderTransactionId ?? "",
            ["vnp_TransactionType"] = "02",
            ["vnp_TxnRef"] = attempt.Id.ToString("N"),
            ["vnp_Version"] = "2.1.0"
        };
        values["vnp_SecureHash"] = Sign(values);
        using var response = await http.PostAsJsonAsync(Required(settings.RefundUrl, nameof(settings.RefundUrl)), values, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("VNPay returned an empty refund response.");
        if (body.GetValueOrDefault("vnp_ResponseCode") != "00")
            throw new InvalidOperationException("VNPay rejected the refund request.");
    }

    private string Sign(IEnumerable<KeyValuePair<string, string>> values)
    {
        var query = string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        var key = Encoding.UTF8.GetBytes(Required(settings.HashSecret, nameof(settings.HashSecret)));
        return Convert.ToHexString(HMACSHA512.HashData(key, Encoding.UTF8.GetBytes(query)));
    }

    private bool IsValidSignature(IEnumerable<KeyValuePair<string, string>> values, string supplied)
    {
        if (supplied.Length != 128) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(Sign(values)),
                Convert.FromHexString(supplied));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

    private static string NormalizeIp(string ip) =>
        string.IsNullOrWhiteSpace(ip) || ip == "::1" ? "127.0.0.1" : ip;

    private static string ToVnPayDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"))
            .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string Required(string? value, string setting) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"Payments:VnPay:{setting} is not configured.");
}

public sealed class VnPayOptions
{
    public string? PaymentUrl { get; init; }
    public string? QueryUrl { get; init; }
    public string? RefundUrl { get; init; }
    public string? ReturnUrl { get; init; }
    public string? TmnCode { get; init; }
    public string? HashSecret { get; init; }
}
