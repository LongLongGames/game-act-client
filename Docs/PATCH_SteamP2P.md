# Steam P2P 传输层 Patch（与 UDP 兼容）

覆盖路径：把本包 `Assets/` 下文件覆盖到工程对应位置即可。

## 包含文件

| 路径 | 说明 |
|------|------|
| `Assets/Scripts/Les/Transport/SteamP2PNetPeer.cs` | **新增** LES `AbstractNetPeer` ↔ Steam 连接适配 |
| `Assets/Scripts/Les/Transport/SteamP2PTransport.cs` | **新增** Steam Networking Sockets 后端（Listen / Connect / Poll） |
| `Assets/Scripts/Les/LesNetworkHub.cs` | **覆盖** 双传输：`NetTransportKind.Udp` \| `SteamP2P` |
| `Assets/Scripts/Les/LesNetworkGate.cs` | **覆盖** Host/Client 自动选传输并写 Lobby 端点 |
| `Assets/Scripts/Steam/SteamLobbyEndpoint.cs` | **覆盖** 增加 `les_transport` / `les_steamid` / `les_vport` |

不改动：`ActPlayer` / 实体逻辑 / `INetSession` 接口签名（仅使用已有 `NetTransportKind` 参数）。

## 架构

```
                    INetSession (LesNetworkHub)
                           │
           ┌───────────────┴───────────────┐
           │                               │
    NetTransportKind.Udp          NetTransportKind.SteamP2P
           │                               │
      LiteNet NetManager            SteamP2PTransport
           │                               │
    GameActNetPeer                  SteamP2PNetPeer
           │                               │
           └───────────────┬───────────────┘
                           │
                    AbstractNetPeer
                           │
              ServerEntityManager / ClientEntityManager
                           │
                    ActPlayer / ActEnemy / RPC
```

## Lobby 数据约定

| Key | SteamP2P | UDP |
|-----|----------|-----|
| `les_transport` | `steam` | `udp` |
| `les_steamid` | Host SteamID64 | — |
| `les_vport` | virtual port（默认 0） | — |
| `les_host` | （可选）LAN IP 回退 | Host IP |
| `les_port` | （可选）9050 回退 | 9050 |

## 行为

1. **Host**：`LesNetworkGate` 在 Steam 已初始化时默认 `SteamP2P`，Lobby 写 `les_steamid`；否则 UDP + LAN IP。
2. **Client**：读 `les_transport`；若为 `steam` 则用 SteamID 连接，否则 UDP。
3. **强制 UDP**：`LesNetworkGate.PreferSteamP2PWhenAvailable = false`。
4. Join / EntitySystem 包头仍用 `LesPacketType`，应用层无感。

## 依赖

- 已有 `com.rlabrecque.steamworks.net`
- Steam 客户端运行中
- Host/Client 需在同一 Steam Lobby（或已知对方 SteamID）

## 验证步骤

1. 覆盖本包文件，编译通过。
2. **Steam 双开 / 两账号**：A 建房 → 进局（Host SteamP2P）→ B 加入 Lobby → 进局，日志应见：
   - `[SteamP2P] … listening …`
   - `[LES-Net] Sent Steam Join …`
   - `[LES-Net] Spawned remote ActPlayer …`
3. **无 Steam / 关 Prefer**：应回退 UDP，行为与改前一致。
4. Solo 路径不变（不启传输）。

## 已知限制 / 后续

- SteamP2P 无 LiteNet 延迟模拟；测预测请用 UDP + `SimulateLatency`。
- 断线重连、版本不一致 UI 仍按原 LES 未完成项。
- 若 Dedicated 官服，继续用 UDP/`LesLauncherConnect`，与本 Patch 不冲突。
