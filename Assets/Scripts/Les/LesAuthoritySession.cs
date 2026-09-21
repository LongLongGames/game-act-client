using System;
using System.Collections.Generic;
using LiteEntitySystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameAct.Les.Shared;
using GameAct.Les.View;

namespace GameAct.Les
{
    /// <summary>
    /// Solo / Host 侧 LES 权威会话：进程内 ServerEntityManager，不绑端口。
    /// 负责在 Map1 刷一群 AiControllerLogic 敌人并驱动 View。
    /// Client 模式不启动（等真正联机与快照后再接）。
    /// </summary>
    public sealed class LesAuthoritySession : IDisposable
    {
        public const int DefaultEnemyCount = 12;
        public const int TickRate = 30;
        const byte PacketHeader = 1;

        static bool _fieldTypesRegistered;

        ServerEntityManager _em;
        readonly List<EnemyView> _views = new List<EnemyView>(32);
        Transform _viewRoot;
        string _levelSceneName;
        bool _started;

        public bool IsStarted => _started;
        public int EnemyCount => _views.Count;
        public ServerEntityManager ServerManager => _em;

        public void Start(Vector3 center, string levelSceneName, int enemyCount = DefaultEnemyCount)
        {
            if (_started)
                Stop();

            EnsureLoggerAndFieldTypes();

            var typesMap = new EntityTypesMap<GameEntities>()
                .Register(GameEntities.Enemy, e => new ActEnemy(e))
                .Register(GameEntities.EnemyBot, e => new EnemyBotController(e));

            _em = new ServerEntityManager(
                typesMap,
                PacketHeader,
                TickRate,
                ServerSendRate.EqualToFPS);

            _levelSceneName = levelSceneName;
            _viewRoot = new GameObject("LES_EnemyViews").transform;
            MoveToLevel(_viewRoot.gameObject, levelSceneName);

            int n = Mathf.Clamp(enemyCount, 0, 64);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float radius = 6f + (i % 3) * 2.5f;
                var pos = center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                pos = SnapToGround(pos);

                var enemy = _em.AddEntity<ActEnemy>(e => e.Spawn(pos));
                _em.AddAIController<EnemyBotController>(c => c.StartControl(enemy));

                var view = EnemyView.Create(enemy.Id, pos, _viewRoot);
                MoveToLevel(view.gameObject, levelSceneName);
                _views.Add(view);
            }

            _started = true;
            Debug.Log($"[LES] Authority session started: enemies={n} center={center} level={levelSceneName}");
        }

        public void Tick()
        {
            if (!_started || _em == null)
                return;

            _em.Update();

            // 权威姿态 → View（按 EntityId 对齐，避免枚举顺序变化）
            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null) continue;
                // GetEntities 无按 Id 直接取时线性匹配（敌人数量小）
                ActEnemy matched = null;
                foreach (var enemy in _em.GetEntities<ActEnemy>())
                {
                    if (enemy != null && !enemy.IsDestroyed && enemy.Id == view.EntityId)
                    {
                        matched = enemy;
                        break;
                    }
                }
                if (matched != null)
                    view.Apply(matched.Position, matched.Yaw);
            }
        }

        public void Stop()
        {
            if (!_started && _em == null)
                return;

            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i] != null)
                    UnityEngine.Object.Destroy(_views[i].gameObject);
            }
            _views.Clear();

            if (_viewRoot != null)
            {
                UnityEngine.Object.Destroy(_viewRoot.gameObject);
                _viewRoot = null;
            }

            // ServerEntityManager 无公开 Clear；停掉引用，由 GC 回收。
            // 正式联机时应按玩家/实体生命周期 Destroy。
            _em = null;
            _started = false;
            Debug.Log("[LES] Authority session stopped");
        }

        public void Dispose() => Stop();

        static void EnsureLoggerAndFieldTypes()
        {
            // 全限定，避免与 UnityEngine.Logger / 实例属性 EntityManager 冲突
            if (LiteEntitySystem.Logger.LoggerImpl == null)
                LiteEntitySystem.Logger.LoggerImpl = new UnityLesLogger();

            if (_fieldTypesRegistered)
                return;

            // 静态方法必须挂在类型上，不能经实例属性解析
            LiteEntitySystem.EntityManager.RegisterFieldType<Vector3>(Vector3.Lerp);
            _fieldTypesRegistered = true;
        }

        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 20f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }

        static void MoveToLevel(GameObject go, string sceneName)
        {
            if (go == null || string.IsNullOrEmpty(sceneName)) return;
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (go.scene == scene) return;
            SceneManager.MoveGameObjectToScene(go, scene);
        }
    }
}
