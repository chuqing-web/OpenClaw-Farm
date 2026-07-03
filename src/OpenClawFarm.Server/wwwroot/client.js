const TILE = 32;
const VIEW_W = 960;
const VIEW_H = 640;
const MOVE_SPEED = 128;
const CAMERA_LERP = 0.1;

const ITEM_EMOJI = {
  seed_strawberry: "🍓", seed_wheat: "🌾", seed_carrot: "🥕", seed_corn: "🌽", seed_pumpkin: "🎃",
  crop_strawberry: "🍓", crop_wheat: "🌾", crop_carrot: "🥕", crop_corn: "🌽", crop_pumpkin: "🎃",
  crop_charcoal: "🪵", crop_jam: "🫙", crop_flour: "🌾", crop_cheese: "🧀",
  crop_wood: "🪵", crop_plank: "📐",
  tool_watering_can: "🚿", tool_hoe: "⛏️", tool_sickle: "🔪", tool_axe: "🪓", tool_pickaxe: "⛏️", tool_rod: "🎣",
  ore_stone: "🪨", ore_iron: "⛓️", ore_silver: "🥈", ore_gold: "🥇", ore_crystal: "💎",
  ingot_iron: "🔩", ingot_silver: "⚙️",
  fish_common: "🐟", fish_medium: "🐠", fish_rare: "🐡", fish_glow: "✨", fish_dried: "🐟",
  bait_basic: "🪱", bait_advanced: "🦐", meal_fish_stew: "🍲",
};

const CROP_COLORS = {
  crop_strawberry: "#e74c6f", crop_wheat: "#d4a82a", crop_carrot: "#e67e22",
  crop_corn: "#f1c40f", crop_pumpkin: "#e8963a",
};

const ACTIVITY_LABELS = {
  mine: "⛏️ 挖矿中…", fish: "🎣 垂钓中…", harvest: "🌾 收割中…",
  plant: "🌱 播种中…", water: "💧 浇水中…", forge: "🔨 锻造中…",
  process: "🏭 加工中…", enter_mine: "🕳️ 进入矿洞…", chop: "🪓 砍树中…", build: "🔨 建造中…",
};

const POIS = [
  { id: "forest", emoji: "🌲", tx: 2, ty: 12, label: "树林" },
  { id: "mine", emoji: "⛏️", tx: 5, ty: 22, label: "矿洞" },
  { id: "ore_merchant", emoji: "💎", tx: 7, ty: 22, label: "矿石商" },
  { id: "blacksmith", emoji: "🔨", tx: 28, ty: 8, label: "铁匠" },
  { id: "pond", emoji: "🎣", tx: 35, ty: 7, label: "池塘" },
  { id: "fishmonger", emoji: "🐟", tx: 34, ty: 10, label: "渔贩" },
  { id: "river", emoji: "🌊", tx: 38, ty: 12, label: "河流" },
  { id: "lake", emoji: "🏞️", tx: 36, ty: 15, label: "湖泊" },
  { id: "factory", emoji: "🏭", tx: 26, ty: 13, label: "工坊" },
];

let activeMerchantIdx = 0;

const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");
const minimap = document.getElementById("minimap");
const mctx = minimap.getContext("2d");

let state = {
  player: { x: 240, y: 304, facing: "down" },
  lands: [], bag: { gold: 0, items: [] },
  merchant: { x: 976, y: 496 }, well: { x: 1104, y: 400 },
  merchantPrices: {}, farmOrder: { orders: [] },
  mapWidth: 40, mapHeight: 30, tiles: [], gameHour: 8,
};

let displayPlayer = { x: 240, y: 304, facing: "down" };
let camera = { x: 0, y: 0 };
let cameraTarget = { x: 0, y: 0 };
let animFrame = 0;
let lastFrameTs = 0;
let particles = [];
let floatTexts = [];
let hoverTile = null;
let moveQueue = [];
let landAnims = {};
let currentActivity = null;
let metaCache = null;
let displayGold = 0;

const toastEl = document.getElementById("toast");
const activityLabel = document.getElementById("activity-label");
let toastTimer;

function showToast(msg, ok = true) {
  toastEl.textContent = msg;
  toastEl.style.borderColor = ok ? "#5a8f42" : "#e86c4a";
  toastEl.style.color = ok ? "#8fd464" : "#e86c4a";
  toastEl.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toastEl.classList.remove("show"), 2200);
}

function spawnParticles(x, y, color, count = 8) {
  for (let i = 0; i < count; i++) {
    particles.push({
      x, y, vx: (Math.random() - 0.5) * 3, vy: (Math.random() - 0.5) * 3 - 1,
      life: 24 + Math.random() * 20, color, size: 2 + Math.random() * 2,
    });
  }
}

