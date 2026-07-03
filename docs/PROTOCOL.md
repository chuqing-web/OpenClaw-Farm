# OpenClaw Farm Agent Protocol

Base URL: `http://127.0.0.1:28080`  
WebSocket: `ws://127.0.0.1:28080/ws/agent`

All HTTP responses use `{ data, ts }` on success or `{ error, message }` with 4xx on failure.

## HTTP Read Endpoints

### GET /health

```json
{ "ok": true }
```

### GET /agent/state/farm_lands

Returns all farm plots.

```json
{
  "data": [
    {
      "id": "land_01",
      "x": 80,
      "y": 112,
      "state": "growing",
      "cropId": "crop_strawberry",
      "growth": 0.5,
      "needsWater": false,
      "canHarvest": false
    }
  ],
  "ts": 1700000000000
}
```

### GET /agent/state/bag

```json
{
  "data": {
    "gold": 100,
    "items": [{ "itemId": "seed_strawberry", "count": 5 }]
  },
  "ts": 1700000000000
}
```

### GET /agent/state/merchant_price

```json
{
  "data": {
    "crop_strawberry": 15,
    "crop_wheat": 8,
    "crop_carrot": 12
  },
  "ts": 1700000000000
}
```

### GET /agent/state/farm_order

```json
{
  "data": {
    "orders": [
      { "cropId": "crop_wheat", "required": 5, "delivered": 0, "reward": 100 }
    ],
    "expiresAt": "2026-07-04T00:00:00.000Z"
  },
  "ts": 1700000000000
}
```

### GET /agent/state/player

```json
{
  "data": { "x": 48, "y": 80, "sceneId": "farm_main" },
  "ts": 1700000000000
}
```

### GET /agent/state/sell_confirm

Returns a one-time token required for `sell_item`.

```json
{
  "data": { "confirmToken": "abc123", "expiresIn": 60 },
  "ts": 1700000000000
}
```

## WebSocket Protocol

### Downstream (Agent → Game)

```json
{
  "type": "action",
  "reqId": "550e8400-e29b-41d4-a716-446655440000",
  "payload": {
    "actionId": "move_to",
    "params": { "x": 102, "y": 96, "sceneId": "farm_main" }
  }
}
```

### Upstream (Game → Agent)

```json
{
  "type": "action_result",
  "reqId": "550e8400-e29b-41d4-a716-446655440000",
  "success": true,
  "message": "moved to (102,96)",
  "extra": { "player": { "x": 102, "y": 96, "sceneId": "farm_main" } }
}
```

### World Patch (Game → Browser)

```json
{
  "type": "world_patch",
  "ts": 1700000000000,
  "player": { "x": 102, "y": 96, "sceneId": "farm_main" },
  "lands": [],
  "bag": { "gold": 100, "items": [] },
  "gameHour": 8
}
```

## Actions

| actionId    | params                                      | Description                          |
|-------------|---------------------------------------------|--------------------------------------|
| `move_to`   | `{ x, y, sceneId? }`                        | Grid pathfinding to target           |
| `interact`  | `{ targetEntityId, itemId? }`               | Plant, water, harvest, clear         |
| `sell_item` | `{ itemId, count?, confirmToken? }`         | Sell crops near merchant             |
| `wait`      | `{ ms }`                                    | Idle wait (max 30000ms)              |

### interact semantics

| targetEntityId | itemId              | Effect                    |
|----------------|---------------------|---------------------------|
| `land_XX`      | `seed_*`            | Plant seed on empty land  |
| `land_XX`      | (omit or `water`)   | Water if needs water      |
| `land_XX`      | `harvest`           | Harvest mature crop       |
| `land_XX`      | `clear`             | Clear withered land       |

### sell_item flow

1. Move within 3 tiles of `merchant_01`
2. `GET /agent/state/sell_confirm` for token
3. Send `sell_item` with `confirmToken`
4. Without token: returns `success: false`, `extra.needConfirm: true`

## Security

- Server binds `127.0.0.1` only
- Global action cooldown ~800ms + random jitter
- Actions serialized per WS connection
- `sell_item` requires confirm token
