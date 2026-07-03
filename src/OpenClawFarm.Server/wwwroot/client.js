const TILE = 32;
const VIEW_W = 960;
const VIEW_H = 640;

const ITEM_EMOJI = {
  seed_strawberry: "🍓", seed_wheat: "🌾", seed_carrot: "🥕", seed_corn: "🌽", seed_pumpkin: "🎃",
  crop_strawberry: "🍓", crop_wheat: "🌾", crop_carrot: "🥕", crop_corn: "🌽", crop_pumpkin: "🎃",
  tool_watering_can: "🚿", tool_hoe: "⛏️", tool_sickle: "🔪",
};

const HOTBAR = [
  { id: "tool_watering_can", label: "洒水" },
  { id: "seed_strawberry", label: "草莓" },
  { id: "seed_wheat", label: "小麦" },
  { id: "seed_carrot", label: "胡萝卜" },
  { id: "seed_corn", label: "玉米" },
  { id: "seed_pumpkin", label: "南瓜" },
  { id: "tool_sickle", label: "收割" },
  { id: "tool_hoe", label: "锄头" },
];

const CROP_COLORS = {
  crop_strawberry: "#e74c6f",
  crop_wheat: "#d4a82a",
  crop_carrot: "#e67e22",
  crop_corn: "#f1c40f",
  crop_pumpkin: "#e8963a",
};

const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");
const minimap = document.getElementById("minimap");
const mctx = minimap.getContext("2d");

let state = {
  player: { x: 240, y: 288, facing: "down" },
  lands: [],
  bag: { gold: 0, items: [] },
  merchant: { x: 976, y: 496 },
  well: { x: 1104, y: 400 },
  merchantPrices: {},
  farmOrder: { orders: [] },
  manualMode: true,
  mapWidth: 40,
  mapHeight: 30,
  tiles: [],
  gameHour: 8,
  selectedSlot: 0,
};

let displayPlayer = { x: 240, y: 288, facing: "down" };
let camera = { x: 0, y: 0 };
const keys = new Set();
let lastMoveAt = 0;
let moveInFlight = false;
let interactInFlight = false;
let lastMoveKey = null;
const MOVE_COOLDOWN_MS = 70;
let animFrame = 0;
let particles = [];
let hoverTile = null;
let clickPath = [];

const toastEl = document.getElementById("toast");
let toastTimer;

function showToast(msg, ok = true) {
  toastEl.textContent = msg;
  toastEl.style.borderColor = ok ? "#5a8f42" : "#e86c4a";
  toastEl.style.color = ok ? "#8fd464" : "#e86c4a";
  toastEl.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toastEl.classList.remove("show"), 2200);
}

function isWalkable(tx, ty) {
  if (tx < 0 || ty < 0 || tx >= state.mapWidth || ty >= state.mapHeight) return false;
  const t = state.tiles[ty]?.[tx];
  return t === "grass" || t === "path" || t === "bridge" || t === "soil";
}

function findPath(sx, sy, ex, ey) {
  const key = (x, y) => `${x},${y}`;
  const open = [{ x: sx, y: sy, g: 0, f: Math.abs(ex - sx) + Math.abs(ey - sy) }];
  const came = {};
  const gScore = { [key(sx, sy)]: 0 };
  const dirs = [[0, 1], [0, -1], [1, 0], [-1, 0]];

  while (open.length) {
    open.sort((a, b) => a.f - b.f);
    const cur = open.shift();
    if (cur.x === ex && cur.y === ey) {
      const path = [];
      let k = key(cur.x, cur.y);
      while (k) {
        const [px, py] = k.split(",").map(Number);
        path.unshift({ x: px, y: py });
        k = came[k];
      }
      return path.slice(1);
    }
    for (const [dx, dy] of dirs) {
      const nx = cur.x + dx, ny = cur.y + dy;
      if (!isWalkable(nx, ny)) continue;
      const nk = key(nx, ny);
      const tg = cur.g + 1;
      if (tg < (gScore[nk] ?? Infinity)) {
        came[nk] = key(cur.x, cur.y);
        gScore[nk] = tg;
        open.push({ x: nx, y: ny, g: tg, f: tg + Math.abs(ex - nx) + Math.abs(ey - ny) });
      }
    }
  }
  return [];
}

