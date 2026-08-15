using System.ComponentModel.DataAnnotations;

namespace MahjongApi.Models;

/// <summary>
/// 已授权的小程序微信用户（openid 白名单）
/// </summary>
public class WechatUser
{
    public int Id { get; set; }

    /// <summary>微信 openid（唯一）</summary>
    [MaxLength(64)]
    public string OpenId { get; set; } = string.Empty;

    /// <summary>备注/昵称（可选）</summary>
    [MaxLength(50)]
    public string? Name { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
