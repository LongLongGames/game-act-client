# Steam 手动重试 Init（无轮询）

## 改动

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/AppFlow/AppFlowController.cs` | 点击「进入游戏」时 `TryEnsureSteamAndRefreshLoginUi` 再 Init 一次 |
| `Assets/Scripts/UI/RuntimeLoginView.cs` | Steam 未就绪时按钮仍可点，文案提示「启动 Steam 后点按钮重试」 |

## 行为

1. 启动时 `InitSteam()` 尝试一次（与原来一致）。
2. 进入 Login 页时再尝试一次（处理「启动后才开 Steam、尚未点按钮」）。
3. 点「进入游戏」：若仍未 Init → 再调 `SteamService.Init()`；成功则继续 ticket 登录，失败则状态栏提示，不重启客户端。
4. **无定时轮询**。

## 覆盖方式

将本包 `Assets/Scripts/**` 覆盖到工程对应路径即可。
