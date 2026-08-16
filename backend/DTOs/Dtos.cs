using MahjongApi.Models;

namespace MahjongApi.DTOs;

// ===== 玩家 =====
public record PlayerDto(int Id, string Name, DateTime CreatedAt);

public record CreatePlayerRequest(string Name);

public record UpdatePlayerRequest(string Name);

public record PlayerNameHistoryDto(int Id, string OldName, string NewName, string ChangedAt);

// ===== 对局 =====
public record GamePlayerInput(int PlayerId, int Score);

public record CreateGameRequest(
    string? PlayedAt,
    string? Note,
    List<GamePlayerInput> Players);

public record GamePlayerDto(int PlayerId, string PlayerName, int Score);

public record GameDto(
    int Id,
    string PlayedAt,
    string? Note,
    GameStatus Status,
    bool Selected,
    List<GamePlayerDto> Players);

public record UpdateGameStatusRequest(GameStatus Status);

public record UpdateGameSelectedRequest(bool Selected);

// ===== 鉴权 =====
public record LoginRequest(string Code);
