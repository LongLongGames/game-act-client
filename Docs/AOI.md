# AOI – Area of Interest（分层同步）

对应 `Docs/P2 战斗细化.md` 的 **P2-5 AOI / 同步 / 特殊群**。

## 设计目标

- 基于已有 `ISpatialIndex`（SpatialHash），不重复造空间索引
- 近 / 中 / 远 三层 + Swarm（纯客户端群演）
- 带 hysteresis，避免边界抖动导致频繁进/出包
- Host / Dedicated 用 Observer 过滤「该发给哪个客户端」
- 客户端本地也可用同一套做视野剔除 / LOD

## 文件

```
Assets/Scripts/Spatial/
├── AoiTier.cs          ← 枚举 Near / Mid / Far / Swarm
├── AoiConfig.cs        ← 半径与 hysteresis 配置
├── AoiObserver.cs      ← 单个观察者（玩家）的兴趣集
├── AoiSystem.cs        ← 中央管理器 + Tick
└── AoiSystemExample.cs ← 最小可运行示例（可删）
```

## 分层语义

| Tier  | 默认半径 | 网络行为 |
|-------|----------|----------|
| Near  | 25m      | 全精度：位置 + 速度 + 动画 + 技能状态 |
| Mid   | 55m      | 降频 / 降精度快照 |
| Far   | 120m     | 稀疏更新，或只发「存在」 |
| Swarm | —        | 纯客户端群演，不参与权威同步 |

## 快速接入

```csharp
// 1. 创建
var spatial = sectionManager.ActiveSpatial; // 或 new SpatialHash(10f)
var aoi = new AoiSystem(AoiConfig.Default);
aoi.SetSpatial(spatial);

// 2. 注册观察者（本地玩家 / 每个连接的客户端）
aoi.AddObserver(playerEntityId, playerPosition);

// 3. 实体位置变化时
aoi.UpdateEntityPosition(entityId, worldPos);
// 同时保持 SpatialIndex 的 Insert/Update（技能/AI 仍用 Spatial）

// 4. 每 Tick（或每 N Tick）
aoi.Tick();

// 5. 网络过滤
if (aoi.ShouldSync(observerId, entityId))
    SendSnapshotToClient(...);

// 6. 查某层实体
var nearList = new List<int>();
aoi.GetEntitiesInTier(observerId, AoiTier.Near, nearList);
```

## 与现有系统关系

- **SpatialHash / SectionManager**：AOI 只读查询，不拥有索引
- **LES StateSync**：在发送快照前用 `ShouldSync` / `GetEntitiesInTier` 过滤
- **MonsterBotController / FlowField**：远景 Swarm 可走纯客户端群演路径（本 patch 只标记，不实现群演逻辑）
- **SpatialDebugDrawer**：可继续用其 aoiRadius 画圈；后续可扩展画三层环

## 后续可扩展

1. 把 position 缓存改为直接读 LES 实体组件，去掉 `_entityPositions` 双写
2. 按 Tier 配置不同发送频率（Near 每 tick，Mid 每 3 tick，Far 每 10 tick）
3. Swarm 层接简易 Boids / FlowField 客户端表现
4. 多 Section 时对 `GetRelevantSections` 做跨区 AOI
```
