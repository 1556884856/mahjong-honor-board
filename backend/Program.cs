using Microsoft.EntityFrameworkCore;
using MahjongApi.Data;
using MahjongApi.Models;
using MahjongApi.DTOs;

var builder = WebApplication.CreateBuilder(args);

// SQLite 数据库
var configuredDbPath = builder.Configuration["Database:Path"] ?? "mahjong.db";
var dbPath = Path.IsPathRooted(configuredDbPath)
    ? configuredDbPath
    : Path.Combine(builder.Environment.ContentRootPath, configuredDbPath);
var connStr = $"Data Source={dbPath}";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p =>
    {
        if (builder.Environment.IsDevelopment())
        {
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseCors();

// 启动时创建数据库和表
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedIfEmpty(db);
}

// ===== 玩家 API =====
app.MapGet("/api/players", async (AppDbContext db) =>
{
    var players = await db.Players.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
    return Results.Ok(players.Select(p => new PlayerDto(p.Id, p.Name, p.CreatedAt)));
});

app.MapPost("/api/players", async (AppDbContext db, CreatePlayerRequest req) =>
{
    var name = req.Name?.Trim() ?? "";
    if (string.IsNullOrEmpty(name))
        return Results.BadRequest(new { message = "玩家名不能为空" });

    var exists = await db.Players.AnyAsync(p => p.Name == name);
    if (exists)
        return Results.Conflict(new { message = "玩家已存在" });

    var player = new Player { Name = name, CreatedAt = DateTime.UtcNow };
    db.Players.Add(player);
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
    {
        return Results.Conflict(new { message = "玩家已存在" });
    }
    return Results.Created($"/api/players/{player.Id}",
        new PlayerDto(player.Id, player.Name, player.CreatedAt));
});

app.MapDelete("/api/players/{id:int}", async (AppDbContext db, int id) =>
{
    var player = await db.Players.FindAsync(id);
    if (player is null) return Results.NotFound(new { message = "玩家不存在" });

    var hasGames = await db.GamePlayers.AnyAsync(gp => gp.PlayerId == id);
    if (hasGames)
        return Results.Conflict(new { message = "该玩家存在于对局记录中，无法删除" });

    db.Players.Remove(player);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "已删除" });
});

// ===== 对局 API =====
app.MapGet("/api/games", async (AppDbContext db) =>
{
    var games = await db.Games
        .AsNoTracking()
        .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
        .OrderByDescending(g => g.PlayedAt)
        .ToListAsync();

    return Results.Ok(games.Select(ToGameDto));
});

app.MapGet("/api/games/{id:int}", async (AppDbContext db, int id) =>
{
    var game = await db.Games
        .AsNoTracking()
        .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
        .FirstOrDefaultAsync(g => g.Id == id);

    if (game is null) return Results.NotFound(new { message = "对局不存在" });
    return Results.Ok(ToGameDto(game));
});

app.MapPost("/api/games", async (AppDbContext db, CreateGameRequest req) =>
{
    if (req.Players is null || req.Players.Count < 2)
        return Results.BadRequest(new { message = "至少需要2名玩家" });

    var playerIds = req.Players.Select(p => p.PlayerId).Distinct().ToList();
    if (playerIds.Count != req.Players.Count)
        return Results.BadRequest(new { message = "玩家不能重复" });

    var players = await db.Players.Where(p => playerIds.Contains(p.Id)).ToListAsync();
    if (players.Count != playerIds.Count)
        return Results.BadRequest(new { message = "存在无效的玩家" });

    var playerNameById = players.ToDictionary(p => p.Id, p => p.Name);
    var game = new Game
    {
        PlayedAt = req.PlayedAt ?? DateTime.UtcNow,
        Note = req.Note,
        Status = GameStatus.Active,
        Selected = true,
        CreatedAt = DateTime.UtcNow,
        GamePlayers = req.Players.Select(p => new GamePlayer
        {
            PlayerId = p.PlayerId,
            Score = p.Score
        }).ToList()
    };

    db.Games.Add(game);
    await db.SaveChangesAsync();

    var dto = new GameDto(
        game.Id,
        game.PlayedAt,
        game.Note,
        game.Status,
        game.Selected,
        game.GamePlayers.Select(gp => new GamePlayerDto(
            gp.PlayerId,
            playerNameById[gp.PlayerId],
            gp.Score)).ToList());

    return Results.Created($"/api/games/{game.Id}", dto);
});

// 作废 / 恢复
app.MapPatch("/api/games/{id:int}/status", async (AppDbContext db, int id, UpdateGameStatusRequest req) =>
{
    var game = await db.Games.FindAsync(id);
    if (game is null) return Results.NotFound(new { message = "对局不存在" });

    if (!Enum.IsDefined(typeof(GameStatus), req.Status))
        return Results.BadRequest(new { message = "无效的对局状态" });

    game.Status = req.Status;
    await db.SaveChangesAsync();
    return Results.Ok(new { id, status = game.Status });
});

// 勾选 / 取消勾选（纳入统计）
app.MapPatch("/api/games/{id:int}/selected", async (AppDbContext db, int id, UpdateGameSelectedRequest req) =>
{
    var game = await db.Games.FindAsync(id);
    if (game is null) return Results.NotFound(new { message = "对局不存在" });

    game.Selected = req.Selected;
    await db.SaveChangesAsync();
    return Results.Ok(new { id, selected = game.Selected });
});

// 永久删除
app.MapDelete("/api/games/{id:int}", async (AppDbContext db, int id) =>
{
    var game = await db.Games.FindAsync(id);
    if (game is null) return Results.NotFound(new { message = "对局不存在" });

    db.Games.Remove(game);
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "已永久删除" });
});

app.Run();

static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
    ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;

static GameDto ToGameDto(Game g) => new(
    g.Id,
    g.PlayedAt,
    g.Note,
    g.Status,
    g.Selected,
    g.GamePlayers.Select(gp => new GamePlayerDto(gp.PlayerId, gp.Player.Name, gp.Score)).ToList()
);

static void SeedIfEmpty(AppDbContext db)
{
    if (db.Players.Any() || db.Games.Any()) return;

    var names = new[] { "张三", "李四", "王五", "赵六" };
    var players = names.Select(n => new Player { Name = n, CreatedAt = DateTime.UtcNow }).ToList();
    db.Players.AddRange(players);
    db.SaveChanges();

    var now = DateTime.UtcNow;
    var day = TimeSpan.FromDays(1);
    var seedGames = new List<(DateTime t, string? note, GameStatus status, int[] scores)>
    {
        (now - day * 7, "", GameStatus.Active, new[] { 120, -40, -30, -50 }),
        (now - day * 5, "", GameStatus.Active, new[] { -50, 80, -10, -20 }),
        (now - day * 3, "王五自摸", GameStatus.Active, new[] { 60, -60, 30, -30 }),
        (now - day * 1, "记错了，作废", GameStatus.Voided, new[] { -30, 90, -20, -40 }),
        (now - day / 2, "", GameStatus.Active, new[] { 40, -40, -20, 20 }),
    };

    foreach (var (t, note, status, scores) in seedGames)
    {
        db.Games.Add(new Game
        {
            PlayedAt = t,
            Note = note,
            Status = status,
            Selected = true,
            CreatedAt = DateTime.UtcNow,
            GamePlayers = players.Select((p, i) => new GamePlayer
            {
                PlayerId = p.Id,
                Score = scores[i]
            }).ToList()
        });
    }
    db.SaveChanges();
}