function screenToWorld(sx, sy) {
  const rect = canvas.getBoundingClientRect();
  const scaleX = canvas.width / rect.width;
  const scaleY = canvas.height / rect.height;
  return {
    x: sx * scaleX + camera.x,
    y: sy * scaleY + camera.y,
  };
}

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

function applyPatch(msg) {
  // 手动模式下玩家位置只由 doMove / loadSnapshot 更新，避免 WS 回弹
  if (msg.player && !state.manualMode) {
    state.player = msg.player;
    displayPlayer.x = msg.player.x;
    displayPlayer.y = msg.player.y;
    displayPlayer.facing = msg.player.facing || displayPlayer.facing;
  }
  if (msg.lands) state.lands = msg.lands;
  if (msg.bag) state.bag = msg.bag;
  if (msg.manualMode !== undefined) {
    state.manualMode = msg.manualMode;
    document.getElementById("manual-mode").checked = msg.manualMode;
    document.getElementById("mode-label").textContent = msg.manualMode ? "手动模式" : "Agent 模式";
  }
  if (msg.selectedSlot !== undefined) state.selectedSlot = msg.selectedSlot;
  if (msg.gameHour !== undefined) state.gameHour = msg.gameHour;
  updateHud();
  renderHotbar();
}

async function loadSnapshot() {
  const res = await fetch("/api/snapshot");
  const { data: d } = await res.json();
  state = { ...state, ...d };
  displayPlayer.x = d.player.x;
  displayPlayer.y = d.player.y;
  displayPlayer.facing = d.player.facing || "down";
  document.getElementById("manual-mode").checked = state.manualMode;
  document.getElementById("mode-label").textContent = state.manualMode ? "手动模式" : "Agent 模式";
  updateHud();
  renderHotbar();
}

function updateHud() {
  document.getElementById("gold").textContent = state.bag.gold;
  const h = String(state.gameHour).padStart(2, "0");
  document.getElementById("game-time").textContent = `${h}:00`;

  const bagGrid = document.getElementById("bag-grid");
  bagGrid.innerHTML = state.bag.items.map((i) =>
    `<div class="item-cell"><span class="emoji">${ITEM_EMOJI[i.itemId] || "📦"}</span><span>${i.count}</span></div>`
  ).join("") || '<div class="item-cell">空</div>';

  document.getElementById("order-list").innerHTML = (state.farmOrder?.orders || []).map((o) =>
    `<li>${ITEM_EMOJI[o.cropId] || ""} ${o.delivered}/${o.required} <b>+${o.reward}g</b></li>`
  ).join("") || "<li>暂无订单</li>";

  document.getElementById("price-list").innerHTML = Object.entries(state.merchantPrices || {}).map(([k, v]) =>
    `<li>${ITEM_EMOJI[k] || ""} ${v}g</li>`
  ).join("");
}

function renderHotbar() {
  const el = document.getElementById("hotbar-slots");
  el.innerHTML = HOTBAR.map((slot, i) =>
    `<div class="hotbar-slot ${i === state.selectedSlot ? "selected" : ""}" data-slot="${i}">
      <span class="key">${i + 1}</span>
      <span class="emoji">${ITEM_EMOJI[slot.id] || "❓"}</span>
      <span class="label">${slot.label}</span>
    </div>`
  ).join("");
  el.querySelectorAll(".hotbar-slot").forEach((node) => {
    node.onclick = () => selectSlot(+node.dataset.slot);
  });
}

async function selectSlot(slot) {
  state.selectedSlot = slot;
  renderHotbar();
  await fetch("/api/select_slot", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ slot }),
  });
}

function isMovementKey(k) {
  return ["w", "a", "s", "d", "arrowup", "arrowdown", "arrowleft", "arrowright"].includes(k);
}

