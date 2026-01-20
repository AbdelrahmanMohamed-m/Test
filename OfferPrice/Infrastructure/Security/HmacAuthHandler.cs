using System.Security.Cryptography;
using System.Text;

namespace OfferPrice.Infrastructure.Security;

public class HmacAuthenticationHandler(string appId, string apiKey) : DelegatingHandler
{
    private readonly string _appId = appId ?? throw new ArgumentNullException(nameof(appId));
    private readonly string _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authHeader = await GenerateHmacAuthHeaderAsync(request);

        request.Headers.Remove("TRPS-Auth");
        request.Headers.Add("TRPS-Auth", authHeader);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GenerateHmacAuthHeaderAsync(HttpRequestMessage request)
    {
        var method = request.Method.Method.ToUpperInvariant();
        var encodedRequestUri = GetEncodedRequestUri(request.RequestUri);
        var timestamp = GetUnixTimestamp();
        var nonce = GenerateNonce();
        var bodyHash = await ComputeBodyHashAsync(request);

        var rawString = BuildRawString(method, encodedRequestUri, timestamp, nonce, bodyHash);
        var signature = ComputeHmacSignature(rawString);

        return $"trpsamx {_appId}:{signature}:{nonce}:{timestamp}";
    }


    private static string GetEncodedRequestUri(Uri uri)
    {
        var protocol = uri.Scheme;
        var host = uri.Host;
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        var pathAndQuery = uri.AbsolutePath + uri.Query;

        var fullUri = $"{protocol}://{host}{port}{pathAndQuery}";
        return Uri.EscapeDataString(fullUri).ToLowerInvariant();
    }

    private static string GetUnixTimestamp() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

    private static string GenerateNonce()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }

    private static async Task<string> ComputeBodyHashAsync(HttpRequestMessage request)
    {
        if (request.Content == null)
            return string.Empty;

        string body = await request.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(body))
            return string.Empty;

        using var sha512 = SHA512.Create();
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var hashBytes = sha512.ComputeHash(bodyBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private string BuildRawString(string method, string encodedUri, string timestamp, string nonce, string bodyHash) =>
        _appId + method + encodedUri + timestamp + nonce + bodyHash;

    private string ComputeHmacSignature(string raw)
    {
        var keyBytes = Convert.FromBase64String(_apiKey);
        using var hmac = new HMACSHA256(keyBytes);
        var rawBytes = Encoding.UTF8.GetBytes(raw);
        var signatureBytes = hmac.ComputeHash(rawBytes);
        return Convert.ToBase64String(signatureBytes);
    }

}