function spawnFloatText(x, y, text, color = "#f4c542") {
  floatTexts.push({ x, y, text, color, life: 60, vy: -0.8 });
}

function queuePlayerMove(px, py, facing) {
  const last = moveQueue.length ? moveQueue[moveQueue.length - 1] : displayPlayer;
  const d = Math.hypot(px - last.x, py - last.y);
  if (d < 1) return;
  if (d > TILE * 1.2) {
    const steps = Math.ceil(d / TILE);
    for (let i = 1; i <= steps; i++) {
      const t = i / steps;
      moveQueue.push({
        x: last.x + (px - last.x) * t,
        y: last.y + (py - last.y) * t,
        facing: facing || last.facing,
      });
    }
  } else {
    moveQueue.push({ x: px, y: py, facing: facing || last.facing });
  }
}

function startActivity(act) {
  if (!act?.kind) return;
  currentActivity = {
    kind: act.kind, x: act.x, y: act.y,
    start: performance.now(), duration: act.durationMs || 600,
    progress: 0,
  };
  if (activityLabel) {
    activityLabel.textContent = ACTIVITY_LABELS[act.kind] || "⚙️ 工作中…";
    activityLabel.classList.add("active");
  }
  const colors = { mine: "#aaa", fish: "#5eb8e8", harvest: "#f4c542", plant: "#8fd464", water: "#5eb8e8", forge: "#e8963a", process: "#c9a227", chop: "#6b4428", build: "#8b6914" };
  spawnParticles(act.x, act.y, colors[act.kind] || "#8fd464", 6);
}

let pageClosing = false;
let viewWs = null;

function connectViewWs() {
  if (pageClosing) return;
  const proto = location.protocol === "https:" ? "wss:" : "ws:";
  viewWs = new WebSocket(`${proto}//${location.host}/ws/view`);
  viewWs.onclose = () => {
    if (!pageClosing) setTimeout(connectViewWs, 1200);
  };
  viewWs.onerror = () => {
    try { viewWs.close(); } catch (_) {}
  };
}

window.addEventListener("pagehide", (ev) => {
  if (ev.persisted) return;
  pageClosing = true;
  try { viewWs?.close(); } catch (_) {}
});

function connectWs() {
  const ws = new WebSocket(`ws://${location.host}/ws/agent`);
  ws.onmessage = (ev) => {
    try {
      const msg = JSON.parse(ev.data);
      if (msg.type === "world_patch") applyPatch(msg);
    } catch (_) {}
  };
  ws.onclose = () => setTimeout(connectWs, 3000);
}

function syncLandAnims(lands) {
  for (const land of lands) {
    if (!landAnims[land.id]) landAnims[land.id] = { growth: land.growth, flash: 0 };
    landAnims[land.id].targetGrowth = land.growth;
    if (land.canHarvest) landAnims[land.id].pulse = (landAnims[land.id].pulse || 0) + 1;
  }
}

function applyPatch(msg) {
  if (msg.activity) startActivity({ kind: msg.activity.kind, x: msg.activity.x, y: msg.activity.y, durationMs: msg.activity.durationMs });

  if (msg.player) {
    queuePlayerMove(msg.player.x, msg.player.y, msg.player.facing);
    state.player = msg.player;
  }
  if (msg.lands) { syncLandAnims(msg.lands); state.lands = msg.lands; }
  if (msg.bag) {
    if (msg.bag.gold > state.bag.gold) spawnFloatText(displayPlayer.x, displayPlayer.y - 20, `+${msg.bag.gold - state.bag.gold}💰`, "#f4c542");
    state.bag = msg.bag;
  }
  if (msg.gameHour !== undefined) state.gameHour = msg.gameHour;
  if (msg.tileUpdates?.length) {
    for (const u of msg.tileUpdates) {
      if (state.tiles[u.ty]) state.tiles[u.ty][u.tx] = u.type;
    }
  }
  updateHud();
}

