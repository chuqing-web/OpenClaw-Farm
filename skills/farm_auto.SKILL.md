---
name: farm-auto-ws
description: "OpenClaw Farm 完整指令集：移动/交互/出售/读状态。WS 或 HTTP /agent/action，含全部动作命令与 JS 封装。"
metadata:
  openclaw:
    emoji: "🌾"
    requires:
      bins: []
---

# OpenClaw Farm Agent 完整指令 Skill

单 Skill 涵盖**全部人物动作**：Agent 模式（WS/HTTP action）与浏览器手动模式 API。零插件、零 MCP。

## 运行前置

1. 启动游戏：Visual Studio F5，或 `dotnet run --project src/OpenClawFarm.Server`
2. 确认 `http://127.0.0.1:28080/health` 返回 `{ ok: true }`
3. OpenClaw 沙箱需允许 `127.0.0.1:28080`（fetch 必需；WebSocket 可选）
4. **移动/交互必须发动作**（`sendAction` 或 `/agent/action`），仅 `GET /agent/state/*` 不会移动
5. 浏览器勾选「手动游玩」时，Agent 发动作会**自动切换为 Agent 模式**

## 命令总览

### Agent 动作（推荐，需关闭手动模式或发动作时自动切换）

| 命令 | actionId / 接口 | 说明 |
|------|-----------------|------|
| 移动到坐标 | `move_to` | 寻路走到像素坐标 `(x,y)` |
| 地块交互 | `interact` | 播种 / 浇水 / 收割 / 清理 |
| 出售作物 | `sell_item` | 在商人附近出售，需 confirmToken |
| 等待 | `wait` | 空闲等待（最大 30000ms） |
| 统一入口 | `POST /agent/action` | HTTP 版，参数 `{ actionId, params }` |
| WebSocket | `ws://127.0.0.1:28080/ws/agent` | 发 `{ type:"action", reqId, payload }` |

### 读状态（HTTP GET，不触发动作）

| 命令 | 路径 | 说明 |
|------|------|------|
| 健康检查 | `GET /health` | `{ ok: true }` |
| 全部地块 | `GET /agent/state/farm_lands` | 24 块地状态 |
| 背包 | `GET /agent/state/bag` | 金币 + 物品 |
| 商人价格 | `GET /agent/state/merchant_price` | 作物收购价 |
| 每日订单 | `GET /agent/state/farm_order` | 订单进度 |
| 玩家位置 | `GET /agent/state/player` | `{ x, y, sceneId, facing }` |
| 出售令牌 | `GET /agent/state/sell_confirm` | `sell_item` 前置 |
| 手动模式 | `GET /agent/state/manual_mode` | `{ manualMode: bool }` |
| 完整快照 | `GET /api/snapshot` | 含 merchant / well / map / 热键栏 |
| 地图数据 | `GET /api/map` | 40×30 瓦片 |

### 浏览器手动模式 API（仅 `manualMode=true` 时）

| 命令 | 接口 | 说明 |
|------|------|------|
| 切换模式 | `POST /api/manual_mode` | `{ enabled: true/false }` |
| 方向移动 | `POST /api/manual_move` | `{ dx, dy }` 一次一格，±1 |
| 附近交互 | `POST /api/manual_interact` | `{ landId? }` 水井/商人/地块 |
| 切换热键栏 | `POST /api/select_slot` | `{ slot: 0-7 }` |

### 交互 itemId 一览（`interact` 的 params.itemId）

| itemId | 条件 | 效果 |
|--------|------|------|
| `harvest` | 作物成熟 `canHarvest` | 收割入背包 |
| `water` 或省略 | `needsWater` / `needs_water` | 浇水 |
| `clear` / `clear_withered` | 地块 `withered` | 清理枯萎 |
| `seed_strawberry` | 地块 `empty` + 有种子 | 种草莓 |
| `seed_wheat` | 同上 | 种小麦 |
| `seed_carrot` | 同上 | 种胡萝卜 |
| `seed_corn` | 同上 | 种玉米 |
| `seed_pumpkin` | 同上 | 种南瓜 |

