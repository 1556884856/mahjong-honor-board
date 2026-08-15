using System.ComponentModel.DataAnnotations;

namespace MahjongApi.Models;

/// <summary>
/// 一场对局
/// </summary>
public class Game
{
    public int Id { get; set; }

    /// <summary>对局时间</summary>
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    /// <summary>备注</summary>
    [MaxLength(200)]
    public string? Note { get; set; }

    /// <summary>状态：Active=正常，Voided=作废</summary>
    public GameStatus Status { get; set; } = GameStatus.Active;

    /// <summary>是否纳入统计（勾选）</summary>
    public bool Selected { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>参与玩家及得分</summary>
    public List<GamePlayer> GamePlayers { get; set; } = new();
}

public enum GameStatus
{
    Active = 0,
    Voided = 1
}