async function loadMeta() {
  try {
    const res = await fetch("/agent/state/meta");
    const { data: m } = await res.json();
    metaCache = m;
    document.getElementById("season-label").textContent = m.season.season;
    document.getElementById("progress-pct").textContent = `${m.progress.perfectPercent}%`;
    document.getElementById("victory-list").innerHTML = m.progress.victories.map((v) =>
      `<li class="fade-in">${v.achieved ? "✅" : "⬜"} ${v.name}</li>`
    ).join("");
    document.getElementById("achievement-summary").textContent =
      `${m.achievements.unlockedCount} / ${m.achievements.totalCount} 已解锁`;
    if (m.economy) {
      const el = document.getElementById("economy-bar");
      if (el) el.innerHTML = `
        <span>🌾 ${Math.round(m.economy.farmYield * 100)}%</span>
        <span>⛏️ ${Math.round(m.economy.mineYield * 100)}%</span>
        <span>🎣 ${Math.round(m.economy.fishYield * 100)}%</span>`;
    }
    if (m.mine) {
      const el = document.getElementById("mine-status");
      if (el) el.textContent = m.mine.inMine ? `矿层 L${m.mine.layer} · 体力${m.mine.stamina} · 镐${m.mine.pickDur}` : "地面";
    }
    if (m.boss) {
      const el = document.getElementById("boss-status");
      if (el) el.textContent = m.boss.active ? `👹 Boss ${m.boss.bossHp}/${m.boss.maxHp}` : `Boss 已击败 ${m.boss.defeatedCount} 次`;
    }
    if (m.codex) {
      const el = document.getElementById("codex-grid");
      if (el) el.innerHTML = m.codex.entries.map((e) =>
        `<div class="item-cell ${e.discovered ? "" : "dim"}">${ITEM_EMOJI[e.fishId] || "🐟"}${e.discovered ? "✓" : "?"}</div>`
      ).join("");
    }
    if (m.decorations) {
      const el = document.getElementById("decor-list");
      if (el) el.innerHTML = m.decorations.catalog.map((d) =>
        `<li>${d.placed ? "✅" : "⬜"} ${d.name}</li>`
      ).join("");
    }
    if (m.forest) {
      const el = document.getElementById("forest-status");
      if (el) el.textContent = `🌲 ${m.forest.activeTrees}/${m.forest.totalTrees} 可砍 · 今日 ${m.forest.choppedToday}`;
      const fl = document.getElementById("forest-nearby");
      if (fl) fl.innerHTML = (m.forest.nearbyTrees || []).slice(0, 6).map((t) =>
        `<li>${t.active ? "🌳" : "🪵"} ${t.id} hp${t.hp}${t.regrowInDays != null ? ` · ${t.regrowInDays}天再生` : ""}</li>`
      ).join("") || "<li>附近无树</li>";
    }
    if (m.construction) {
      const el = document.getElementById("construction-status");
      if (el) el.textContent = `已建造 ${m.construction.builtCount} 格`;
    }
    if (m.orders?.crossLineOrders) {
      const el = document.getElementById("cross-order-list");
      if (el) el.innerHTML = m.orders.crossLineOrders.filter(o => !o.completed).map((o) => {
        const items = Object.entries(o.required).map(([k, n]) => `${ITEM_EMOJI[k] || ""}${n}`).join(" ");
        return `<li><b>[${o.type}]</b> ${items} → ${o.goldReward}g</li>`;
      }).join("") || "<li class='dim'>无跨线订单</li>";
    }
    if (m.merchants?.length) {
      const tabs = document.getElementById("merchant-tabs");
      if (tabs) {
        tabs.innerHTML = m.merchants.map((mer, i) =>
          `<button class="mer-tab ${i === activeMerchantIdx ? "active" : ""}" data-idx="${i}">${mer.name}</button>`
        ).join("");
        tabs.querySelectorAll(".mer-tab").forEach((btn) => {
          btn.onclick = () => { activeMerchantIdx = +btn.dataset.idx; renderMerchantPrices(m.merchants); };
        });
        renderMerchantPrices(m.merchants);
      }
    }
    if (m.upkeep) {
      const el = document.getElementById("economy-bar");
      if (el) el.innerHTML = (el.innerHTML || "") +
        `<span>🔧矿${m.upkeep.mineIntegrity}%</span><span>🏠${m.upkeep.buildingDurability}%</span>`;
    }
  } catch (_) {}
}

function renderMerchantPrices(merchants) {
  const mer = merchants[activeMerchantIdx];
  if (!mer) return;
  document.getElementById("price-list").innerHTML = Object.entries(mer.prices || {}).slice(0, 10).map(([k, v]) =>
    `<li>${ITEM_EMOJI[k] || ""} ${v}g</li>`
  ).join("");
}

async function loadSnapshot() {
  const res = await fetch("/api/snapshot");
  const { data: d } = await res.json();
  state = { ...state, ...d, merchant: d.merchant || state.merchant, well: d.well || state.well };
  displayPlayer = { x: d.player.x, y: d.player.y, facing: d.player.facing || "down" };
  displayGold = d.bag?.gold || 0;
  moveQueue = [];
  syncLandAnims(d.lands || []);
  updateHud();
}

