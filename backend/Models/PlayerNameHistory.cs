using System.ComponentModel.DataAnnotations;

namespace MahjongApi.Models;

/// <summary>
/// 玩家改名历史（子表）
/// </summary>
public class PlayerNameHistory
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>改前名字</summary>
    [MaxLength(50)]
    public string OldName { get; set; } = string.Empty;

    /// <summary>改后名字</summary>
    [MaxLength(50)]
    public string NewName { get; set; } = string.Empty;

    /// <summary>修改时间</summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
