using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MahjongApi;

/// <summary>
/// 微信登录鉴权辅助：无状态 token 的签发与校验（HMACSHA256 签名，不引入第三方库）。
/// token 格式：base64url(openid).过期时间戳.签名
/// </summary>
public static class AuthHelpers
{
    public static string SignToken(string openid, string secret, DateTime expiresAt)
    {
        var exp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        var head = Base64Url(Encoding.UTF8.GetBytes(openid));
        var data = $"{head}.{exp}";
        var sig = Hmac(data, secret);
        return $"{data}.{sig}";
    }

    public static bool TryVerifyToken(string token, string secret, string[] allowedOpenIds, out string openid)
    {
        openid = "";
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var data = $"{parts[0]}.{parts[1]}";
            var expected = Hmac(data, secret);
            if (!FixedTimeEquals(expected, parts[2])) return false;

            var exp = long.Parse(parts[1]);
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;

            openid = Encoding.UTF8.GetString(FromBase64Url(parts[0]));
            if (allowedOpenIds is { Length: > 0 } && !allowedOpenIds.Contains(openid)) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Hmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Base64Url(bytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.ASCII.GetBytes(a);
        var bb = Encoding.ASCII.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        var diff = 0;
        for (var i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
        return diff == 0;
    }

    // ===== 微信 code2session =====
    public class Code2SessionResponse
    {
        public string? openid { get; set; }
        public string? session_key { get; set; }
        public string? unionid { get; set; }
        public int errcode { get; set; }
        public string? errmsg { get; set; }
    }

    public static async Task<Code2SessionResponse?> Code2SessionAsync(string appId, string appSecret, string code)
    {
        var url =
            $"https://api.weixin.qq.com/sns/jscode2session" +
            $"?appid={Uri.EscapeDataString(appId)}" +
            $"&secret={Uri.EscapeDataString(appSecret)}" +
            $"&js_code={Uri.EscapeDataString(code)}" +
            $"&grant_type=authorization_code";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        using var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Code2SessionResponse>(json);
    }
}