### 物品 ID

| 类型 | ID |
|------|-----|
| 种子 | `seed_strawberry` `seed_wheat` `seed_carrot` `seed_corn` `seed_pumpkin` |
| 作物 | `crop_strawberry` `crop_wheat` `crop_carrot` `crop_corn` `crop_pumpkin` |
| 工具 | `tool_watering_can` `tool_hoe` `tool_sickle` |
| NPC | `merchant_01`（用 sell_item，勿 interact） |
| 地块 | `land_01` … `land_24` |

### 热键栏槽位（手动模式，slot 0-7）

| slot | 物品 | 浏览器按键 |
|------|------|-----------|
| 0 | 洒水壶 | 1 |
| 1 | 草莓种 | 2 |
| 2 | 小麦种 | 3 |
| 3 | 胡萝卜种 | 4 |
| 4 | 玉米种 | 5 |
| 5 | 南瓜种 | 6 |
| 6 | 镰刀 | 7 |
| 7 | 锄头 | 8 |

### 地图关键坐标（像素，sceneId=`farm_main`）

| 地点 | x | y |
|------|---|---|
| 玩家出生点 | 240 | 304 |
| 商人 merchant_01 | 976 | 496 |
| 水井 well_01 | 1104 | 400 |
| 第一块地 land_01 | 304 | 368 |

> 地块坐标以 `GET /agent/state/farm_lands` 返回的 `x,y` 为准。

## 运行参数

| 参数 | 值 | 说明 |
|------|-----|------|
| loop | true | 单轮完成后继续（TaskFlow/Cron 推荐长跑） |
| delayMin | 600 | 单步最小间隔 ms |
| delayMax | 1400 | 单步最大随机间隔 ms |
| timeout | 120000 | 单轮超时 2 分钟 |
| defaultSeed | seed_strawberry | 播种作物 |

## 块 1：全局 JS 通信底层

执行挂机前，先运行以下代码初始化通信层（交互式会话可复用 WS 单例）：

```javascript
const BASE = "http://127.0.0.1:28080";
const WS_URL = "ws://127.0.0.1:28080/ws/agent";
const DELAY_MIN = 600;
const DELAY_MAX = 1400;
const ACTION_TIMEOUT = 10000;

let wsInstance = null;
let wsConnecting = null;
const waitMap = new Map();
let farmStopped = false;

function genReqId() {
  return crypto.randomUUID();
}

function randomDelay() {
  return DELAY_MIN + Math.floor(Math.random() * (DELAY_MAX - DELAY_MIN + 1));
}

async function waitMs(ms) {
  await new Promise((r) => setTimeout(r, ms));
}

function getWs() {
  if (farmStopped) throw new Error("farm stopped");
  if (wsInstance && wsInstance.readyState === WebSocket.OPEN) return wsInstance;
  if (wsConnecting) return wsConnecting;

  wsConnecting = new Promise((resolve, reject) => {
    const ws = new WebSocket(WS_URL);
    ws.onopen = () => {
      wsInstance = ws;
      wsConnecting = null;
      resolve(ws);
    };
    ws.onerror = (e) => {
      wsConnecting = null;
      reject(e);
    };
    ws.onclose = () => {
      wsInstance = null;
      if (!farmStopped) setTimeout(() => getWs().catch(() => {}), 3000);
    };
    ws.onmessage = (ev) => {
      try {
        const msg = JSON.parse(ev.data);
        if (msg.type === "action_result" && waitMap.has(msg.reqId)) {
          const cb = waitMap.get(msg.reqId);
          waitMap.delete(msg.reqId);
          cb(msg);
        }
      } catch (_) {}
    };
  });
  return wsConnecting;
}

async function sendActionWs(actionId, params) {
  const reqId = genReqId();
  const ws = await getWs();
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      waitMap.delete(reqId);
      reject(new Error(`timeout: ${actionId}`));
    }, ACTION_TIMEOUT);
    waitMap.set(reqId, (result) => {
      clearTimeout(timer);
      resolve(result);
    });
    ws.send(JSON.stringify({ type: "action", reqId, payload: { actionId, params } }));
  });
}

/** HTTP 动作（沙箱无 WebSocket 时用此接口） */
async function sendActionHttp(actionId, params) {
  const res = await fetch(BASE + "/agent/action", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ actionId, params: params || {} }),
  });
  if (!res.ok) throw new Error(`HTTP action ${res.status}`);
  return res.json();
}

async function sendAction(actionId, params) {
  try {
    if (wsInstance?.readyState === WebSocket.OPEN) {
      return await sendActionWs(actionId, params);
    }
    await getWs();
    return await sendActionWs(actionId, params);
  } catch {
    return sendActionHttp(actionId, params);
  }
}

async function httpGet(path) {
  const res = await fetch(BASE + path);
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${path}`);
  return res.json();
}

