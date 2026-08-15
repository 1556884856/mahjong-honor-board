# 🀄 麻将荣誉榜

个人纪录小队的麻将积分统计系统。支持多玩家管理、对局记录、作废/恢复/永久删除、多维度统计筛选。

## 技术栈

| 层面 | 技术 |
|------|------|
| 前端 | Vue 3 + Vite |
| 后端 | .NET 10 (ASP.NET Core Minimal API) |
| 数据库 | SQLite (EF Core) |

## 项目结构

```
麻将荣誉榜/
├── backend/                  # 后端 .NET 10 Web API
│   ├── Program.cs            # API 端点 + 启动初始化
│   ├── Models/               # 数据模型 (Player, Game, GamePlayer)
│   ├── Data/AppDbContext.cs  # EF Core 上下文
│   ├── DTOs/Dtos.cs          # 请求/响应对象
│   ├── MahjongApi.csproj     # 项目文件 (net10.0)
│   └── mahjong.db            # SQLite 数据库（运行时自动生成）
└── frontend/                 # 前端 Vue 3
    ├── src/
    │   ├── App.vue           # 主应用 + 标签页导航
    │   ├── api.js            # API 请求封装
    │   ├── components/
    │   │   ├── RecordTab.vue # 玩家管理 + 新建对局
    │   │   ├── HistoryTab.vue# 历史记录（作废/恢复/永久删除）
    │   │   └── StatsTab.vue  # 统计分析（多维度筛选 + 实时更新）
    │   └── style.css         # 全局样式
    ├── vite.config.js        # 开发代理配置
    └── package.json
```

## 运行

### 后端

```bash
cd backend
dotnet run
# 默认监听 http://localhost:5080
```

数据库文件 `mahjong.db` 会自动创建在 backend 目录下。首次运行会自动写入示例数据。

### 前端（开发模式）

```bash
cd frontend
npm install
npm run dev
# 访问 http://localhost:5173
```

开发模式下前端 `/api` 请求会代理到后端 `http://localhost:5080`。

### 前端（生产构建）

```bash
cd frontend
npm run build
# 产物在 frontend/dist
```

## API 一览

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | /api/players | 玩家列表 |
| POST | /api/players | 新建玩家 |
| DELETE | /api/players/{id} | 删除玩家（有对局记录则拒绝） |
| GET | /api/games | 对局列表（含玩家得分） |
| GET | /api/games/{id} | 单场对局详情 |
| POST | /api/games | 新建对局 |
| PATCH | /api/games/{id}/status | 作废/恢复（status: 0正常 1作废） |
| PATCH | /api/games/{id}/selected | 勾选/取消勾选纳入统计 |
| DELETE | /api/games/{id} | 永久删除 |

## 数据模型

- **Player**: Id, Name(唯一), CreatedAt
- **Game**: Id, PlayedAt, Note, Status(Active/Voided), Selected, CreatedAt
- **GamePlayer**: GameId, PlayerId, Score

## 部署说明

1. 后端发布：`cd backend && dotnet publish -c Release -o publish`
2. 前端构建：`cd frontend && npm run build`，将 `dist` 目录交给任意静态服务器
3. 反向代理（如 Nginx）将 `/api` 转发到后端，其余转发到前端静态资源
