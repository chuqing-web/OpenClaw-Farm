# OpenClaw Farm

> **一个 Skill 文件，一条 WebSocket，你的 Agent 替你 7×24 打理农场。**

[English](README.md) · **简体中文**

---

## 为 AI Agent 而生的农场游戏——不是事后硬贴上去的

大多数「AI 玩游戏」演示，不过是聊天机器人叠在截图工具或脆弱的桌面宏上。**OpenClaw Farm 走另一条路。**

它是一款你可以亲手游玩的像素农场，更是**从第一天就为长期自动化设计**的 Agent 试验场。无需插件、无需 MCP 中间层、无需图像识别。复制一个 `.SKILL.md` 文件，让 OpenClaw Agent 连接 `ws://127.0.0.1:28080`，循环就开始了：收割、浇水、播种、出售、再来一轮。

真实的游戏循环，真实的通信协议，不必再演戏。

---

## 两个玩家，同一个世界

| 你手动玩 | Agent 自动挂机 |
|----------|----------------|
| WASD 穿行田间 | HTTP 读取地块状态 |
| 空格与土地交互 | WebSocket 下发移动 / 交互 / 出售 |
| 勾选**手动模式**接管键盘 | TaskFlow 或 Cron 实现 7×24 托管 |

同一张地图，同样的作物，同样的商人。**手动模式**会暂停 Agent 动作，键盘永远不会和自动化抢控制权。

> *周五晚上你亲手种下草莓；周六早上 Agent 替你收完、卖掉、再种一轮。*

---

## Agent 为什么偏爱这套方案

### 零部署成本
单个 Skill 文件内嵌完整通信层——WebSocket 客户端、HTTP 查询、请求匹配、断线重连。不用重启网关，不用维护插件工程。

### 消息体积极小
纯 JSON：`type`、`reqId`、`payload`。没有 JSON-RPC 外壳，没有 MCP 嵌套。**上下文占用更低，Token 更省**——专为上千次循环的挂机场景优化。

### 读写分离，延迟更低
- **HTTP** 拉取世界状态：地块、背包、物价、每日订单
- **WebSocket** 下发动作，同步等待执行结果

先观察，再行动。游戏始终响应及时。

### 本地回环，安全内置
服务仅绑定 `127.0.0.1`，不暴露局域网。动作冷却 + 出售二次确认，让自动化更像人类操作，而非脚本轰炸。

---

## 有生命的农场，不是 JSON 表格

十二块耕地，三种作物——草莓、小麦、胡萝卜。土地会缺水，疏忽会枯萎。商人收购价每日波动，订单系统奖励你真正种出的作物。

Canvas 客户端把一切可视化：成熟的金色地块、缺水的蓝色标记、商人摊位的高亮范围、像素小人在田间穿行。

可爱画面之下，是完整的模拟 tick、A* 寻路、背包经济、订单结算——Agent 需要推理的真实状态机，而不是永远返回 `success: true` 的假按钮。

---

## 三种方式驱动你的 Agent

### 聊天窗口 — 调试单轮
适合打磨逻辑。跑一轮完整的收割→出售流程，或单独测试一次 `move()`、`interact()`。

### TaskFlow — 真正的 7×24 托管
独立后台会话，关掉聊天窗口农场照样运转。出问题时查日志即可追溯。

### Cron — 分时经营
每 30 分钟打理一次日常；每晚 23 点统一出售。只在农场需要人时唤醒 Agent——不必每秒烧算力。

> *放置挂机是 OpenClaw 的强项场景。这就是标准范例。*

---

## 为谁而做

| 人群 | 你能得到什么 |
|------|--------------|
| **Agent 开发者** | 复制 Skill 内 JS 通信层，适配挖矿、牧场等同协议游戏 |
| **快速原型党** | 改一行作物 ID 即可换策略，底层 WS 代码不用动 |
| **低配长期托管** | 单长连接、串行动作、极小带宽——在你已有的机器上就能跑 |

用 **Visual Studio 2022** 打开解决方案，在 `GameWorld.cs` 设断点，按 F5。你调试的服务器，就是 Agent 连接的那一台。

---

## 60 秒上手

```bash
dotnet run --project src/OpenClawFarm.Server
```

打开 **http://127.0.0.1:28080** — 亲手玩，或放手给 Agent。

将 [`skills/farm_auto.SKILL.md`](skills/farm_auto.SKILL.md) 复制到 OpenClaw skills 目录，然后说：

```
运行一轮完整农场流程：收割、浇水、播种、出售。
```

---

## Visual Studio 2022

> **注意：** `OpenClawFarm.Core` 是类库，不能直接运行。必须启动 **`OpenClawFarm.Server`**。

1. 双击 **`OpenClawFarm.sln`** 用 Visual Studio 2022 打开
2. 在解决方案资源管理器中，右键 **`OpenClawFarm.Server`** → **设为启动项目**（项目名变粗体）
3. 按 **F5**（或绿色 ▶ 按钮）— 浏览器自动打开 http://127.0.0.1:28080
4. 调试游戏逻辑时，在 `src/OpenClawFarm.Core/Game/GameWorld.cs` 等处设断点

需要安装 **.NET 8 SDK**（https://dotnet.microsoft.com/download/dotnet/8.0）。

**若提示「无法直接启动类库输出类型的项目」：** 说明当前启动项是 `OpenClawFarm.Core` 或测试项目，请按上面第 2 步改为 `OpenClawFarm.Server`。

---

## 技术一览

| 模块 | 说明 |
|------|------|
| `src/OpenClawFarm.Core/` | C# 农场模拟（地块、经济、寻路） |
| `src/OpenClawFarm.Server/` | ASP.NET Core — HTTP + WebSocket + Canvas 客户端 |
| `skills/farm_auto.SKILL.md` | OpenClaw Agent Skill（内嵌 JS 通信） |
| `docs/PROTOCOL.md` | Agent 接入协议文档 |
| `OpenClawFarm.sln` | Visual Studio 2022 解决方案 |

**Agent 接口**

| 通道 | 地址 |
|------|------|
| HTTP 状态 | `http://127.0.0.1:28080/agent/state/*` |
| WebSocket 动作 | `ws://127.0.0.1:28080/ws/agent` |

---

*OpenClaw Farm — 你可以自己玩，也可以让 Agent 永远替你玩。同一款游戏，同一套协议，无需任何插件。*