function updateHud() {
  const goldEl = document.getElementById("gold");
  if (goldEl && state.bag.gold !== displayGold) {
    goldEl.classList.add("bump");
    setTimeout(() => goldEl.classList.remove("bump"), 300);
    displayGold = state.bag.gold;
  }
  goldEl.textContent = state.bag.gold;
  document.getElementById("game-time").textContent = `${String(state.gameHour).padStart(2, "0")}:00`;

  document.getElementById("bag-grid").innerHTML = state.bag.items.map((i) =>
    `<div class="item-cell pop-in"><span class="emoji">${ITEM_EMOJI[i.itemId] || "📦"}</span><span>${i.count}</span></div>`
  ).join("") || '<div class="item-cell">空</div>';

  document.getElementById("order-list").innerHTML = (state.farmOrder?.orders || []).map((o) =>
    `<li>${ITEM_EMOJI[o.cropId] || ""} ${o.delivered}/${o.required} <b>+${o.reward}g</b></li>`
  ).join("") || "<li>暂无订单</li>";

  document.getElementById("price-list").innerHTML = Object.entries(state.merchantPrices || {}).slice(0, 12).map(([k, v]) =>
    `<li>${ITEM_EMOJI[k] || ""} ${v}g</li>`
  ).join("");
}

canvas.addEventListener("mousemove", (e) => {
  const rect = canvas.getBoundingClientRect();
  const scaleX = canvas.width / rect.width;
  const scaleY = canvas.height / rect.height;
  const wx = (e.clientX - rect.left) * scaleX + camera.x;
  const wy = (e.clientY - rect.top) * scaleY + camera.y;
  hoverTile = { tx: Math.floor(wx / TILE), ty: Math.floor(wy / TILE) };
});

function drawTile(tx, ty, type) {
  const x = tx * TILE - camera.x;
  const y = ty * TILE - camera.y;
  if (x < -TILE || y < -TILE || x > VIEW_W + TILE || y > VIEW_H + TILE) return;
  const hash = (tx * 7 + ty * 13) % 4;
  switch (type) {
    case "grass":
      ctx.fillStyle = hash % 2 ? "#4a8f3a" : "#3d7a32";
      ctx.fillRect(x, y, TILE, TILE);
      break;
    case "path":
      ctx.fillStyle = "#c4a574"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#b8956a"; ctx.fillRect(x, y + TILE - 4, TILE, 4);
      break;
    case "soil":
      ctx.fillStyle = "#6b4428"; ctx.fillRect(x + 1, y + 1, TILE - 2, TILE - 2);
      break;
    case "water":
      ctx.fillStyle = "#3a8fc4"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = `rgba(255,255,255,${0.1 + Math.sin(animFrame * 0.05 + tx) * 0.05})`;
      ctx.fillRect(x + 4 + (animFrame % 28), y + 8, 10, 2);
      break;
    case "fence":
      ctx.fillStyle = "#8b6914"; ctx.fillRect(x, y + 12, TILE, 8);
      break;
    case "house":
      ctx.fillStyle = "#a0522d"; ctx.fillRect(x + 2, y + 10, TILE - 4, TILE - 10);
      ctx.fillStyle = "#8b4513";
      ctx.beginPath(); ctx.moveTo(x, y + 12); ctx.lineTo(x + TILE / 2, y); ctx.lineTo(x + TILE, y + 12); ctx.fill();
      break;
    case "tree": {
      ctx.fillStyle = "#4a8f3a"; ctx.fillRect(x, y, TILE, TILE);
      const sway = Math.sin(animFrame * 0.04 + tx) * 2;
      ctx.fillStyle = "#2d5a1e"; ctx.fillRect(x + 13 + sway * 0.2, y + 18, 6, 12);
      ctx.fillStyle = "#3d8f32";
      ctx.beginPath(); ctx.arc(x + 16 + sway, y + 12, 12, 0, Math.PI * 2); ctx.fill();
      break;
    }
    case "stump":
      ctx.fillStyle = "#4a8f3a"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#5c4033"; ctx.beginPath();
      ctx.ellipse(x + 16, y + 22, 10, 5, 0, 0, Math.PI * 2); ctx.fill();
      ctx.fillStyle = "#6b4428"; ctx.fillRect(x + 14, y + 18, 4, 6);
      break;
    case "wood_fence":
      ctx.fillStyle = "#4a8f3a"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#8b6914"; ctx.fillRect(x, y + 12, TILE, 8);
      ctx.fillStyle = "#6b4428"; ctx.fillRect(x + 4, y + 8, 4, 16);
      ctx.fillRect(x + TILE - 8, y + 8, 4, 16);
      break;
    case "wood_path":
      ctx.fillStyle = "#a08050"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#8b6914"; ctx.fillRect(x + 2, y + 2, TILE - 4, TILE - 4);
      break;
    case "lumber_platform":
      ctx.fillStyle = "#4a8f3a"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#8b6914"; ctx.fillRect(x + 2, y + 20, TILE - 4, 10);
      ctx.fillStyle = "#6b4428"; ctx.fillRect(x + 4, y + 14, TILE - 8, 6);
      break;
    case "flower":
      ctx.fillStyle = "#4a8f3a"; ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = ["#e74c6f", "#f4c542", "#9b59b6"][hash % 3];
      ctx.beginPath(); ctx.arc(x + 16, y + 16, 5, 0, Math.PI * 2); ctx.fill();
      break;
    case "bridge":
      ctx.fillStyle = "#3a8fc4"; ctx.fillRect(x, y + 20, TILE, 12);
      ctx.fillStyle = "#8b6914"; ctx.fillRect(x + 2, y + 18, TILE - 4, 4);
      break;
    default:
      ctx.fillStyle = "#3d6b2e"; ctx.fillRect(x, y, TILE, TILE);
  }
}

