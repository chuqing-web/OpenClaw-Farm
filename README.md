# OpenClaw Farm

> **One Skill file. One WebSocket. Your agent runs the farm while you sleep.**

**English** · [简体中文](README.zh-CN.md)

---

## The farm game built for AI agents — not bolted on afterward

Most “AI gaming” demos stack a chatbot on a screenshot tool or a fragile desktop macro. **OpenClaw Farm is different.**

It is a watchable pixel farm built for **long-running agent automation** from day one: no plugins, no MCP, no image recognition. Copy [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md), connect to `ws://127.0.0.1:28080`, and the loop begins — farm, mine, fish, chop trees, build, sell, maintain.

The browser is a **spectator client**. Movement and interactions must go through **HTTP / WebSocket actions** (or tell your OpenClaw agent in chat to send them).

---

## Two players, one world

| You (watch & command) | Agent (executes) |
|-----------------------|------------------|
| Canvas view: character, animations, HUD | `GET /agent/state/*` for full world state |
| Chat: “chop wood”, “deliver cross-line order” | `POST /agent/action` or WebSocket |
| HUD: gold, triple-line yields, upkeep | `GET /agent/state/action` for busy state + next hint |

Same map, same rules. Actions run **serially** — concurrent requests get “wait for current action” until the previous one finishes.

---

## What’s in the game

### Triple-line idle loop

| Route | Features |
|-------|----------|
| 🌾 **Farm** | 24 plots, 5 crops, greenhouse / auto sprinkler / harvester, processing factory |
| ⛏️ **Mine** | 3 layers, stamina / pickaxe / lantern, L3 boss |
| 🎣 **Fish** | Multiple ponds, bait, dried fish, collectible codex |

Three merchants (crops, ore, fish). **Daily / weekly / festival cross-line orders** push all three routes.

### Forest & construction

- Chop western forest → `crop_wood` (requires `tool_axe`)
- Sawmill unlocks `crop_plank` processing
- Buildings: **lumber camp** (+25% wood), **sawmill** (plank recipes)
- `build_tile`: `wood_fence`, `wood_path`, `lumber_platform`

### Forced upkeep & economy

HUD **🌾⛏️🎣** shows **yield multipliers** (start at 100%; decay toward ~20% if you only idle one route). **🔧 / 🏠** are mine and building durability — they drop each in-game day; use `reinforce_mine`, `repair_buildings`, `feed_pond`, etc.

Daily livestock feed, greenhouse heating, building maintenance, and passive inventory decay keep long AFK runs from being “set and forget forever.”

### Meta systems

Achievements, victory progress, prestige, hybrid seeds, land bonds, decorations, save/load (`POST /api/game/new` · `/api/game/load` · `/api/game/save`).

---

## Why agents love this setup

### Zero deploy overhead
One Skill embeds WS/HTTP helpers, busy-wait, and a triple-line cycle template.

### Tiny messages
Plain JSON: `type`, `reqId`, `payload`. HTTP to observe, WS/HTTP to act.

### Next-step hints after every success
Responses include `→ next step:` plus `nextHint` / `nextActionId` in `extra` for auto-chaining.

### Local-only
Binds to `127.0.0.1`. Cooldowns + sell confirmation for sane Cron / TaskFlow runs.

---

## Get started in 60 seconds

```bash
dotnet run --project src/OpenClawFarm.Server
```

1. Open **http://127.0.0.1:28080**
2. **New game** or **Continue** (session required — otherwise `no_session`)
3. Copy [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md) into OpenClaw skills, then:

```
Run one triple-line cycle: farm → chop if low on wood → mine → fish → upkeep → sell.
```

### Windows single-file build

```powershell
.\publish.ps1
# → dist\OpenClawFarm\OpenClawFarm.exe — double-click; exit via tray icon
```

---

## Agent API cheat sheet

| Purpose | URL |
|---------|-----|
| Health | `GET /health` |
| **Full snapshot (recommended)** | `GET /agent/state/meta` |
| Busy + next hint | `GET /agent/state/action` |
| Forest / construction | `GET /agent/state/forest` · `/agent/state/construction` |
| Execute action | `POST /agent/action` `{ "actionId", "params" }` |
| WebSocket | `ws://127.0.0.1:28080/ws/agent` |

Start session: `POST /api/game/new` · `POST /api/game/load`

Common actions: `move_to` · `interact` · `chop_tree` · `build_tile` · `process` · `mine_*` · `fish` · `sell_item` · `unlock_building` · `deliver_cross_order` · `reinforce_mine` · `repair_buildings`

Full list: [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md) and [`docs/PROTOCOL.md`](docs/PROTOCOL.md).

---

## Visual Studio 2022

> **Important:** Start **`OpenClawFarm.Server`** — Core is a class library and cannot run alone.

1. Open **`OpenClawFarm.sln`**
2. Right-click **`OpenClawFarm.Server`** → **Set as Startup Project**
3. **F5** → browser opens http://127.0.0.1:28080
4. Breakpoint in `GameWorld.cs` to debug simulation logic

Requires **.NET 8 SDK**.

---

## Repository layout

| Path | Role |
|------|------|
| `src/OpenClawFarm.Core/` | Simulation: lands, triple-line, forest, construction, economy, upkeep, saves |
| `src/OpenClawFarm.Server/` | ASP.NET Core + Canvas client + tray / browser lifecycle |
| `skills/farm_auto.SKILL.md` | OpenClaw Agent Skill (protocol + cycle templates) |
| `docs/PROTOCOL.md` | HTTP / WebSocket reference |
| `tests/OpenClawFarm.Core.Tests/` | Unit tests (`dotnet test`) |

---

## HUD legend

| Display | Meaning |
|---------|---------|
| 💰 | Gold |
| 🕐 | In-game time |
| 🌤️ | Season |
| 🏆 | Victory progress % |
| 🌾 ⛏️ 🎣 | Route yield multipliers (100% at start; decays if one route dominates) |
| 🔧 / 🏠 | Mine / building durability (100% at start; drops after in-game days) |

---

*OpenClaw Farm — you watch, your agent works. Same game, same protocol, no plugins required.*