function setupInput() {
  const onKeyDown = (e) => {
    if (e.target.closest('input[type="text"], input[type="number"], textarea, select')) return;
    const k = e.key.toLowerCase();
    if (isMovementKey(k)) {
      e.preventDefault();
      lastMoveKey = k;
      clickPath = [];
      keys.add(k);
    }
    if (k === "e") {
      e.preventDefault();
      void doInteract();
    }
    const num = parseInt(k, 10);
    if (num >= 1 && num <= 8) {
      e.preventDefault();
      void selectSlot(num - 1);
    }
  };

  const onKeyUp = (e) => {
    const k = e.key.toLowerCase();
    if (!isMovementKey(k) && k !== "e") return;
    keys.delete(k);
    if (k === lastMoveKey) {
      lastMoveKey = [...keys].find(isMovementKey) ?? null;
    }
  };

  window.addEventListener("keydown", onKeyDown, true);
  window.addEventListener("keyup", onKeyUp, true);

  document.getElementById("game-root").addEventListener("mousedown", () => canvas.focus());
  canvas.addEventListener("click", () => canvas.focus());

  setInterval(tickInput, 30);
}

document.getElementById("manual-mode").addEventListener("change", async (e) => {
  const enabled = e.target.checked;
  state.manualMode = enabled;
  document.getElementById("mode-label").textContent = enabled ? "手动模式" : "Agent 模式";
  if (!enabled) keys.clear();
  await fetch("/api/manual_mode", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ enabled }),
  });
});

canvas.addEventListener("mousemove", (e) => {
  const rect = canvas.getBoundingClientRect();
  const w = screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
  hoverTile = { tx: Math.floor(w.x / TILE), ty: Math.floor(w.y / TILE) };
});

canvas.addEventListener("click", (e) => {
  if (!state.manualMode) return;
  canvas.focus();
  const rect = canvas.getBoundingClientRect();
  const w = screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
  const tx = Math.floor(w.x / TILE);
  const ty = Math.floor(w.y / TILE);

  const land = state.lands.find((l) =>
    Math.floor(l.x / TILE) === tx && Math.floor(l.y / TILE) === ty);
  if (land && dist(state.player.x, state.player.y, land.x, land.y) < TILE * 1.8) {
    void doInteract(land.id);
    return;
  }

  const ptx = Math.floor(state.player.x / TILE);
  const pty = Math.floor(state.player.y / TILE);
  if (ptx === tx && pty === ty) return;
  clickPath = findPath(ptx, pty, tx, ty);
});

async function doMove(dx, dy) {
  try {
    const res = await fetch("/api/manual_move", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ dx, dy }),
    });
    if (!res.ok) throw new Error("move failed");
    const j = await res.json();
    if (j.success && j.player) {
      state.player = j.player;
      displayPlayer.x = j.player.x;
      displayPlayer.y = j.player.y;
      displayPlayer.facing = j.player.facing || displayPlayer.facing;
      return true;
    }
    return false;
  } catch {
    showToast("服务器已断开，请重新 F5 启动", false);
    return false;
  }
}

async function doInteract(landId) {
  if (!state.manualMode || interactInFlight) return;
  interactInFlight = true;
  try {
    const res = await fetch("/api/manual_interact", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(landId ? { landId } : {}),
    });
    const j = await res.json();
    showToast(j.message || (j.success ? "完成" : "失败"), j.success);
    if (j.success && j.message?.includes("收获"))
      spawnParticles(displayPlayer.x, displayPlayer.y, "#f4c542");
    if (j.success) await loadSnapshot();
  } finally {
    interactInFlight = false;
  }
}

function dist(x1, y1, x2, y2) {
  return Math.hypot(x1 - x2, y1 - y2);
}

function spawnParticles(x, y, color) {
  for (let i = 0; i < 12; i++) {
    particles.push({
      x, y,
      vx: (Math.random() - 0.5) * 4,
      vy: (Math.random() - 0.5) * 4 - 2,
      life: 30 + Math.random() * 20,
      color,
    });
  }
}