function drawLand(land) {
  const tx = Math.floor(land.x / TILE);
  const ty = Math.floor(land.y / TILE);
  const x = tx * TILE - camera.x;
  const y = ty * TILE - camera.y;
  const anim = landAnims[land.id] || { growth: land.growth, targetGrowth: land.growth };
  anim.growth += (anim.targetGrowth - anim.growth) * 0.08;
  if (anim.flash > 0) anim.flash--;

  if (land.needsWater || land.isDry) {
    ctx.fillStyle = "rgba(74, 144, 217, 0.35)"; ctx.fillRect(x, y, TILE, TILE);
  }
  if (land.hasPest) { ctx.fillStyle = "rgba(180, 60, 60, 0.3)"; ctx.fillRect(x, y, TILE, TILE); }
  if (land.hasFrost) { ctx.fillStyle = "rgba(200, 230, 255, 0.35)"; ctx.fillRect(x, y, TILE, TILE); }
  if (land.isGreenhouse) {
    ctx.strokeStyle = "rgba(143, 212, 100, 0.5)"; ctx.lineWidth = 2;
    ctx.strokeRect(x + 1, y + 1, TILE - 2, TILE - 2);
  }

  const crop = land.cropId;
  if (crop && anim.growth > 0.02 && land.state !== "empty") {
    const col = CROP_COLORS[crop] || "#4aaf3a";
    const h = 6 + anim.growth * 14;
    ctx.fillStyle = "#2d5016"; ctx.fillRect(x + 14, y + TILE - 8, 4, 8);
    ctx.fillStyle = col;
    ctx.beginPath(); ctx.arc(x + 16, y + TILE - 8 - h, 4 + anim.growth * 6, 0, Math.PI * 2); ctx.fill();
  }

  if (land.canHarvest) {
    const pulse = 0.5 + Math.sin(animFrame * 0.1) * 0.5;
    ctx.strokeStyle = `rgba(244, 197, 66, ${0.4 + pulse * 0.6})`;
    ctx.lineWidth = 2; ctx.strokeRect(x + 2, y + 2, TILE - 4, TILE - 4);
  }
  if (anim.flash > 0) {
    ctx.fillStyle = `rgba(255,255,255,${anim.flash / 20})`;
    ctx.fillRect(x, y, TILE, TILE);
  }
  if (hoverTile && hoverTile.tx === tx && hoverTile.ty === ty) {
    ctx.strokeStyle = "rgba(255,255,255,0.45)"; ctx.lineWidth = 2;
    ctx.strokeRect(x + 1, y + 1, TILE - 2, TILE - 2);
  }
}

function drawPOI(poi) {
  const x = poi.tx * TILE + TILE / 2 - camera.x;
  const y = poi.ty * TILE + TILE / 2 - camera.y;
  if (x < -40 || y < -40 || x > VIEW_W + 40 || y > VIEW_H + 40) return;
  const bob = Math.sin(animFrame * 0.06 + poi.tx) * 3;
  ctx.font = "16px sans-serif"; ctx.textAlign = "center";
  ctx.globalAlpha = 0.85 + Math.sin(animFrame * 0.08) * 0.15;
  ctx.fillText(poi.emoji, x, y + bob);
  ctx.font = "9px sans-serif"; ctx.fillStyle = "rgba(232,244,220,0.7)";
  ctx.fillText(poi.label, x, y + bob + 14);
  ctx.fillStyle = "#e8f4dc"; ctx.textAlign = "left"; ctx.globalAlpha = 1;
}