async function move(x, y, sceneId = "farm_main") {
  const r = await sendAction("move_to", { x, y, sceneId });
  await waitMs(randomDelay());
  return r;
}

async function interact(targetEntityId, itemId) {
  const params = { targetEntityId };
  if (itemId) params.itemId = itemId;
  const r = await sendAction("interact", params);
  await waitMs(randomDelay());
  return r;
}

async function sellItem(itemId, count) {
  const confirm = await httpGet("/agent/state/sell_confirm");
  const token = confirm.data.confirmToken;
  const r = await sendAction("sell_item", { itemId, count, confirmToken: token });
  await waitMs(randomDelay());
  return r;
}

async function getLands() {
  return httpGet("/agent/state/farm_lands");
}

async function getBag() {
  return httpGet("/agent/state/bag");
}

async function getPrices() {
  return httpGet("/agent/state/merchant_price");
}

async function getOrders() {
  return httpGet("/agent/state/farm_order");
}

async function getPlayer() {
  return httpGet("/agent/state/player");
}

async function getManualMode() {
  return httpGet("/agent/state/manual_mode");
}

async function getSnapshot() {
  return httpGet("/api/snapshot");
}

async function getMap() {
  return httpGet("/api/map");
}

async function httpPost(path, body) {
  const res = await fetch(BASE + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}: ${path}`);
  return res.json();
}

// ─── Agent 动作封装 ───

async function wait(ms) {
  const r = await sendAction("wait", { ms: Math.min(ms, 30000) });
  await waitMs(randomDelay());
  return r;
}

async function harvest(landId) {
  return interact(landId, "harvest");
}

async function water(landId) {
  return interact(landId, "water");
}

async function clearLand(landId) {
  return interact(landId, "clear");
}

async function plant(landId, seedId = "seed_strawberry") {
  return interact(landId, seedId);
}

async function sellAllCrops() {
  const bag = await getBag();
  const results = [];
  for (const item of bag.data.items) {
    if (!item.itemId.startsWith("crop_")) continue;
    results.push(await sellItem(item.itemId, item.count));
  }
  return results;
}

async function setManualMode(enabled) {
  return httpPost("/api/manual_mode", { enabled });
}

async function enableAgentMode() {
  return setManualMode(false);
}

async function enableManualMode() {
  return setManualMode(true);
}

// ─── 浏览器手动模式 API（manualMode=true） ───

async function manualMove(dx, dy) {
  return httpPost("/api/manual_move", { dx, dy });
}

async function manualInteract(landId) {
  return httpPost("/api/manual_interact", landId ? { landId } : {});
}

async function selectHotbarSlot(slot) {
  return httpPost("/api/select_slot", { slot });
}

function stopFarm() {
  farmStopped = true;
  waitMap.clear();
  if (wsInstance) {
    wsInstance.close();
    wsInstance = null;
  }
}
```

## 块 2：业务挂机流程

默认作物 `seed_strawberry`，修改 `DEFAULT_SEED` 即可切换。

```javascript
const DEFAULT_SEED = "seed_strawberry";

async function runFarmCycle() {
  // 可选：显式关闭手动模式（发动作时服务端也会自动切换）
  await fetch(BASE + "/api/manual_mode", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ enabled: false }),
  });

  const landsRes = await getLands();
  const lands = landsRes.data;

  for (const land of lands) {
    if (!land.canHarvest) continue;
    const r1 = await move(land.x, land.y);
    if (!r1.success) console.log("move fail:", land.id, r1.message);
    const r = await interact(land.id, "harvest");
    if (!r.success) console.log("harvest fail:", land.id, r.message);
  }

  for (const land of lands) {
    if (!land.needsWater && land.state !== "needs_water") continue;
    await move(land.x, land.y);
    const r = await interact(land.id, "water");
    if (!r.success) console.log("water fail:", land.id, r.message);
  }

  const landsRes2 = await getLands();
  for (const land of landsRes2.data) {
    if (land.state !== "empty") continue;
    await move(land.x, land.y);
    const r = await interact(land.id, DEFAULT_SEED);
    if (!r.success) console.log("plant fail:", land.id, r.message);
  }

  const snap = await httpGet("/api/snapshot");
  const m = snap.data.merchant;
  await move(m.x, m.y, m.sceneId);
  const bagRes = await getBag();
  for (const item of bagRes.data.items) {
    if (!item.itemId.startsWith("crop_")) continue;
    const r = await sellItem(item.itemId, item.count);
    if (!r.success) console.log("sell fail:", item.itemId, r.message);
  }

  await sendAction("wait", { ms: 2000 });
  return { ok: true, message: "cycle complete" };
}
```

## 块 3：单动作命令示例

执行前需先运行**块 1** 通信代码。

```javascript
// ── 移动 ──
await move(304, 368);                    // 走到 land_01
await move(976, 496, "farm_main");       // 走到商人
await sendAction("move_to", { x: 1104, y: 400 });  // 走到水井（Agent 模式）

// ── 地块：收割 / 浇水 / 清理 / 播种 ──
await harvest("land_01");
await water("land_03");
await clearLand("land_05");
await plant("land_02", "seed_wheat");
await plant("land_04", "seed_carrot");
await interact("land_06", "seed_corn");  // 通用 interact

// ── 出售（须先走到商人 3 格内） ──
await move(976, 496);
await sellItem("crop_strawberry", 5);    // 卖 5 个草莓
await sellAllCrops();                    // 卖光所有 crop_*

// ── 等待 ──
await wait(2000);

// ── 读状态 ──
await getPlayer();
await getLands();
await getBag();
await getPrices();
await getOrders();
await getSnapshot();   // 含 merchant / well 坐标
await getManualMode();

// ── 模式切换 ──
await enableAgentMode();   // 关闭手动，Agent 接管
await enableManualMode();  // 开启手动，浏览器 WASD

// ── 浏览器手动模式（需 manualMode=true） ──
await enableManualMode();
await manualMove(1, 0);    // 向右一格
await manualMove(0, -1);   // 向上一格
await selectHotbarSlot(1); // 选草莓种（slot 1）
await manualInteract("land_01");  // 对最近地块/smart 交互
await manualInteract();         // 在水井/商人旁：打水/批量出售
```

### curl 快速测试

```bash
# 移动
curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"move_to\",\"params\":{\"x\":304,\"y\":368}}"

# 收割
curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"interact\",\"params\":{\"targetEntityId\":\"land_01\",\"itemId\":\"harvest\"}}"

# 播种
curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"interact\",\"params\":{\"targetEntityId\":\"land_02\",\"itemId\":\"seed_wheat\"}}"

# 浇水
curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"interact\",\"params\":{\"targetEntityId\":\"land_03\",\"itemId\":\"water\"}}"

# 清理
curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"interact\",\"params\":{\"targetEntityId\":\"land_04\",\"itemId\":\"clear\"}}"

# 等待
curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"wait\",\"params\":{\"ms\":1000}}"

# 手动方向移动
curl -X POST http://127.0.0.1:28080/api/manual_move -H "Content-Type: application/json" -d "{\"dx\":1,\"dy\":0}"
```

### WebSocket 原始消息格式

```json
{ "type": "action", "reqId": "uuid", "payload": { "actionId": "move_to", "params": { "x": 304, "y": 368 } } }
{ "type": "action", "reqId": "uuid", "payload": { "actionId": "interact", "params": { "targetEntityId": "land_01", "itemId": "harvest" } } }
{ "type": "action", "reqId": "uuid", "payload": { "actionId": "sell_item", "params": { "itemId": "crop_wheat", "count": 3, "confirmToken": "..." } } }
{ "type": "action", "reqId": "uuid", "payload": { "actionId": "wait", "params": { "ms": 2000 } } }
```

### 常见失败 message

| message | 原因 |
|---------|------|
| `manual_mode` | 手动模式未关；发 Agent 动作或 `enableAgentMode()` |
| `too far from land_XX` | 未走到地块旁，先 `move(land.x, land.y)` |
| `too far from merchant` | 离商人太远，先走到 merchant 坐标 |
| `no path to (x,y)` | 目标不可达（墙/水） |
| `no seed_* in bag` | 背包无种子 |
| `invalid or expired confirmToken` | 重新 `GET /agent/state/sell_confirm` |
| `not enough crop_*` | 背包数量不足 |

## 块 4：运行控制指令

### 方式 1：聊天窗口手动

- 运行一轮：`运行农场全自动WS直连挂机` 或 `执行JS代码 await runFarmCycle()`
- 移动测试：`执行JS代码 await move(304,368)`
- 收割测试：`执行JS代码 await harvest("land_01")`
- 读状态：`执行JS代码 await getLands()`
- 紧急停止：`立刻停止所有农场操作，关闭WS连接，清空等待队列` → 执行 `stopFarm()`

### 方式 2：TaskFlow 后台 7×24

```bash
openclaw taskflow create farm_ws_loop --label "农场后台循环挂机"
openclaw taskflow start farm_ws_loop --skill ./skills/farm_auto.SKILL.md
openclaw taskflow pause farm_ws_loop
openclaw taskflow stop farm_ws_loop
openclaw logs read farm_ws_loop --lines 200
```

### 方式 3：Cron 定时

```bash
openclaw cron create farm_cycle --schedule "*/30 * * * *" --session isolated --message "运行农场全自动WS直连挂机，执行 runFarmCycle()"
openclaw cron create sell_daily --schedule "0 23 * * *" --session isolated --message "移动到商人并出售全部作物"
openclaw cron list
```

> 超过 1 分钟的空闲等待用 Cron 唤醒，避免 agent 超时。7×24 长跑推荐 TaskFlow 或 Cron，而非单会话 `while(true)`。

## 调试

- 浏览器：`http://127.0.0.1:28080/agent/state/farm_lands`
- HTTP 移动测试：`curl -X POST http://127.0.0.1:28080/agent/action -H "Content-Type: application/json" -d "{\"actionId\":\"move_to\",\"params\":{\"x\":304,\"y\":368}}"`
- wscat：`wscat -c ws://127.0.0.1:28080/ws/agent`
- 手动发 action：`{"type":"action","reqId":"test-1","payload":{"actionId":"move_to","params":{"x":304,"y":368}}}`

若日志里只有 `GET /agent/state/*` 没有 `POST /agent/action` 或 WS action，说明 Agent **只读了状态、没发移动指令**。

## 地块状态说明

| state | 含义 | 可用动作 |
|-------|------|---------|
| `empty` | 空地 | `plant(landId, seed_*)` |
| `growing` | 生长中 | `water`（若 needsWater） |
| `needs_water` | 缺水 | `water` |
| `mature` / canHarvest | 可收割 | `harvest` |
| `withered` | 枯萎 | `clear` |

## 扩展

切换默认作物：改 `DEFAULT_SEED` 为 `seed_wheat`、`seed_carrot`、`seed_corn` 或 `seed_pumpkin`。

复制块 1 通信代码到其他 Skill 即可复用到同协议游戏。