function drawTile(tx, ty, type) {
  const x = tx * TILE - camera.x;
  const y = ty * TILE - camera.y;
  if (x < -TILE || y < -TILE || x > VIEW_W + TILE || y > VIEW_H + TILE) return;

  const hash = (tx * 7 + ty * 13) % 4;
  switch (type) {
    case "grass":
      ctx.fillStyle = hash % 2 ? "#4a8f3a" : "#3d7a32";
      ctx.fillRect(x, y, TILE, TILE);
      if (hash === 0) { ctx.fillStyle = "#5aa048"; ctx.fillRect(x + 8, y + 10, 3, 3); ctx.fillRect(x + 20, y + 18, 2, 2); }
      break;
    case "path":
      ctx.fillStyle = "#c4a574";
      ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#b8956a";
      ctx.fillRect(x, y + TILE - 4, TILE, 4);
      break;
    case "soil":
      ctx.fillStyle = "#6b4428";
      ctx.fillRect(x + 1, y + 1, TILE - 2, TILE - 2);
      ctx.fillStyle = "#5a3820";
      for (let i = 0; i < 3; i++) ctx.fillRect(x + 4 + i * 9, y + 6, 6, 2);
      break;
    case "water":
      ctx.fillStyle = "#3a8fc4";
      ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "rgba(255,255,255,0.2)";
      ctx.fillRect(x + 4 + (animFrame % 20), y + 8, 12, 2);
      break;
    case "fence":
      ctx.fillStyle = "#8b6914";
      ctx.fillRect(x, y + 12, TILE, 8);
      ctx.fillRect(x + 4, y + 4, 4, 24);
      ctx.fillRect(x + TILE - 8, y + 4, 4, 24);
      break;
    case "house":
      ctx.fillStyle = "#a0522d";
      ctx.fillRect(x + 2, y + 10, TILE - 4, TILE - 10);
      ctx.fillStyle = "#8b4513";
      ctx.beginPath();
      ctx.moveTo(x, y + 12);
      ctx.lineTo(x + TILE / 2, y);
      ctx.lineTo(x + TILE, y + 12);
      ctx.fill();
      ctx.fillStyle = "#f5d76e";
      ctx.fillRect(x + TILE / 2 - 4, y + 18, 8, 10);
      break;
    case "tree":
      ctx.fillStyle = hash % 2 ? "#4a8f3a" : "#3d7a32";
      ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = "#2d5a1e";
      ctx.fillRect(x + 13, y + 18, 6, 12);
      ctx.fillStyle = "#3d8f32";
      ctx.beginPath();
      ctx.arc(x + 16, y + 12, 12, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = "#4aaf3a";
      ctx.beginPath();
      ctx.arc(x + 12, y + 8, 8, 0, Math.PI * 2);
      ctx.fill();
      break;
    case "flower":
      ctx.fillStyle = "#4a8f3a";
      ctx.fillRect(x, y, TILE, TILE);
      ctx.fillStyle = ["#e74c6f", "#f4c542", "#9b59b6"][hash % 3];
      for (let i = 0; i < 4; i++) {
        ctx.beginPath();
        ctx.arc(x + 10 + (i % 2) * 8, y + 10 + Math.floor(i / 2) * 8, 4, 0, Math.PI * 2);
        ctx.fill();
      }
      break;
    case "bridge":
      ctx.fillStyle = "#3a8fc4";
      ctx.fillRect(x, y + 20, TILE, 12);
      ctx.fillStyle = "#8b6914";
      ctx.fillRect(x + 2, y + 18, TILE - 4, 4);
      break;
    default:
      ctx.fillStyle = "#3d6b2e";
      ctx.fillRect(x, y, TILE, TILE);
  }
}

function drawLand(land) {
  const tx = Math.floor(land.x / TILE);
  const ty = Math.floor(land.y / TILE);
  const x = tx * TILE - camera.x;
  const y = ty * TILE - camera.y;

  if (land.needsWater) {
    ctx.fillStyle = "rgba(74, 144, 217, 0.35)";
    ctx.fillRect(x, y, TILE, TILE);
  }

  const crop = land.cropId;
  if (crop && land.growth > 0 && land.state !== "empty") {
    const col = CROP_COLORS[crop] || "#4aaf3a";
    const h = 6 + land.growth * 14;
    ctx.fillStyle = "#2d5016";
    ctx.fillRect(x + 14, y + TILE - 8, 4, 8);
    ctx.fillStyle = col;
    ctx.beginPath();
    ctx.arc(x + 16, y + TILE - 8 - h, 4 + land.growth * 6, 0, Math.PI * 2);
    ctx.fill();
  }

  if (land.canHarvest) {
    ctx.strokeStyle = "#f4c542";
    ctx.lineWidth = 2;
    ctx.strokeRect(x + 2, y + 2, TILE - 4, TILE - 4);
    ctx.fillStyle = "#ff6b35";
    ctx.font = "14px sans-serif";
    ctx.fillText("✨", x + 10, y + 12);
  }

  if (hoverTile && hoverTile.tx === tx && hoverTile.ty === ty) {
    ctx.strokeStyle = "rgba(255,255,255,0.5)";
    ctx.lineWidth = 2;
    ctx.strokeRect(x + 1, y + 1, TILE - 2, TILE - 2);
  }
}

function drawCharacter(px, py, facing, isPlayer) {
  const x = px - camera.x;
  const y = py - camera.y;
  const bob = Math.sin(animFrame * 0.15) * (keys.size > 0 ? 2 : 0.5);

  ctx.fillStyle = "rgba(0,0,0,0.25)";
  ctx.beginPath();
  ctx.ellipse(x, y + 10, 10, 4, 0, 0, Math.PI * 2);
  ctx.fill();

  ctx.fillStyle = isPlayer ? "#4a90d9" : "#c9a227";
  ctx.fillRect(x - 9, y - 10 + bob, 18, 20);
  ctx.fillStyle = isPlayer ? "#3a7ab8" : "#a08020";
  ctx.fillRect(x - 9, y + 6 + bob, 18, 4);

  ctx.fillStyle = "#f4c2a1";
  ctx.fillRect(x - 7, y - 18 + bob, 14, 10);
  ctx.fillStyle = "#2c1810";
  ctx.fillRect(x - 6, y - 16 + bob, 12, 4);

  const tool = HOTBAR[state.selectedSlot];
  if (isPlayer && tool) {
    ctx.font = "14px sans-serif";
    const ox = facing === "left" ? -16 : facing === "right" ? 12 : 4;
    const oy = facing === "up" ? -20 : -8;
    ctx.fillText(ITEM_EMOJI[tool.id] || "", x + ox, y + oy + bob);
  }
}

function drawMerchant() {
  drawCharacter(state.merchant.x, state.merchant.y, "down", false);
  const x = state.merchant.x - camera.x;
  const y = state.merchant.y - camera.y - 28;
  ctx.fillStyle = "rgba(244, 197, 66, 0.9)";
  ctx.font = "bold 11px sans-serif";
  ctx.textAlign = "center";
  ctx.fillText("🏪 商人", x, y);
  ctx.textAlign = "left";
}

function drawWell() {
  const wx = state.well?.x ?? 1104;
  const wy = state.well?.y ?? 400;
  const x = wx - camera.x - 12;
  const y = wy - camera.y - 12;
  ctx.fillStyle = "#708090";
  ctx.fillRect(x, y, 24, 20);
  ctx.fillStyle = "#4a90d9";
  ctx.beginPath();
  ctx.arc(x + 12, y + 8, 8, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#9ab88a";
  ctx.font = "10px sans-serif";
  ctx.textAlign = "center";
  ctx.fillText("井", wx - camera.x, wy - camera.y - 18);
  ctx.textAlign = "left";
}

function drawDayNight() {
  const hour = state.gameHour;
  let alpha = 0;
  if (hour >= 20 || hour < 5) alpha = 0.45;
  else if (hour >= 18) alpha = 0.25;
  else if (hour < 7) alpha = 0.3;
  if (alpha > 0) {
    ctx.fillStyle = `rgba(10, 15, 40, ${alpha})`;
    ctx.fillRect(0, 0, VIEW_W, VIEW_H);
  }
}

function drawMinimap() {
  if (!state.tiles.length) return;
  const mw = state.mapWidth;
  const mh = state.mapHeight;
  const scale = 160 / mw;
  mctx.fillStyle = "#1a3018";
  mctx.fillRect(0, 0, 160, 120);
  for (let y = 0; y < mh; y++) {
    for (let x = 0; x < mw; x++) {
      const t = state.tiles[y][x];
      mctx.fillStyle = t === "water" ? "#3a8fc4" : t === "soil" ? "#6b4428" : t === "path" ? "#c4a574" : "#4a8f3a";
      mctx.fillRect(x * scale, y * scale * (120 / mh), scale, scale * (120 / mh));
    }
  }
  const px = displayPlayer.x / TILE * scale;
  const py = displayPlayer.y / TILE * scale * (120 / mh);
  mctx.fillStyle = "#4a90d9";
  mctx.fillRect(px - 2, py - 2, 4, 4);
}

function draw() {
  animFrame++;
  const mapW = state.mapWidth * TILE;
  const mapH = state.mapHeight * TILE;

  camera.x = Math.max(0, Math.min(mapW - VIEW_W, displayPlayer.x - VIEW_W / 2));
  camera.y = Math.max(0, Math.min(mapH - VIEW_H, displayPlayer.y - VIEW_H / 2));

  const startTx = Math.floor(camera.x / TILE);
  const startTy = Math.floor(camera.y / TILE);
  const endTx = Math.ceil((camera.x + VIEW_W) / TILE);
  const endTy = Math.ceil((camera.y + VIEW_H) / TILE);

  for (let ty = startTy; ty <= endTy; ty++) {
    for (let tx = startTx; tx <= endTx; tx++) {
      drawTile(tx, ty, state.tiles[ty]?.[tx] || "grass");
    }
  }

  for (const land of state.lands) drawLand(land);
  drawWell();
  drawMerchant();
  drawCharacter(displayPlayer.x, displayPlayer.y, displayPlayer.facing, true);
  drawDayNight();

  for (const p of particles) {
    p.x += p.vx;
    p.y += p.vy;
    p.vy += 0.15;
    p.life--;
    ctx.fillStyle = p.color;
    ctx.globalAlpha = p.life / 50;
    ctx.fillRect(p.x - camera.x, p.y - camera.y, 4, 4);
    ctx.globalAlpha = 1;
  }
  particles = particles.filter((p) => p.life > 0);

  drawMinimap();
}

function resolveMoveDelta() {
  let dx = 0;
  let dy = 0;
  if (keys.has("w") || keys.has("arrowup")) dy -= 1;
  if (keys.has("s") || keys.has("arrowdown")) dy += 1;
  if (keys.has("a") || keys.has("arrowleft")) dx -= 1;
  if (keys.has("d") || keys.has("arrowright")) dx += 1;

  if (dx === 0 && dy === 0) return { dx: 0, dy: 0 };

  // 禁止斜向：优先最近按下的方向键
  if (dx !== 0 && dy !== 0) {
    const vKeys = ["w", "arrowup", "s", "arrowdown"];
    const hKeys = ["a", "arrowleft", "d", "arrowright"];
    if (lastMoveKey && keys.has(lastMoveKey)) {
      if (vKeys.includes(lastMoveKey)) dx = 0;
      else if (hKeys.includes(lastMoveKey)) dy = 0;
      else dy = 0;
    } else {
      dy = 0;
    }
  }
  return { dx, dy };
}

async function tickInput() {
  if (!state.manualMode || moveInFlight) return;
  const now = Date.now();
  if (now - lastMoveAt < MOVE_COOLDOWN_MS) return;

  let dx = 0;
  let dy = 0;

  const hasWasd =
    keys.has("w") || keys.has("arrowup") ||
    keys.has("s") || keys.has("arrowdown") ||
    keys.has("a") || keys.has("arrowleft") ||
    keys.has("d") || keys.has("arrowright");

  if (hasWasd) {
    clickPath = [];
    ({ dx, dy } = resolveMoveDelta());
  } else if (clickPath.length > 0) {
    const step = clickPath[0];
    const ptx = Math.floor(state.player.x / TILE);
    const pty = Math.floor(state.player.y / TILE);
    dx = step.x - ptx;
    dy = step.y - pty;
    if (Math.abs(dx) + Math.abs(dy) !== 1) {
      clickPath.shift();
      return;
    }
    clickPath.shift();
  } else {
    return;
  }

  if (dx === 0 && dy === 0) return;

  moveInFlight = true;
  lastMoveAt = now;
  try {
    const ok = await doMove(dx, dy);
    if (!ok) clickPath = [];
  } finally {
    moveInFlight = false;
  }
}

function lerpPlayer() {
  // 手动模式下位置由 doMove 直接同步，不做插值避免漂移
  if (!state.manualMode) {
    const d = Math.hypot(state.player.x - displayPlayer.x, state.player.y - displayPlayer.y);
    if (d > 0.5) {
      displayPlayer.x += (state.player.x - displayPlayer.x) * 0.2;
      displayPlayer.y += (state.player.y - displayPlayer.y) * 0.2;
    }
  }
}

function loop() {
  lerpPlayer();
  draw();
  requestAnimationFrame(loop);
}

connectWs();
loadSnapshot();
renderHotbar();
setupInput();
canvas.focus();
loop();