function drawCharacter(px, py, facing, isPlayer) {
  const x = px - camera.x;
  const y = py - camera.y;
  const moving = moveQueue.length > 0 || Math.hypot(state.player.x - displayPlayer.x, state.player.y) > 2;
  const bob = Math.sin(animFrame * (moving ? 0.18 : 0.06)) * (moving ? 2 : 0.4);
  const act = currentActivity;
  const actProg = act ? Math.min(1, (performance.now() - act.start) / act.duration) : 0;

  ctx.fillStyle = "rgba(0,0,0,0.25)";
  ctx.beginPath(); ctx.ellipse(x, y + 10, 10, 4, 0, 0, Math.PI * 2); ctx.fill();

  ctx.fillStyle = isPlayer ? "#4a90d9" : "#c9a227";
  ctx.fillRect(x - 9, y - 10 + bob, 18, 20);
  ctx.fillStyle = isPlayer ? "#3a7ab8" : "#a08020";
  ctx.fillRect(x - 9, y + 6 + bob, 18, 4);
  ctx.fillStyle = "#f4c2a1"; ctx.fillRect(x - 7, y - 18 + bob, 14, 10);
  ctx.fillStyle = "#2c1810"; ctx.fillRect(x - 6, y - 16 + bob, 12, 4);

  if (act && isPlayer) drawActivityTool(x, y + bob, act.kind, actProg, facing);
}

function drawActivityTool(x, y, kind, prog, facing) {
  ctx.save();
  ctx.translate(x, y);
  const swing = Math.sin(prog * Math.PI) * 0.8;
  switch (kind) {
    case "mine":
    case "forge":
      ctx.rotate(facing === "left" ? -swing : swing);
      ctx.font = "18px sans-serif"; ctx.fillText("⛏️", facing === "left" ? -22 : 12, -4);
      break;
    case "fish": {
      ctx.font = "16px sans-serif";
      const lineLen = 8 + prog * 18;
      ctx.strokeStyle = "#ccc"; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(8, 0); ctx.lineTo(8, lineLen); ctx.stroke();
      ctx.fillText("🎣", 0, -6);
      if (prog > 0.5) { ctx.fillText("💦", 8, lineLen + 4); }
      break;
    }
    case "harvest":
    case "chop":
    case "build":
      ctx.rotate(swing);
      ctx.font = "16px sans-serif";
      ctx.fillText(kind === "chop" ? "🪓" : kind === "build" ? "🔨" : "🔪", 14, 0);
      break;
    case "water":
      ctx.globalAlpha = prog;
      ctx.font = "14px sans-serif"; ctx.fillText("💧", 10 + prog * 6, -8 - prog * 4);
      break;
    case "plant":
      ctx.globalAlpha = prog;
      ctx.font = "14px sans-serif"; ctx.fillText("🌱", 8, -6 - prog * 8);
      break;
    case "process":
      ctx.font = "14px sans-serif"; ctx.fillText("⚙️", 12, -8 + Math.sin(prog * 12) * 3);
      break;
  }
  ctx.restore();
}

function drawActivityRing() {
  if (!currentActivity) return;
  const prog = Math.min(1, (performance.now() - currentActivity.start) / currentActivity.duration);
  const x = displayPlayer.x - camera.x;
  const y = displayPlayer.y - camera.y - 28;
  ctx.strokeStyle = "rgba(143, 212, 100, 0.8)"; ctx.lineWidth = 3;
  ctx.beginPath(); ctx.arc(x, y, 14, -Math.PI / 2, -Math.PI / 2 + prog * Math.PI * 2); ctx.stroke();
  if (prog >= 1) {
    currentActivity = null;
    if (activityLabel) { activityLabel.textContent = "👁️ Agent 观战中"; activityLabel.classList.remove("active"); }
  }
}

function drawMerchant() {
  drawCharacter(state.merchant.x, state.merchant.y, "down", false);
  const x = state.merchant.x - camera.x;
  const y = state.merchant.y - camera.y - 28;
  ctx.fillStyle = "rgba(244, 197, 66, 0.9)"; ctx.font = "bold 11px sans-serif"; ctx.textAlign = "center";
  ctx.fillText("🏪 作物商", x, y);
  if (metaCache?.merchants) {
    for (const mer of metaCache.merchants) {
      if (mer.id === state.merchant?.id) continue;
      drawCharacter(mer.x, mer.y, "down", false);
      const mx = mer.x - camera.x, my = mer.y - camera.y - 28;
      ctx.fillText(mer.category === "ore" ? "💎 矿石" : "🐟 渔贩", mx, my);
    }
  }
  ctx.textAlign = "left";
}

