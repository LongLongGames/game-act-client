using System;
using System.Collections.Generic;
using LiteEntitySystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameAct.Les.Shared;
using GameAct.Skill;
using GameAct.Les.View;
using GameAct.Spatial;

namespace GameAct.Les
{
    /// <summary>
    /// Solo 离线权威：ServerEntityManager + 本地 ActPlayer + 敌人。
    /// 刷怪统一：AddEntity&lt;ActMonster&gt; + MonsterBotController + MonsterView（Dummy prefab）。
    /// </summary>
    public sealed class LesAuthoritySession : IDisposable
    {
        public const int DefaultMonsterCount = 12;

        ServerEntityManager _em;
        readonly List<MonsterView> _views = new List<MonsterView>(32);
        Transform _viewRoot;
        string _levelSceneName;
        bool _started;
        ActPlayer _localPlayer;

        public bool IsStarted => _started;
        public int MonsterCount => _views.Count;
        public ActPlayer LocalPlayer => _localPlayer;

        public void Start(Vector3 center, string levelSceneName, int monsterCount = DefaultMonsterCount)
        {
            if (_started) Stop();

            var typesMap = LesTypesMapFactory.Create();
            _em = new ServerEntityManager(
                typesMap,
                LesTypesMapFactory.HeaderByte,
                (byte)LesTypesMapFactory.TickRate,
                ServerSendRate.EqualToFPS);

            _levelSceneName = levelSceneName;
            _viewRoot = new GameObject("LES_MonsterViews_Solo").transform;
            MoveToLevel(_viewRoot.gameObject, levelSceneName);

            _localPlayer = _em.AddEntity<ActPlayer>(e =>
            {
                e.Spawn(center);
                e.SetDriveLocally(true);
            });

            SpawnMonstersInternal(center, monsterCount, baseRadius: 6f);

            // Flow Field：以开局中心建场并烘焙障碍，供怪物追击绕障
            FlowFieldService.Reset();
            FlowFieldService.Ensure(center, halfExtent: 48f, cellSize: FlowField.DefaultCellSize);
            if (_localPlayer != null)
                FlowFieldService.Tick(_localPlayer.Position, 0f);

            MonsterDeathService.AuthorityDestroyMonster = DestroyMonsterById;
            MonsterKnockbackService.AuthorityKnockback = KnockbackMonsterById;
            _started = true;
            Debug.Log($"[LES] Offline authority: player={_localPlayer.Id} monsters={MonsterCount} flowField=on");
        }

        /// <summary>
        /// Debug / 运行时追加怪：同一套 LES 链路（会走、有 AI），不是站桩 View。
        /// </summary>
        public void SpawnExtraMonsters(Vector3 center, int count, float radius, string levelSceneName = null)
        {
            if (!_started || _em == null)
            {
                Debug.LogWarning("[LES] SpawnExtraMonsters: session not started");
                return;
            }
            if (!string.IsNullOrEmpty(levelSceneName))
                _levelSceneName = levelSceneName;

            int n = Mathf.Clamp(count, 1, 64);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float r = radius * (0.35f + 0.65f * ((i % 3) / 2f));
                var pos = Snap(center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
                SpawnOne(pos);
            }
            Debug.Log($"[LES] SpawnExtraMonsters +{n} totalViews={_views.Count}");
        }

        void SpawnMonstersInternal(Vector3 center, int count, float baseRadius)
        {
            int n = Mathf.Clamp(count, 0, 64);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float radius = baseRadius + (i % 3) * 2.5f;
                var pos = Snap(center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
                SpawnOne(pos);
            }
        }

        void SpawnOne(Vector3 pos)
        {
            var monster = _em.AddEntity<ActMonster>(e => e.Spawn(pos));
            _em.AddAIController<MonsterBotController>(c => c.StartControl(monster));
            var view = MonsterView.Create(monster.Id, pos, _viewRoot);
            MoveToLevel(view.gameObject, _levelSceneName);
            _views.Add(view);
        }

        public void Tick()
        {
            if (!_started || _em == null) return;

            // 以本地玩家为 Flow Field 目标，周期性重建
            if (_localPlayer != null && !_localPlayer.IsDestroyed)
                FlowFieldService.Tick(_localPlayer.Position, 1f / 30f);

            _em.Update();
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                var view = _views[i];
                if (view == null)
                {
                    _views.RemoveAt(i);
                    continue;
                }
                foreach (var monster in _em.GetEntities<ActMonster>())
                {
                    if (monster != null && !monster.IsDestroyed && !monster.IsDead && monster.Id == view.EntityId)
                    {
                        view.Apply(monster.Position, monster.Yaw, monster.SpeedXZ);
                        break;
                    }
                }
            }
        }

        void DestroyMonsterById(int entityId)
        {
            if (_em == null) return;
            foreach (var monster in _em.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed) continue;
                if (monster.Id != entityId) continue;
                monster.MarkDead();
                monster.Destroy();
                Debug.Log($"[LES] ActMonster destroyed id={entityId}");
                break;
            }
        }

        /// <summary>
        /// 权威击退：改 ActMonster 位置。View 下帧 Apply 会跟上。
        /// Boss/大型怪后期可在 ActMonster.ApplyKnockback 或此处按体重短路。
        /// </summary>
        void KnockbackMonsterById(int entityId, Vector3 worldDir, float distance)
        {
            if (_em == null || distance <= 0.001f) return;
            foreach (var monster in _em.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed || monster.IsDead) continue;
                if (monster.Id != entityId) continue;
                monster.ApplyKnockback(worldDir, distance);
                Debug.Log($"[LES] ActMonster knockback id={entityId} dist={distance:F2}");
                break;
            }
        }

        public void Stop()
        {
            FlowFieldService.Reset();
            MonsterDeathService.Clear();
            MonsterKnockbackService.Clear();
            _localPlayer = null;
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null) UnityEngine.Object.Destroy(_views[i].gameObject);
            _views.Clear();
            if (_viewRoot != null)
            {
                UnityEngine.Object.Destroy(_viewRoot.gameObject);
                _viewRoot = null;
            }
            _em = null;
            _started = false;
        }

        public void Dispose() => Stop();

        static Vector3 Snap(Vector3 pos)
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
