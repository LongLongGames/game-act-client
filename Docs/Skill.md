# SkillSystem - 通用技能与命中基础

配合 SpatialSystem 使用的 Skill + Hit 基础框架。

## 设计原则

- **一切从 Skill 出发**：普攻、即时命中、弹道、延迟区域、持续区域都是 Skill
- **HitSystem 是服务**：只负责检测，不负责伤害结算和表现
- **接口稳定，实现可换**：后续可升级 Raycast 精度、加 Lag Compensation、接 LES PredictedEntity

## 依赖

需要先有 SpatialSystem（`GameAct.Spatial` 命名空间）：
- `ISpatialIndex`
- `AABB`
- `SpatialLayer`

## 文件结构

```
Scripts/
├── HitResult.cs
├── IHitSystem.cs
├── HitSystem.cs              ← 默认实现（基于 Spatial）
├── SkillDefine.cs            ← 技能静态配置 + 工厂方法
├── ISkill.cs / SkillBase.cs
├── SkillFactory.cs
├── SkillCaster.cs            ← 单个实体的技能槽管理
├── Skills/
│   ├── MeleeSkill.cs         ← 普攻 / 近战
│   ├── HitscanSkill.cs       ← 即时射线
│   ├── ProjectileSkill.cs    ← 飞行弹道
│   ├── DelayedAreaSkill.cs   ← 延迟区域（预警后生效）
│   └── PersistentZoneSkill.cs← 持续地面区域
└── SkillSystemExample.cs     ← 完整示例
```

## 快速使用

```csharp
// 1. 准备 Spatial + HitSystem
ISpatialIndex spatial = new SpatialHash(10f);
var hitSystem = new HitSystem(spatial);
hitSystem.GetEntityBounds = id => ...;   // 注入实体 Bounds 查询

// 2. 给角色加技能
var caster = new SkillCaster();
caster.AddSkill(SkillDefine.CreateMelee("Slash"));
caster.AddSkill(SkillDefine.CreateHitscan("Rail"));
caster.AddSkill(SkillDefine.CreateProjectile("Fireball"));
caster.AddSkill(SkillDefine.CreateDelayedArea("Meteor"));
caster.AddSkill(SkillDefine.CreatePersistentZone("FireZone"));

// 3. 每帧 Tick
caster.Tick(Time.deltaTime);

// 4. 释放
var ctx = new SkillCastContext {
    CasterEntityId = playerId,
    CasterPosition = pos,
    CasterForward  = forward,
    TargetPosition = aimPoint,
    HitSystem      = hitSystem,
    OnHit          = (casterId, hit, def) => { /* 结算伤害 */ }
};
caster.TryCast(skillId, ctx);
```

## 技能类型对照

| ExecType          | 类                    | 典型用途           |
|-------------------|-----------------------|--------------------|
| Melee             | MeleeSkill            | 普攻、近战技能     |
| Hitscan           | HitscanSkill          | 射线枪、瞬发法术   |
| Projectile        | ProjectileSkill       | 火球、箭矢         |
| DelayedArea       | DelayedAreaSkill      | 落雷、陨石（带预警）|
| PersistentZone    | PersistentZoneSkill   | 火墙、毒池         |
| ShapeArea         | (暂用 Melee)          | 扇形/环形，可扩展  |

## 后续扩展建议

1. 伤害结算单独做成 DamagePipeline（暴击、护盾、元素等）
2. Projectile 改为 LES `AddPredictedEntity`，支持客户端预测
3. HitSystem.Raycast 升级为真正的射线-AABB / 射线-Capsule 测试
4. 加入 Lag Compensation（回滚到开火时刻的位置）
5. SkillDefine 改为从 Excel / ScriptableObject 加载

## 与 SpatialSystem 的配合

把两个文件夹都放进工程即可。  
SkillSystem 通过 `GameAct.Spatial` 命名空间引用 Spatial 的接口，无直接依赖具体实现。
EOF