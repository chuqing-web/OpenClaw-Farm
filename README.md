# OpenClaw Farm

> **One Skill file. One WebSocket. Your agent runs the farm while you sleep.**

**English** · [简体中文](README.zh-CN.md)

---

## The farm game built for AI agents — not bolted on afterward

Most “AI gaming” demos stack a chatbot on top of a screenshot tool or a fragile desktop macro. **OpenClaw Farm is different.**

It is a playable pixel farm you can enjoy yourself — and a **first-class agent playground** designed from day one for long-running automation. No plugins. No MCP middle layer. No image recognition. Copy a single `.SKILL.md` file, point your OpenClaw agent at `ws://127.0.0.1:28080`, and the loop begins: harvest, water, plant, sell, repeat.

You get a real game loop. Your agent gets a real protocol. Everyone stops pretending.

---

## Two players, one world

| You play manually | Your agent plays automatically |
|-------------------|-------------------------------|
| WASD through the fields | Reads land state over HTTP |
| Space to interact with the soil | Sends move / interact / sell over WebSocket |
| Toggle **Manual Mode** when you want the keyboard | TaskFlow or Cron when you want 24/7 hands-off |

Same map. Same crops. Same merchant. **Manual Mode** pauses agent actions so you never fight the keyboard for control.

> *Friday night: you plant strawberries by hand. Saturday morning: the agent finishes what you started.*

---

## Why agents love this setup

### Zero deploy overhead
One Skill file embeds the entire communication stack — WebSocket client, HTTP queries, request matching, auto-reconnect. No gateway restart. No plugin build pipeline.

### Smallest possible messages
Plain JSON: `type`, `reqId`, `payload`. No JSON-RPC wrappers. No MCP envelopes. **Less noise in context, lower token burn** — built for thousand-iteration idle loops.

### Read/write split that actually makes sense
- **HTTP** pulls world state: lands, bag, prices, daily orders
- **WebSocket** pushes actions and waits for results synchronously

Agents observe first, act second. The game stays responsive.

### Local-only by design
Everything binds to `127.0.0.1`. No LAN exposure. No public attack surface. Action cooldowns and sell confirmation keep automation from looking like a bot swarm.

---

## A living farm, not a JSON spreadsheet

Twelve tilled plots. Three crops — strawberry, wheat, carrot. Soil dries out. Crops wither if neglected. A merchant whose prices shift day to day. Daily orders that reward the crops you actually grow.

The Canvas client shows it all: golden mature plots, blue thirst indicators, a glowing merchant stall, your little pixel farmer walking the rows.

Behind the cute pixels: a full simulation tick, A* pathfinding, inventory, economy, and order fulfillment — the kind of state machine agents need to reason about, not a fake “success: true” button.

---

## Three ways to run your agent

### Chat — debug one cycle
Perfect for tuning logic. Run a single harvest-to-sell loop. Test one `move()` or `interact()` in isolation.

### TaskFlow — true 24/7 hosting
Isolated background session. Close the chat window; the farm keeps running. Inspect logs when something looks off.

### Cron — scheduled care
Every 30 minutes for routine upkeep. 11 PM for a bulk sell. Wake the agent only when the farm needs attention — not on your dime every second.

> *Idle games are OpenClaw’s superpower. This is the reference implementation.*

---

## Built for builders

| Audience | What you get |
|----------|--------------|
| **Agent hackers** | Copy the Skill’s JS transport layer to mining, ranching, or any game that speaks the same protocol |
| **Game prototypers** | Change crop IDs in one line; swap business logic without touching WebSocket code |
| **Low-spec hosts** | One long-lived connection, serial actions, minimal bandwidth — runs on the machine you already have |

Open the solution in **Visual Studio 2022**, set a breakpoint in `GameWorld.cs`, press F5. The same server your agent talks to is the one you debug.

---

## Get started in 60 seconds

```bash
dotnet run --project src/OpenClawFarm.Server
```

Open **http://127.0.0.1:28080** — play by hand or unleash the agent.

Copy [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md) into your OpenClaw skills folder and say:

```
Run one full farm cycle: harvest, water, plant, sell.
```

---

## Visual Studio 2022

> **Important:** `OpenClawFarm.Core` is a class library — it cannot run alone. Always start **`OpenClawFarm.Server`**.

1. Double-click **`OpenClawFarm.sln`** to open in Visual Studio 2022
2. In Solution Explorer, right-click **`OpenClawFarm.Server`** → **Set as Startup Project** (project name turns bold)
3. Press **F5** (or the green ▶ button) — browser opens http://127.0.0.1:28080
4. To debug game logic, set breakpoints in `src/OpenClawFarm.Core/Game/GameWorld.cs` etc.

Requires **.NET 8 SDK** (install from https://dotnet.microsoft.com/download/dotnet/8.0).

**If you see "Cannot start class library project":** you selected `OpenClawFarm.Core` or `OpenClawFarm.Core.Tests` as startup — switch to `OpenClawFarm.Server` as above.

---

## Under the hood

| Piece | Role |
|-------|------|
| `src/OpenClawFarm.Core/` | C# farm simulation (lands, economy, pathfinding) |
| `src/OpenClawFarm.Server/` | ASP.NET Core — HTTP + WebSocket + static Canvas client |
| `skills/farm_auto.SKILL.md` | OpenClaw Agent Skill (embedded JS for WS/HTTP) |
| `docs/PROTOCOL.md` | Agent API reference |
| `OpenClawFarm.sln` | Visual Studio 2022 solution |

**Agent endpoints**

| Channel | URL |
|---------|-----|
| HTTP state | `http://127.0.0.1:28080/agent/state/*` |
| WebSocket actions | `ws://127.0.0.1:28080/ws/agent` |

---

*OpenClaw Farm — play it yourself, or let your agent run it forever. Same game, same protocol, no plugins required.*
