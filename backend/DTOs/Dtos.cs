using MahjongApi.Models;

namespace MahjongApi.DTOs;

// ===== 玩家 =====
public record PlayerDto(int Id, string Name, DateTime CreatedAt);

public record CreatePlayerRequest(string Name);

// ===== 对局 =====
public record GamePlayerInput(int PlayerId, int Score);

public record CreateGameRequest(
    DateTime? PlayedAt,
    string? Note,
    List<GamePlayerInput> Players);

public record GamePlayerDto(int PlayerId, string PlayerName, int Score);

public record GameDto(
    int Id,
    DateTime PlayedAt,
    string? Note,
    GameStatus Status,
    bool Selected,
    List<GamePlayerDto> Players);

public record UpdateGameStatusRequest(GameStatus Status);

public record UpdateGameSelectedRequest(bool Selected);
