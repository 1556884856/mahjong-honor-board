using Microsoft.EntityFrameworkCore;
using System.Data;
using MahjongApi;
using MahjongApi.Data;
using MahjongApi.Models;
using MahjongApi.DTOs;

const int MaxPlayerNameLength = 50;
const int MaxGameNoteLength = 200;
const int MaxPlayersPerGame = 4;
const int MaxScore = 1_000_000;

var builder = WebApplication.CreateBuilder(args);

// ===== 鉴权配置 =====
var wechatAppId = builder.Configuration["Wechat:AppId"] ?? "";
var wechatAppSecret = builder.Configuration["Wechat:AppSecret"] ?? "";
var allowedOpenIds = builder.Configuration.GetSection("Wechat:AllowedOpenIds").Get<string[]>() ?? [];
var wechatAutoRegister = builder.Configuration.GetValue<bool?>("Wechat:AutoRegister") ?? true;
var authTokenSecret = builder.Configuration["Auth:TokenSecret"] ?? "";
var adminToken = builder.Configuration["Auth:AdminToken"] ?? "";
var tokenExpireDays = int.TryParse(builder.Configuration["Auth:TokenExpireDays"], out var d) ? d : 30;

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

// ===== 鉴权中间件：写接口（POST/PATCH/DELETE）要求有效 token =====
// GET/OPTIONS/HEAD 与登录接口放行；token 可为 adminToken 或微信登录签发的 token。
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var method = context.Request.Method;

    var needsAuth = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                    && method != "GET"
                    && method != "OPTIONS"
                    && method != "HEAD"
                    && !path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);

    if (!needsAuth)
    {
        await next();
        return;
    }

    var auth = context.Request.Headers.Authorization.ToString();
    var ok = false;
    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var token = auth.Substring("Bearer ".Length).Trim();
        if (!string.IsNullOrEmpty(adminToken) && token == adminToken)
        {
            ok = true;
        }
        else
        {
            // 白名单 = 配置文件 AllowedOpenIds + 数据库 WechatUsers 表
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            var dbIds = await db.WechatUsers.AsNoTracking().Select(w => w.OpenId).ToListAsync();
            var merged = allowedOpenIds.Concat(dbIds).Distinct().ToArray();
            if (AuthHelpers.TryVerifyToken(token, authTokenSecret, merged, out _))
            {
                ok = true;
            }
        }
    }

    if (!ok)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { message = "未授权，请先登录" });
        return;
    }

    await next();
});

// 启动时创建数据库和表
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    // 已有库不会自动补建新表，这里显式建 WechatUsers
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "WechatUsers" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_WechatUsers" PRIMARY KEY AUTOINCREMENT,
            "OpenId" TEXT NOT NULL,
            "Name" TEXT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_WechatUsers_OpenId" ON "WechatUsers" ("OpenId");
        """);

    // 一次性迁移：旧数据按 UTC 存储，统一转为上海时区 yyyy-MM-dd HH:mm:ss 字符串
    var conn = db.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open) conn.Open();
    using (var verCmd = conn.CreateCommand())
    {
        verCmd.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(verCmd.ExecuteScalar());
        if (version < 1)
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE Games SET PlayedAt = strftime('%Y-%m-%d %H:%M:%S', PlayedAt, '+8 hours') WHERE PlayedAt IS NOT NULL AND PlayedAt <> ''";
            upd.ExecuteNonQuery();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA user_version = 1";
            pragma.ExecuteNonQuery();
        }
    }

    SeedIfEmpty(db);
}

// ===== 微信登录 =====
app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("WechatLogin");
    var code = req.Code?.Trim() ?? "";
    if (string.IsNullOrEmpty(code))
        return Results.BadRequest(new { message = "code 不能为空" });

    if (string.IsNullOrEmpty(wechatAppId) || string.IsNullOrEmpty(wechatAppSecret))
        return Results.Json(new { message = "服务器未配置微信 AppId/AppSecret" }, statusCode: 500);

    var session = await AuthHelpers.Code2SessionAsync(wechatAppId, wechatAppSecret, code);
    if (session is null || session.errcode != 0 || string.IsNullOrEmpty(session.openid))
        return Results.Json(new { message = $"微信登录失败：{session?.errmsg ?? "网络错误"}" }, statusCode: 401);

    logger.LogInformation("登录请求 openid={OpenId}", session.openid);

    var dbOpenIds = await db.WechatUsers.AsNoTracking().Select(w => w.OpenId).ToListAsync();
    var mergedOpenIds = allowedOpenIds.Concat(dbOpenIds).Distinct().ToArray();
    var isAllowed = mergedOpenIds.Length == 0 || mergedOpenIds.Contains(session.openid);

    // AutoRegister=true：新微信用户首次登录自动写入白名单。
    // AutoRegister=false：仅允许 AllowedOpenIds 或数据库白名单中的用户登录。
    if (!wechatAutoRegister && !isAllowed)
    {
        logger.LogWarning("未授权登录 openid={OpenId}", session.openid);
        return Results.Json(new { message = "未授权", openid = session.openid }, statusCode: 403);
    }

    if (wechatAutoRegister && !dbOpenIds.Contains(session.openid))
    {
        db.WechatUsers.Add(new WechatUser
        {
            OpenId = session.openid,
            Name = null,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogInformation("新用户已自动入库 openid={OpenId}", session.openid);
    }

    var expiresAt = DateTime.UtcNow.AddDays(tokenExpireDays);
    var token = AuthHelpers.SignToken(session.openid, authTokenSecret, expiresAt);
    return Results.Ok(new { token, openid = session.openid, expiresAt });
});

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
    if (name.Length > MaxPlayerNameLength)
        return Results.BadRequest(new { message = $"玩家名不能超过{MaxPlayerNameLength}个字符" });

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
    if (req.Players.Count > MaxPlayersPerGame)
        return Results.BadRequest(new { message = $"最多支持{MaxPlayersPerGame}名玩家" });
    if (req.Players.Any(p => p.Score < -MaxScore || p.Score > MaxScore))
        return Results.BadRequest(new { message = "得分超出允许范围" });
    if ((req.Note?.Length ?? 0) > MaxGameNoteLength)
        return Results.BadRequest(new { message = $"备注不能超过{MaxGameNoteLength}个字符" });

    var playerIds = req.Players.Select(p => p.PlayerId).Distinct().ToList();
    if (playerIds.Count != req.Players.Count)
        return Results.BadRequest(new { message = "玩家不能重复" });

    var players = await db.Players.Where(p => playerIds.Contains(p.Id)).ToListAsync();
    if (players.Count != playerIds.Count)
        return Results.BadRequest(new { message = "存在无效的玩家" });

    var playerNameById = players.ToDictionary(p => p.Id, p => p.Name);

    var playedAtStr = (req.PlayedAt ?? "").Trim();
    if (!DateTime.TryParseExact(playedAtStr, "yyyy-MM-dd HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var playedAt))
    {
        playedAt = ChinaTime.Now;
    }

    var game = new Game
    {
        PlayedAt = playedAt.ToString("yyyy-MM-dd HH:mm:ss"),
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

    var dto = ToGameDto(game);

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

    var now = ChinaTime.Now;
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
                PlayedAt = t.ToString("yyyy-MM-dd HH:mm:ss"),
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

// 上海（中国）时区，统一时间以本地时区存储与显示
internal static class ChinaTime
{
    private static readonly TimeZoneInfo Tz = GetTz();
    private static TimeZoneInfo GetTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
    }
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);
}
