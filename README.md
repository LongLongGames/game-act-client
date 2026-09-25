> 组织总览与进度：[LongLongGames](https://github.com/LongLongGames) · [Platform Roadmap](https://github.com/orgs/LongLongGames/projects/1)  
> 服务器仓库：[game-act-server](https://github.com/LongLongGames/game-act-server)

# game-act-client

即时战斗（Action RPG / 类 Diablo + ROR2）Unity 客户端。  
与 [game-act-server](https://github.com/LongLongGames/game-act-server) 配套，遵循组织统一身份与独立部署原则。

身份先调 [MP](https://github.com/LongLongGames/MP) 拿 JWT，再调本游戏 Gateway。

## 游戏定位

- **类型**：P2P 1~4 人共斗（首发省成本），支持单机剧情 + 后期可选官服
- **参考**：Diablo 3、Risk of Rain 2、Space Marine 2 / World War Z、GUNDAM 诡异征途
- **发行**：目前仅 Steam
- **核心体验**：单机跑完剧情解锁角色 → 刷秘境 + Build + 成就 + 联机

## 客户端架构

- **渲染**：URP + ShaderGraph
- **优化**：
  - GPU Instancing（合批）
  - 骨骼动画考虑 GPU 方案（初期可先用标准 GPU Skinning，profiling 后再决定）
  - 可选 DOTS Subscene 做场景 + 远景怪群（非强制）
- **同步**：
  - StateSync + 玩家预测 / 回滚
  - 怪物不做回滚，远景怪群纯客户端群演
- **联机流程**：
  1. 创建房间（Steam P2P Host）
  2. 加入房间（Lobby ServerList：官服 / 社区 VPS / Host 的 P2P 主机）
  3. 邀请好友（Steamworks）
- **输入**：PC / 主机（键鼠 + 手柄已支持，后续可接组织 Input 组件）
- **反作弊**：Low Priority（客户端不持有权威存档，只上报关键操作）

## 依赖与组件

| 组件 | 用途 | 状态 |
|------|------|------|
| [AssetBundleFramework](https://github.com/setsuodu/AssetBundleFramework) | 资源加载 | ✅ |
| [ExcelConfigCompiler](https://github.com/setsuodu/ExcelConfigCompiler) | 导表 | ✅ |
| [BugReport](https://github.com/setsuodu/BugReport) | 异常 / 反馈 | ✅ |
| [Localization](https://github.com/setsuodu/Localization) | 多语言 | ✅ |
| [Mail](https://github.com/setsuodu/Mail) | 游戏内邮件 | ✅ |
| Input | PC / 主机输入（键鼠 + 手柄） | ✅ |

## 启动与联调

1. 确保本机已启动：
   - MP（端口 11080）
   - game-act-server（Gateway 13280）
2. 客户端先调 MP `/auth/login` 拿 JWT
3. 再调本游戏 Gateway（`http://localhost:13280`）拉取资料 / 存档 / 房间列表
4. Steamworks 需在 Steam 环境下测试 P2P / Lobby

详细 API 与 Token 生命周期见服务器 README 与 [ADR-0004](https://github.com/LongLongGames/.github/blob/main/docs/adr/0004-client-access-token-lifecycle.md)。

## 开发计划（与服务器对齐）

### Phase 0 – 骨架（1~2 周）✅
- [x] 工程初始化（URP、基础场景、登录流）
- [x] MP 登录 → JWT → 拉资料 / 存档
- [x] 版本检查接口对接

#### P0 联调说明

1. 打开场景 `Assets/Scenes/Demo.unity`
2. 在任意物体上挂 `GameBootstrap`（可选再挂 `GameLifetimeScope`）
3. 先启动 MP（11080）与 game-act-server（13280）
4. Play：版本检查 → 登录（MP official）→ 拉 `/api/v1/user/profile` → Home
5. Token 持久化与 401 处理遵循 [ADR-0004](https://github.com/LongLongGames/.github/blob/main/docs/adr/0004-client-access-token-lifecycle.md)

脚本入口：`Assets/Scripts/`（Network / Auth / Services / AppFlow / UI / Bootstrap / DI）

### Phase 1 – 联机基础（3~5 周）✅
- [x] Steamworks 接入（Lobby / 邀请 / P2P）
- [x] LiteNetLib 客户端（Host / Join / 官服连接）
- [x] 重连功能和引导
- [x] 基础 StateSync 与玩家移动预测（靠 LES 默认机制支持）

### Phase 2 – 战斗核心（进行中，基础闭环已通）
已落地（本地 / Host 权威可玩）：
- [x] 角色控制（键鼠 + 手柄、WorldMotor 撞墙/贴地/跳跃、相机平滑）
- [x] 2 个技能（近战 + 火球 Projectile）
- [x] 打怪死亡（MonsterDeathService）
- [x] 关卡障碍（LogicCollider / SpatialHash / WorldMotor）
- [x] 怪物寻路 + 避障（FlowField + MonsterBotController 仇恨/Chase/Idle）
- [x] 击退（MonsterKnockbackService）

仍待完善：
- [ ] 玩家完整预测 / 回滚（移动 + 技能释放 + 受击）
- [ ] AOI 分层接收与远景群演
- [ ] Boss / 普通怪完整 AI 与表现（服务器权威）
- [ ] 技能系统表驱动 + 完整客户端表现（冷却/动画/特效）
- [ ] 单机剧情模式可通关（进度上报服务器）
- [ ] 基础 HUD / 战斗 UI（血条、技能栏、伤害数字）

### Phase 3 – 内容与打磨（持续）
- [ ] 秘境 / Build / UI / 成就
- [ ] 性能优化（GPU Instancing、可选 DOTS）
- [ ] 官方 ServerList 支持
- [ ] 反作弊相关客户端配合（Low Priority）

## 技术栈

- Unity（URP）
- LiteNetLib + Steamworks
- 组织统一组件（AssetBundle / 导表 / BugReport / Localization 等）
- 状态同步 + 预测回滚（仅玩家）

## 相关仓库

- 服务器：[game-act-server](https://github.com/LongLongGames/game-act-server)
- 中台：[MP](https://github.com/LongLongGames/MP)
- 模板参考：[game-match3-client](https://github.com/LongLongGames/game-match3-client)
- 组织总览：[LongLongGames/.github](https://github.com/LongLongGames/.github/blob/main/profile/README.md)

## 工程规范

- 客户端 Access Token 生命周期与鉴权失败处理必须遵守 [ADR-0004](https://github.com/LongLongGames/.github/blob/main/docs/adr/0004-client-access-token-lifecycle.md)
- 所有关键数值变化（经验、道具、解锁）最终以服务器权威为准
