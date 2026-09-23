using UnityEngine;
using GameAct.Gameplay;
using GameAct.Gameplay.Player;
using GameAct.Skill;

namespace GameAct.DebugTools
{
    /// <summary>
    /// Combat 调试命令。刷怪必须走 GameplayRunner → LES（ActMonster + MonsterBotController），
    /// 禁止再 MonsterView.Create 旁路（那只会出站桩）。
    /// </summary>
    public static class CombatDebugCommands
    {
        static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            DebugCmd.Reg("help", "列出全部命令", () =>
            {
                foreach (var e in DebugCmd.All)
                    Debug.Log($"[DebugCmd] {e.Category}/{e.Name}: {e.Help}");
            }, "System");

            DebugCmd.Reg("spawn_monster", "LES 刷怪(可走) 默认 5 只", () =>
            {
                SpawnViaGameplay(5, 12f);
            }, "Combat");

            DebugCmd.RegAsync("spawn_monster_n", "LES 刷怪: spawn_monster_n [count] [radius]", async (args, ct) =>
            {
                int count = 5;
                float radius = 12f;
                if (args.Length > 0) int.TryParse(args[0], out count);
                if (args.Length > 1) float.TryParse(args[1], out radius);
                count = Mathf.Clamp(count, 1, 64);
                SpawnViaGameplay(count, radius);
                await Cysharp.Threading.Tasks.UniTask.CompletedTask;
            }, "Combat");

            DebugCmd.Reg("targets", "打印 CombatTargetRegistry 数量", () =>
            {
                Debug.Log($"[DebugCmd] targets alive={CombatTargetRegistry.CountAlive()} keys={CombatTargetRegistry.All.Count}");
            }, "Combat");

            DebugCmd.Reg("god", "本地玩家位置", () =>
            {
                var pv = Object.FindObjectOfType<PlayerView>();
                Debug.Log(pv != null
                    ? $"[DebugCmd] local player={pv.name} pos={pv.transform.position}"
                    : "[DebugCmd] no PlayerView");
            }, "Player");
        }

        static void SpawnViaGameplay(int count, float radius)
        {
            var runner = Object.FindObjectOfType<GameplayRunner>();
            if (runner == null || !runner.IsStarted)
            {
                Debug.LogWarning("[DebugCmd] GameplayRunner 未启动，无法走 LES 刷怪。先 Solo/Host 进图。");
                return;
            }

            if (!runner.TrySpawnDebugMonsters(count, radius))
            {
                Debug.LogWarning("[DebugCmd] TrySpawnDebugMonsters 失败（Client 无权威 / 会话未起）。");
                return;
            }

            Debug.Log($"[DebugCmd] LES spawn_monster count={count} radius={radius}（ActMonster+AI，应能走）");
        }
    }
}
