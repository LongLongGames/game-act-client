# LES 联网接入说明（game-act-client）

## 本包已完成（最大缺口）

1. **LES Transport 路径**
   - `LesNetworkHub`：`INetSession` + LiteNetLib `NetManager`
   - Host：`ServerEntityManager` + `Start(port)` + Join 校验 `typesHash`
   - Client：`Connect` → `JoinPacket` → `ClientEntityManager` + `Deserialize`
   - `GameActNetPeer`：`AbstractNetPeer` 适配
2. **Steam Lobby 地址交换**
   - Host 写入 `les_host` / `les_port`
   - Client 读取后连接（局域网 UDP）
3. **敌人**
   - Solo：`LesAuthoritySession`（无端口）
   - Host：`LesNetworkHub.SpawnEnemiesAround`
   - Client：快照构造 + `EnemyView`
4. **Bootstrap** 默认注入 `LesNetworkHub`（不再用裸 `LiteNetSession` 当玩法传输）

合入时必做：

- 覆盖本包 `Scripts/**`
- **手动改** `AppFlowController.EnsureGameplayNetworkAsync` → 调用 `LesNetworkGate.EnsureAsync`（见 `AppFlowController_EnsureNetwork.cs.patch.txt`）

---

## 剩余开发步骤（按顺序）

### P1 — 玩家进入 LES（才能谈预测回滚）

1. Shared：`ActPlayer : PawnLogic`（位姿 SyncVar）
2. Shared：`ActPlayerController : HumanControllerLogic<Input, ActPlayer>`
3. Host Join 成功后：`AddEntity<ActPlayer>` + `AddController`（对照 demo `ServerLogic.OnJoinReceived`）
4. Client：本地玩家用 LES Controller 输入；逐步拆掉 `LocalSimulation` / `ClientSimulation` 双轨

### P2 — 本机验证预测

1. `LesNetworkHub.SimulateLatency = true`（50–80ms）
2. 同机开两个进程（或 Editor + Build）：一个 Host 一个 Client
3. 确认 Client 角色有预测/和解；再上 Clumsy 局域网

### P3 — 玩法 RPC / 投射物

1. 技能、受击 `RemoteCall`（`ExecuteOnPrediction | SendToOther`）
2. `AddPredictedEntity` 投射物（对照 `SimpleProjectile`）
3. 需要时再上 `UnityPhysicsManager` + LagCompensation 命中

### P4 — 体验

1. Steam P2P 替换 UDP Transport（仍走 `AbstractNetPeer`）
2. `SyncGroup` / AOI 裁剪
3. 断线重连、版本 hash 提示 UI
4. 官服 Dedicated（可选）：独立 `ServerEntityManager` 进程

### 明确不做进复制的

过场、纯 VFX、UI、音效、远景群演 → 继续本地/事件驱动，不要塞 SyncVar。

---

## 联机自测清单

| 步骤 | 期望 |
|------|------|
| A 单机进 Map1 | 红胶囊敌人漫游，无端口占用 |
| B 房主开始 | 日志 `LES Host :9050`，Lobby 有 `les_host` |
| C 成员开始 | 日志 `Connecting x.x.x.x:9050` → `LES Client connected` |
| D 成员看到敌人 | 红胶囊与 Host 侧运动一致（允许插值延迟） |

当前 **玩家仍是本地 Simulation**，C/D 验证的是 **传输 + 敌人复制**，不是玩家预测回滚。