function drawSceneOverlay() {
  if (!metaCache) return;
  if (metaCache.mine?.inMine) {
    ctx.fillStyle = "rgba(0,0,0,0.55)";
    ctx.fillRect(0, 0, VIEW_W, VIEW_H);
    ctx.fillStyle = "#8a8a8a";
    for (let i = 0; i < 40; i++) {
      const rx = (i * 97 + animFrame) % VIEW_W;
      const ry = (i * 53) % VIEW_H;
      ctx.fillRect(rx, ry, 8 + (i % 3) * 4, 6);
    }
    ctx.fillStyle = "#e8f4dc"; ctx.font = "bold 20px sans-serif"; ctx.textAlign = "center";
    ctx.fillText(`🕳️ 矿洞 L${metaCache.mine.layer}`, VIEW_W / 2, 36);
    if (metaCache.boss?.active) {
      ctx.fillStyle = "#e86c4a";
      ctx.fillText(`👹 Boss ${metaCache.boss.bossHp}/${metaCache.boss.maxHp}`, VIEW_W / 2, 62);
    }
    ctx.textAlign = "left";
  }
  const nearWater = metaCache.fishPonds?.some((p) =>
    Math.hypot(displayPlayer.x - p.x, displayPlayer.y - p.y) < TILE * 4);
  if (nearWater && !metaCache.mine?.inMine) {
    ctx.strokeStyle = "rgba(94, 184, 232, 0.35)"; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(displayPlayer.x - camera.x, displayPlayer.y - camera.y, 48 + Math.sin(animFrame * 0.08) * 6, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = "rgba(94, 184, 232, 0.15)"; ctx.font = "12px sans-serif"; ctx.textAlign = "center";
    ctx.fillText("🎣 钓点", displayPlayer.x - camera.x, displayPlayer.y - camera.y - 56);
    ctx.textAlign = "left";
  }
}

function drawWell() {
  const wx = state.well?.x ?? 1104, wy = state.well?.y ?? 400;
  const x = wx - camera.x - 12, y = wy - camera.y - 12;
  ctx.fillStyle = "#708090"; ctx.fillRect(x, y, 24, 20);
  ctx.fillStyle = "#4a90d9"; ctx.beginPath(); ctx.arc(x + 12, y + 8, 8, 0, Math.PI * 2); ctx.fill();
}

function drawMinimap() {
  if (!state.tiles.length) return;
  const mw = state.mapWidth, mh = state.mapHeight;
  const sx = 200 / mw, sy = 150 / mh;
  mctx.fillStyle = "#1a3018"; mctx.fillRect(0, 0, 200, 150);
  for (let y = 0; y < mh; y++) {
    for (let x = 0; x < mw; x++) {
      const t = state.tiles[y][x];
      mctx.fillStyle = t === "water" ? "#3a8fc4" : t === "soil" ? "#6b4428" : t === "path" ? "#c4a574" : t === "tree" ? "#2d5a1e" : "#4a8f3a";
      mctx.fillRect(x * sx, y * sy, sx + 0.5, sy + 0.5);
    }
  }
  for (const poi of POIS) {
    mctx.fillStyle = "#f4c542";
    mctx.fillRect(poi.tx * sx - 1, poi.ty * sy - 1, 3, 3);
  }
  const px = displayPlayer.x / TILE * sx, py = displayPlayer.y / TILE * sy;
  mctx.fillStyle = "#4a90d9"; mctx.fillRect(px - 2, py - 2, 4, 4);
  const vx = camera.x / TILE * sx, vy = camera.y / TILE * sy;
  const vw = VIEW_W / TILE * sx, vh = VIEW_H / TILE * sy;
  mctx.strokeStyle = "rgba(143,212,100,0.8)"; mctx.lineWidth = 1;
  mctx.strokeRect(vx, vy, vw, vh);
}

function updateMovement(dt) {
  if (moveQueue.length > 0) {
    const target = moveQueue[0];
    const dx = target.x - displayPlayer.x, dy = target.y - displayPlayer.y;
    const dist = Math.hypot(dx, dy);
    const step = MOVE_SPEED * dt / 1000;
    if (dist <= step) {
      displayPlayer.x = target.x; displayPlayer.y = target.y;
      displayPlayer.facing = target.facing || displayPlayer.facing;
      moveQueue.shift();
      if (moveQueue.length === 0) spawnParticles(displayPlayer.x, displayPlayer.y, "#8fd464", 3);
    } else {
      displayPlayer.x += (dx / dist) * step;
      displayPlayer.y += (dy / dist) * step;
      displayPlayer.facing = target.facing || displayPlayer.facing;
    }
  }
  const mapW = state.mapWidth * TILE, mapH = state.mapHeight * TILE;
  cameraTarget.x = Math.max(0, Math.min(mapW - VIEW_W, displayPlayer.x - VIEW_W / 2));
  cameraTarget.y = Math.max(0, Math.min(mapH - VIEW_H, displayPlayer.y - VIEW_H / 2));
  camera.x += (cameraTarget.x - camera.x) * CAMERA_LERP;
  camera.y += (cameraTarget.y - camera.y) * CAMERA_LERP;
}

function draw() {
  animFrame++;
  ctx.clearRect(0, 0, VIEW_W, VIEW_H);

  const startTx = Math.floor(camera.x / TILE), startTy = Math.floor(camera.y / TILE);
  const endTx = Math.ceil((camera.x + VIEW_W) / TILE), endTy = Math.ceil((camera.y + VIEW_H) / TILE);

  for (let ty = startTy; ty <= endTy; ty++)
    for (let tx = startTx; tx <= endTx; tx++)
      drawTile(tx, ty, state.tiles[ty]?.[tx] || "grass");

  for (const land of state.lands) drawLand(land);
  for (const poi of POIS) drawPOI(poi);
  drawWell(); drawMerchant(); drawSceneOverlay();
  drawCharacter(displayPlayer.x, displayPlayer.y, displayPlayer.facing, true);
  drawActivityRing();

  for (const p of particles) {
    p.x += p.vx; p.y += p.vy; p.vy += 0.1; p.life--;
    ctx.fillStyle = p.color; ctx.globalAlpha = Math.max(0, p.life / 40);
    ctx.fillRect(p.x - camera.x, p.y - camera.y, p.size || 3, p.size || 3);
  }
  particles = particles.filter((p) => p.life > 0);

  for (const ft of floatTexts) {
    ft.y += ft.vy; ft.life--;
    ctx.fillStyle = ft.color; ctx.globalAlpha = Math.max(0, ft.life / 60);
    ctx.font = "bold 12px sans-serif"; ctx.textAlign = "center";
    ctx.fillText(ft.text, ft.x - camera.x, ft.y - camera.y);
  }
  ctx.globalAlpha = 1; ctx.textAlign = "left";
  floatTexts = floatTexts.filter((f) => f.life > 0);

  drawMinimap();
}

function loop(ts) {
  if (!gameStarted) return;
  const dt = lastFrameTs ? Math.min(50, ts - lastFrameTs) : 16;
  lastFrameTs = ts;
  updateMovement(dt);
  draw();
  requestAnimationFrame(loop);
}

let gameStarted = false;

function formatSaveTime(ts) {
  if (!ts) return "";
  return new Date(ts).toLocaleString("zh-CN", { month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

async function pollActionState() {
  if (!gameStarted) return;
  try {
    const res = await fetch("/agent/state/action");
    const { data: a } = await res.json();
    const label = document.getElementById("activity-label");
    const hintEl = document.getElementById("next-hint");
    if (!label) return;
    if (a.busy) {
      label.textContent = `⚙️ 执行中：${a.currentAction || "…"}`;
      label.classList.add("active");
      if (hintEl) hintEl.textContent = "⏳ 上一个操作还未结束，请等待…";
    } else if (a.nextHint) {
      label.textContent = "✅ 操作完毕";
      label.classList.remove("active");
      if (hintEl) hintEl.textContent = `💡 下一步：${a.nextHint}`;
    } else {
      label.textContent = "👁️ Agent 观战中";
      label.classList.remove("active");
      if (hintEl) hintEl.textContent = "";
    }
  } catch (_) {}
}

async function initTitleScreen() {
  const btnNew = document.getElementById("btn-new-game");
  const btnContinue = document.getElementById("btn-continue");
  const saveInfo = document.getElementById("save-info");
  try {
    const res = await fetch("/api/save/info");
    const { data: info } = await res.json();
    if (info.hasSave) {
      btnContinue.disabled = false;
      saveInfo.textContent = `存档：💰${info.gold} · 第${info.gameDay}天 · ${info.season} · ${formatSaveTime(info.savedAt)}`;
    } else saveInfo.textContent = "暂无存档";
  } catch { saveInfo.textContent = "无法读取存档信息"; }
  btnNew.onclick = () => startSession("new");
  btnContinue.onclick = () => startSession("load");
}

async function startSession(mode) {
  const btnNew = document.getElementById("btn-new-game");
  const btnContinue = document.getElementById("btn-continue");
  btnNew.disabled = btnContinue.disabled = true;
  try {
    const path = mode === "load" ? "/api/game/load" : "/api/game/new";
    const res = await fetch(path, { method: "POST" });
    const j = await res.json();
    if (!j.success) {
      showToast(j.message || "启动失败", false);
      btnNew.disabled = btnContinue.disabled = false;
      return;
    }
    document.getElementById("title-screen").classList.add("hidden");
    document.getElementById("game-root").classList.remove("hidden");
    gameStarted = true;
    await loadSnapshot();
    await loadMeta();
    connectWs();
    setInterval(loadMeta, 8000);
    setInterval(pollActionState, 800);
    requestAnimationFrame(loop);
  } catch {
    showToast("无法连接服务器", false);
    btnNew.disabled = btnContinue.disabled = false;
  }
}

initTitleScreen();
connectViewWs();
