namespace MahjongApi.Models;

/// <summary>
/// 对局中的玩家得分（关联表）
/// </summary>
public class GamePlayer
{
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>该玩家本局得分</summary>
    public int Score { get; set; }
}
