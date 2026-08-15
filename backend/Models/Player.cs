using System.ComponentModel.DataAnnotations;

namespace MahjongApi.Models;

/// <summary>
/// 玩家
/// </summary>
public class Player
{
    public int Id { get; set; }

    /// <summary>玩家名字（唯一）</summary>
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
