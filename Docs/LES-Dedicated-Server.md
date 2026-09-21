# LES 独立服务器 & Launcher 连接

按 [LiteEntitySystem Unity Example](ServerLogic / ClientLogic) 拆分后：

| 组件 | 路径 | 用途 |
|------|------|------|
| **LesServerHost** | `Les/Server/` | 权威服务器（Listen / VPS） |
| **LesDedicatedBootstrap** | `Les/Server/` | VPS 启动入口，解析 `-lesPort` |
| **LesClientSession** | `Les/Client/` | 纯客户端，只连远程 |
| **LesLauncherConnect** | `Les/Client/` | Launcher 传参自动连接 |
| **LesAuthoritySession** | `Les/` | 单机 Solo（不变） |
| **LesNetworkHub** | `Les/` | 旧 Listen-Server 混合（可逐步废弃） |

## 1. VPS 官服 / 专服

### 构建
- Unity：Dedicated Server Build Target，或普通 Linux 构建后加 `-batchmode -nographics`
- 场景里挂 `LesDedicatedBootstrap`（会自动加 `LesServerHost`）

### 启动示例
```bash
./GameActServer.x86_64 -batchmode -nographics -lesPort 9050 -lesMonsters 24
```

### 防火墙
开放 UDP（LiteNetLib）`9050`（或你指定的端口）。

### systemd 示例
```ini
[Service]
ExecStart=/opt/game-act/GameActServer.x86_64 -batchmode -nographics -lesPort 9050
Restart=always
```

## 2. 官方 Launcher 启动客户端

Launcher 在用户点「开始游戏 / 加入房间」后：

```bash
GameActClient.exe -lesConnect game-act.example.com:9050 -lesUser "PlayerName"
```

`LesLauncherConnect` 会解析参数并 `ConnectAsync`。

也可以在 UI 里：
```csharp
var ok = await launcherConnect.ConnectFromLauncher("10.0.0.5", 9050);
```

## 3. 与现有 Gateway / 房间系统协作

1. 客户端先走 **MP JWT** + **game-act-server Gateway** 拿房间列表 / 分配的 LES 地址
2. Gateway 返回 `{ "lesHost": "vps1.example.com", "lesPort": 9050 }`
3. 客户端 `LesClientSession.ConnectAsync(lesHost, lesPort)`
4. LES 只负责实时战斗同步；存档 / 成就仍走 HTTP Gateway

## 4. 协议与校验

- Connect Key：`game-act-les`
- JoinPacket：`UserName` + `GameHash`（`LesTypesMapFactory.EvaluateHash()`）
- Hash 不一致直接断线 → 强制客户端与服务器实体版本一致

## 5. 迁移建议

- 新流程用 `LesServerHost` + `LesClientSession`
- 旧 `LesNetworkHub` 的 Host 模式可暂时保留做本机联调，后续删除
- Solo 继续用 `LesAuthoritySession`，不绑端口
