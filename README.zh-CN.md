# OpenClaw Farm

> **一个 Skill 文件，一条 WebSocket，你的 Agent 替你 7×24 打理农场。**

[English](README.md) · **简体中文**

---

## 为 AI Agent 而生的农场游戏——不是事后硬贴上去的

大多数「AI 玩游戏」演示，不过是聊天机器人叠在截图工具或脆弱的桌面宏上。**OpenClaw Farm 走另一条路。**

它是一款可亲眼看、可长期挂机的像素农场，更是**从第一天就为 Agent 自动化设计**的试验场：无需插件、无需 MCP、无需图像识别。复制 [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md)，连接 `ws://127.0.0.1:28080`，循环就开始了——种植、挖矿、钓鱼、砍树、建造、出售、维护。

浏览器负责**观战**；移动、交互、出售等操作必须通过 **HTTP / WebSocket 动作** 下发（或在聊天里指挥 OpenClaw Agent 代发）。

---

## 两个玩家，同一个世界

| 你（观战 / 指挥） | Agent（自动执行） |
|-------------------|-------------------|
| 浏览器 Canvas 看小人走动、动画、顶栏状态 | `GET /agent/state/*` 读完整世界 |
| 聊天里说「去砍树」「交付跨线订单」 | `POST /agent/action` 或 WS 发动作 |
| 顶栏看 💰 金币、三线效率、维护耐久 | `GET /agent/state/action` 查忙碌与下一步建议 |

同一张地图，同一套规则。Agent 动作**串行执行**——上一个未完成时会返回「请等待」，避免指令打架。

---

## 游戏内容一览

### 三线挂机

| 线路 | 内容 |
|------|------|
| 🌾 **种植** | 24 块地、5 种作物、温室 / 自动洒水 / 自动收割、加工工坊 |
| ⛏️ **挖矿** | 三层矿洞、体力 / 镐耐久 / 灯笼、L3 Boss |
| 🎣 **钓鱼** | 多片鱼塘、鱼饵、鱼干加工、观赏鱼图鉴 |

三类商人分别收作物、矿石、鱼类；**每日 / 7 日 / 庆典跨线订单**强制三线协作。

### 树林与建造

- 西侧树林砍树 → `crop_wood`（需 `tool_axe`）
- 锯木厂解锁后加工 `crop_plank`
- 解锁 **伐木营地**（+25% 木材）、**锯木厂**（木板配方）
- `build_tile` 铺设木栅栏、木路、木平台（消耗木材 / 木板）

### 强制消耗与维护

顶栏 **🌾⛏️🎣** 为三线**产出倍率**（长期只跑一条线会从 100% 衰减至约 20%）；**🔧矿 / 🏠** 为矿道与建筑耐久，每游戏日下降，需 `reinforce_mine`、`repair_buildings`、`feed_pond` 等维护动作。

另有畜牧饲料、温室供暖、建筑维护、背包自然损耗等日常消耗——Designed for 长期挂机，不是一键满分。

### 元系统

成就、通关进度、转生、杂交种子、土地羁绊、装饰放置、存档读写（`POST /api/game/new` · `/api/game/load` · `/api/game/save`）。

---

## Agent 为什么偏爱这套方案

### 零部署成本
单个 Skill 内嵌 WS/HTTP 客户端、忙碌等待、三线循环模板。不用重启网关，不用维护插件。

### 消息体积极小
纯 JSON：`type`、`reqId`、`payload`。读写分离——**HTTP 观察，WS/HTTP 行动**。

### 完成即提示下一步
动作成功后 `message` 含 `→ 下一步：…`，`extra` 带 `nextHint` / `nextActionId`，方便 Agent 自动接续。

### 本地回环
仅绑定 `127.0.0.1`。动作冷却 + 出售二次确认，适合长跑 Cron / TaskFlow。

---

## 60 秒上手

```bash
dotnet run --project src/OpenClawFarm.Server
```

1. 打开 **http://127.0.0.1:28080**
2. **开始游戏** 或 **继续游戏**（必须先开档，否则 `no_session`）
3. 将 [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md) 放入 OpenClaw skills，然后说：

```
运行一轮三线循环：农事 → 缺木则砍树 → 挖矿 → 钓鱼 → 维护 → 出售。
```

### Windows 单文件发布

```powershell
.\publish.ps1
# 输出 dist\OpenClawFarm\OpenClawFarm.exe — 双击运行，托盘图标右键退出
```

---

## Agent 接口速查

| 用途 | 地址 |
|------|------|
| 健康检查 | `GET /health` |
| **推荐全量状态** | `GET /agent/state/meta` |
| 忙碌 + 下一步建议 | `GET /agent/state/action` |
| 树林 / 建造 | `GET /agent/state/forest` · `/agent/state/construction` |
| 执行动作 | `POST /agent/action` `{ "actionId", "params" }` |
| WebSocket | `ws://127.0.0.1:28080/ws/agent` |

开档：`POST /api/game/new` · `POST /api/game/load`

常用动作：`move_to` · `interact` · `chop_tree` · `build_tile` · `process` · `mine_*` · `fish` · `sell_item` · `unlock_building` · `deliver_cross_order` · `reinforce_mine` · `repair_buildings`

完整列表见 [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md) 与 [`docs/PROTOCOL.md`](docs/PROTOCOL.md)。

---

## Visual Studio 2022

> **注意：** 必须启动 **`OpenClawFarm.Server`**（Core 是类库，不能直接 F5）。

1. 打开 **`OpenClawFarm.sln`**
2. 右键 **`OpenClawFarm.Server`** → **设为启动项目**
3. **F5** → 浏览器打开 http://127.0.0.1:28080
4. 在 `GameWorld.cs` 等处设断点调试

需要 **.NET 8 SDK**。

---

## 仓库结构

| 路径 | 说明 |
|------|------|
| `src/OpenClawFarm.Core/` | 游戏逻辑：地块、三线、树林、建造、经济、维护、存档 |
| `src/OpenClawFarm.Server/` | ASP.NET Core 服务 + Canvas 观战客户端 + 托盘 / 浏览器生命周期 |
| `skills/farm_auto.SKILL.md` | OpenClaw Agent Skill（协议 + 循环模板） |
| `docs/PROTOCOL.md` | HTTP / WS 协议说明 |
| `tests/OpenClawFarm.Core.Tests/` | 单元测试（`dotnet test`） |

---

## 顶栏状态说明

| 显示 | 含义 |
|------|------|
| 💰 | 金币 |
| 🕐 | 游戏内时间 |
| 🌤️ | 季节 |
| 🏆 | 通关进度 % |
| 🌾 ⛏️ 🎣 | 三线产出倍率（新档默认 100%，单线挂机久后会降） |
| 🔧矿 🏠 | 矿道 / 建筑耐久（新档 100%，跨天后需维护） |

---

*OpenClaw Farm — 你观战，Agent 劳作。同一款游戏，同一套协议，无需任何插件。*
