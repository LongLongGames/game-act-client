# SpatialSystem - Basic Spatial Hash

可替换的空间索引基础实现，用于 GameAct 战斗核心。

## 设计目标

- **接口稳定**：上层（技能、AI、AOI、避障）只依赖 `ISpatialIndex`
- **实现可替换**：当前是 SpatialHash，后续可无痛换成 Quadtree / Burst 版 / C++ Plugin
- **支持分层**：`SpatialLayer`（Ground / LowAerial / Aerial）
- **零外部依赖**：纯 C# + UnityEngine

## 文件结构

```
Scripts/
├── ISpatialIndex.cs      ← 核心接口（不要轻易改）
├── SpatialHash.cs        ← 当前默认实现
├── AABB.cs               ← 简单包围盒
├── SpatialLayer.cs       ← 层级枚举
└── SpatialIndexExample.cs← 使用示例（可删）
```

## 快速使用

```csharp
// 创建
ISpatialIndex index = new SpatialHash(cellSize: 10f);

// 插入
var bounds = AABB.FromCenterSize(transform.position, new Vector3(1, 2, 1));
index.Insert(entityId, bounds, SpatialLayer.Ground);

// 更新（每帧或位置变化时）
index.Update(entityId, newBounds);

// 查询
List<int> results = new List<int>();
index.QueryRadius(center, radius, SpatialLayer.Ground | SpatialLayer.LowAerial, results);

// 移除
index.Remove(entityId);
```

## 后续替换建议

1. 保持 `ISpatialIndex` 接口不变
2. 新建 `QuadtreeSpatialIndex : ISpatialIndex` 或 `BurstSpatialHash`
3. 通过工厂或 DI 切换实现即可，上层代码零修改

## 性能说明（当前版本）

- 适合原型和中等规模（几百 ~ 一两千实体）
- 使用托管 Dictionary + List，有 GC 压力
- 后续可用 NativeContainers + Burst 重写同一接口大幅提升

## 格子大小建议

- 普通战斗区：8 ~ 16 米
- 大地图 Section：与 Flow Field 格子对齐（方便复用）
```