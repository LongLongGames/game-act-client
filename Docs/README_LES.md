# LES 接入进度（game-act-client）

## 已完成（本包 / 仓库应具备）

- [x] **LES 插件在工程内**（`Assets/Plugins/LiteEntitySystem`）
- [x] **类型表** `LesTypesMapFactory`：Enemy / EnemyBot / **Player / PlayerController**
- [x] **Solo 离线权威** `LesAuthoritySession`：无端口 `ServerEntityManager` + 本地 `ActPlayer` + 敌人
- [x] **LES Transport** `LesNetworkHub`：LiteNet + `ServerEntityManager` / `ClientEntityManager`
- [x] **Join + typesHash** 校验；不匹配断线
- [x] **GameActNetPeer** 适配 `AbstractNetPeer`
- [x] **Steam Lobby 地址** `les_host` / `les_port`（`SteamLobbyEndpoint`）
- [x] **AppFlow 走 LES 门面** `EnsureGameplayNetworkAsync` → `LesNetworkGate.EnsureAsync`
- [x] **Bootstrap** 注入 `LesNetworkHub`（非裸 `LiteNetSession`）
- [x] **Host 本地玩家** `SpawnLocalPlayer`（`DriveLocally`）
- [x] **Client 远端玩家** Join 后 `ActPlayer` + `ActPlayerController`
- [x] **敌人** Solo/Host 刷怪；Client 快照 + `EnemyView`
- [x] **GameplayRunner** 主路径改为 LES 玩家位姿驱动 View

## 未完成（下一阶段）

- [ ] Client 本地预测手感打磨（HumanController 已接，需实机调缓冲）
- [ ] 本机 `SimulateLatency` / 双进程回归用例
- [ ] 战斗：伤害、技能 RPC、受击
- [ ] 投射物 `AddPredictedEntity`
- [ ] 物理命中 + LagCompensation
- [ ] Steam P2P 替换 UDP
- [ ] SyncGroup / AOI
- [ ] 断线重连与版本不一致 UI

## 合入注意

1. 覆盖本包 `act/Scripts/**` → `Assets/Scripts/**`
2. 确认 `GameAct.asmdef` 含 `"LiteEntitySystem"`
3. 进单机 Map1：应能移动（LES `ActPlayer`），并见红胶囊敌人
4. 联机：房主开始后 Lobby 有 `les_host`；成员能连上并收到敌人/玩家实体

## 架构一句话

```text
Solo  = ServerEntityManager（无 socket）+ ActPlayer.DriveLocally
Host  = ServerEntityManager + UDP + 本地 DriveLocally + 远端 HumanController
Client= ClientEntityManager + HumanController 预测 + 快照
战斗逻辑应写在 Shared（ActPlayer / ActEnemy / RPC），不要再开 LocalSimulation 分叉
```
