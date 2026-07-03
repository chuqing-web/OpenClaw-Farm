---
name: farm-auto
description: >-
  控制 OpenClaw Farm 三线挂机（种植/挖矿/钓鱼）：HTTP POST /agent/action 或 WS /ws/agent
  发动作，GET /agent/state/* 读状态。用于农场自动化、Agent 观战、TaskFlow/Cron 长跑。
metadata:
  openclaw:
    emoji: "🌾"
    requires:
      bins: []
---

# OpenClaw Farm Agent

浏览器仅观战；**移动/交互/出售必须发 action**。只读 `GET /agent/state/*` 不会移动角色。

## 前置

1. 启动：`dotnet run --project src/OpenClawFarm.Server` 或 F5
2. `GET http://127.0.0.1:28080/health` → `{ "ok": true }`
3. 开档：`POST /api/game/new` 或 `/api/game/load`（否则 `no_session`）
4. 沙箱允许 `127.0.0.1:28080`

## 协议

| 通道 | 地址 | 格式 |
|------|------|------|
| HTTP 动作 | `POST /agent/action` | `{ success, message, extra, action }` |
| 动作状态 | `GET /agent/state/action` | `{ busy, currentAction, nextHint, nextActionId, nextParams }` |

**忙碌拒绝**：上一个 action 未完成时再次请求 → `{ success:false, message:"上一个操作…还未结束，请等待", extra:{ busy:true, currentAction } }`

**完成提示**：成功时 `message` 含 `→ 下一步：…`，`extra` 含 `nextHint` / `nextActionId` / `nextParams` 供 Agent 自动接续。

```javascript
async function act(actionId, params = {}) {
  const r = await fetch(BASE+"/agent/action", { method:"POST", headers:{"Content-Type":"application/json"},
    body: JSON.stringify({ actionId, params }) }).then(x=>x.json());
  if (r.extra?.busy) { console.warn("请等待:", r.extra.currentAction); return r; }
  if (r.success && r.extra?.nextHint) console.log("下一步:", r.extra.nextHint, r.extra.nextParams);
  return r;
}

async function waitUntilIdle() {
  for (let i = 0; i < 60; i++) {
    const a = (await httpGet("/agent/state/action")).data;
    if (!a.busy) return a;
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error("action timeout");
}
```

| 通道 | 地址 | 格式 |
|------|------|------|
| HTTP 状态 | `GET /agent/state/*` | `{ data, ts }` |
| WebSocket | `ws://127.0.0.1:28080/ws/agent` | `{ type:"action", reqId, payload:{ actionId, params } }` |

**params 字段**：`x` `y` `sceneId` `targetEntityId` `itemId` `count` `confirmToken` `ms` `merchantId` `rewardId` `buildingId` `layer` `pondId` `direction` `orderId` `seedA` `seedB` `decorationId`

## 动作

| actionId | params | 说明 |
|----------|--------|------|
| `move_to` | `{ x, y }` | 寻路移动 |
| `interact` | `{ targetEntityId, itemId? }` | 地块/NPC/工坊 |
| `sell_item` | `{ itemId, count?, confirmToken, merchantId? }` | 见三类商人 |
| `wait` | `{ ms }` | 最大 30000 |
| `process` | `{ itemId }` | 加工（factory_01 旁） |
| `feed_animal` / `collect_animal` | `{ targetEntityId, itemId? }` | 畜牧 |
| `claim_reward` | `{ rewardId? }` | 领奖 |
| `unlock_building` / `upgrade_building` | `{ buildingId }` | 建筑 |
| `prestige_reset` | — | 转生 |
| `mine_enter` / `mine_leave` / `mine_dig` | — | 挖矿 |
| `mine_layer` | `{ direction:"up"\|"down" }` | 换层 L1–L3 |
| `fish` | `{ pondId, itemId? }` | 钓鱼 p01–p04 |
| `eat_meal` / `forge_pickaxe` / `refuel_lantern` | — | 矿线支撑 |
| `deliver_cross_order` | `{ orderId? }` | 每日跨线单 |
| `deliver_weekly_order` | `{ orderId? }` | 7日跨线单 |
| `deliver_festival` | `{ orderId? }` | 赛季庆典交付 |
| `reinforce_mine` / `repair_buildings` / `feed_pond` | — | 日常维护 |
| `hybrid_seed` | `{ seedA, seedB }` | 基因杂交（factory 旁） |
| `summon_boss` / `attack_boss` | — | 矿洞 L3 Boss |
| `place_decoration` | `{ decorationId }` | 奢侈装饰（永久消耗） |
| `chop_tree` | `{ targetEntityId }` 或 `{ tileX, tileY }` | 砍树得 `crop_wood`（需 `tool_axe`） |
| `build_tile` | `{ buildType, tileX, tileY }` | 建造：`wood_fence` / `wood_path` / `lumber_platform` |

### 树林 / 木材

| 物品 | 说明 |
|------|------|
| `crop_wood` | 砍树产出，各类建筑解锁材料 |
| `crop_plank` | 锯木厂解锁后 3 木→2 板（factory 加工） |
| `tool_axe` | 砍树必备，可 factory 锻造（2 铁锭 + 5 木） |

| 建筑 buildingId | 效果 |
|-----------------|------|
| `lumber_camp` | 800g + 30 木 + 5 板，伐木产量 +25% |
| `sawmill` | 1500g + 20 木 + 8 板 + 3 铁，解锁木板配方 |

树林入口 `(64, 400)` 即 tile `(2,12)`。树 ID 格式 `tree_{tx}_{ty}`，也可 `interact` + `targetEntityId: tree_*`。

**params 字段**：… `tileX` `tileY` `buildType`

| merchantId | 位置 (x,y) | 收购 |
|------------|------------|------|
| `merchant_01` | 976,496 | 作物/加工品 |
| `ore_merchant_01` | 224,720 | 矿石/锭 |
| `fishmonger_01` | 1104,336 | 鱼类/鱼干 |

### interact itemId

| target | itemId | 效果 |
|--------|--------|------|
| `land_01`…`land_24` | `seed_*` / `water` / `harvest` / `clear` | 农事 |
| | `tool_pesticide` / `tool_fertilizer` | 除虫/施肥 |
| `factory_01` | 产出 ID | 加工 |
| `animal_*` | `feed` / `collect` | 畜牧 |

### 杂交配方（factory 旁 `hybrid_seed`）

| seedA + seedB | 产出 |
|---------------|------|
| `seed_strawberry` + `seed_pumpkin` | `seed_hybrid_star` |
| `seed_corn` + `seed_wheat` | `seed_hybrid_gold` |

### 装饰 decorationId（纯消耗）

`decor_flower_bed` `decor_aquarium` `decor_statue`

## 状态接口

| 路径 | 用途 |
|------|------|
| `/agent/state/meta` | **推荐** 全量快照 |
| `/agent/state/merchants` | 三类商人价格 |
| `/agent/state/orders` | 含 `crossLineOrders`（type: daily/weekly/festival） |
| `/agent/state/upkeep` | 维护/矿道/鱼塘 |
| `/agent/state/economy` | 三线衰减 |
| `/agent/state/mine` / `/boss` | 挖矿/Boss |
| `/agent/state/fish_pond?id=` | 鱼塘 |
| `/agent/state/codex` | 观赏鱼图鉴 |
| `/agent/state/forest` | 树林状态（附近树 HP / 再生） |
| `/agent/state/construction` | 已建造地块 |
| `/agent/state/decorations` | 装饰目录 |
| `/agent/state/farm_lands` | `{ data: { lands, total } }` |
| `/agent/state/bag` / `/player` | 背包/坐标 |
| `/agent/state/action` | **忙碌状态 + 下一步建议** |
| `/agent/state/sell_confirm` | 出售令牌 |

## 地图坐标（像素）

| 地点 | x | y |
|------|---|---|
| 作物商 | 976 | 496 |
| 矿石商 | 224 | 720 |
| 渔贩 | 1104 | 336 |
| 矿洞 | 176 | 720 |
| 铁匠 | 912 | 272 |
| 工坊 | 848 | 432 |
| 池塘 p01 / 河 p02 / 湖 p03 / 暗河 p04 | 1136,272 / 1232,400 / 1168,496 / 272,784 |

## JS 运行时

```javascript
const BASE = "http://127.0.0.1:28080";
const POI = { crop: [976,496], ore: [224,720], fish: [1104,336], mine: [176,720],
  blacksmith: [912,272], factory: [848,432], pond: [1136,272] };

async function httpGet(p) { const r = await fetch(BASE+p); if (!r.ok) throw new Error(r.status); return r.json(); }
async function act(actionId, params = {}) {
  const r = await fetch(BASE+"/agent/action", { method:"POST", headers:{"Content-Type":"application/json"},
    body: JSON.stringify({ actionId, params }) });
  return r.json();
}
async function ensureSession(mode="load") {
  const h = await httpGet("/api/snapshot");
  if (h.sessionActive) return;
  const p = mode==="new" ? "/api/game/new" : "/api/game/load";
  const r = await fetch(BASE+p,{method:"POST"}).then(x=>x.json());
  if (!r.success) throw new Error(r.message);
}
async function move(x,y) { return act("move_to",{x,y}); }
async function sell(itemId, count, merchantId="merchant_01") {
  const c = (await httpGet("/agent/state/sell_confirm")).data;
  return act("sell_item", { itemId, count, confirmToken: c.confirmToken, merchantId });
}
async function count(id) {
  return (await httpGet("/agent/state/bag")).data.items.find(i=>i.itemId===id)?.count ?? 0;
}
async function lands() { return (await httpGet("/agent/state/farm_lands")).data.lands; }
```

## 三线挂机 `runTripleLineCycle()`

```javascript
async function runTripleLineCycle() {
  await ensureSession();
  const m = (await httpGet("/agent/state/meta")).data;
  const up = m.upkeep;

  if (up.mineIntegrity < 40) await act("reinforce_mine");
  if (up.buildingDurability < 50) await act("repair_buildings");
  if (up.pondEcology < 55) await act("feed_pond");

  for (const o of m.orders.crossLineOrders.filter(x => !x.completed)) {
    const ready = Object.entries(o.required).every(([id,n]) => (await count(id)) >= n);
    if (!ready) continue;
    const action = o.type === "weekly" ? "deliver_weekly_order"
      : o.type === "festival" ? "deliver_festival" : "deliver_cross_order";
    await act(action, { orderId: o.id });
  }

  const seed = Object.entries(m.season.cropMultipliers)
    .filter(([k,v]) => k.startsWith("seed_") && v >= 1).sort((a,b)=>b[1]-a[1])[0]?.[0] || "seed_strawberry";
  for (const l of await lands()) {
    if (l.canHarvest) { await move(l.x,l.y); await act("interact",{targetEntityId:l.id,itemId:"harvest"}); }
  }
  for (const l of await lands()) {
    if (l.needsWater||l.isDry) { await move(l.x,l.y); await act("interact",{targetEntityId:l.id,itemId:"water"}); }
    if (l.hasPest) { await move(l.x,l.y); await act("interact",{targetEntityId:l.id,itemId:"tool_pesticide"}); }
  }
  for (const l of await lands()) {
    if (l.state==="empty") { await move(l.x,l.y); await act("interact",{targetEntityId:l.id,itemId:seed}); }
  }

  await move(...POI.factory);
  if (await count("crop_wheat")>=2) await act("process",{itemId:"bait_basic"});
  if (await count("crop_wheat")>=2 && await count("crop_corn")>=1) await act("process",{itemId:"crop_charcoal"});
  if (await count("seed_strawberry")>=2 && await count("seed_pumpkin")>=2)
    await act("hybrid_seed",{seedA:"seed_strawberry",seedB:"seed_pumpkin"});

  const pond = m.fishPonds.sort((a,b)=>a.fatigueTicks-b.fatigueTicks)[0];
  if (pond?.caughtToday < pond?.dailyCap) {
    await move(pond.x, pond.y);
    await act("fish",{pondId:pond.id,itemId:pond.baitReq});
  }
  if (await count("fish_medium")>=2 && await count("crop_carrot")>=1)
    await act("process",{itemId:"meal_fish_stew"});

  await move(...POI.mine);
  if (!m.mine.inMine) await act("mine_enter");
  for (let i=0; i<8; i++) {
    const mine = (await httpGet("/agent/state/mine")).data;
    if (mine.stamina<20) break;
    if (mine.stamina<30 && await count("meal_fish_stew")>0) await act("eat_meal");
    if (mine.pickDur<=0) { await move(...POI.blacksmith); await act("forge_pickaxe",{layer:mine.layer}); await move(...POI.mine); await act("mine_enter"); }
    const d = await act("mine_dig");
    if (!d.success) break;
  }
  if (m.mine.layer>=3 && await count("ingot_iron")>=5 && await count("ore_crystal")>=3) {
    await act("summon_boss");
    while ((await httpGet("/agent/state/boss")).data.active) await act("attack_boss");
  }
  await act("mine_leave");

  await move(...POI.crop);
  for (const {itemId,count:n} of (await httpGet("/agent/state/bag")).data.items) {
    if (n<=0) continue;
    const cat = itemId.startsWith("fish_") ? "fishmonger_01"
      : itemId.startsWith("ore_")||itemId.startsWith("ingot_") ? "ore_merchant_01"
      : itemId.startsWith("crop_") && itemId!=="crop_charcoal" ? "merchant_01" : null;
    if (cat) await sell(itemId, n, cat);
  }
  return { ok: true };
}
```

**均衡策略**：库存>80 优先 `process`/`place_decoration`/交付订单；economy 某线<50% 切换产出线。

## 常见错误

| message | 处理 |
|---------|------|
| `no_session` | 开档 |
| `上一个操作…还未结束` | 等待或 `waitUntilIdle()` 后再发 |
| `too far from *` | `move` 到坐标 |
| `* does not buy *` | 换对应 merchantId |
| `need bait` / `stamina depleted` | 加工鱼饵 / `eat_meal` |
| `mine collapsed` | `reinforce_mine` |

## OpenClaw 部署

```bash
openclaw taskflow create farm_loop --label "三线挂机"
openclaw taskflow start farm_loop --skill ./skills/farm_auto.SKILL.md
openclaw cron create farm_cycle --schedule "*/30 * * * *" --session isolated \
  --message "执行 runTripleLineCycle()"
```

关闭浏览器窗口约 2s 后进程退出；托盘右键亦可退出。
