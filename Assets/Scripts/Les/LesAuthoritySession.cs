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
    /// Solo 离线权威：ServerEntityManager + 本地 ActPlayer + 敌人，不绑端口。
    /// Monster 从标准 Prefab 实例化。
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

            int n = Mathf.Clamp(monsterCount, 0, 64);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float radius = 6f + (i % 3) * 2.5f;
                var pos = Snap(center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));

                var monster = _em.AddEntity<ActMonster>(e => e.Spawn(pos));
                _em.AddAIController<MonsterBotController>(c => c.StartControl(monster));
                // 标准 Prefab：Root=MonsterView+Logic+HitReceiver, Child=Model+Animator
                var view = MonsterView.Create(monster.Id, pos, _viewRoot);
                MoveToLevel(view.gameObject, levelSceneName);
                _views.Add(view);
            }

            _started = true;
            Debug.Log($"[LES] Offline authority: player={_localPlayer.Id} monsters={n} (prefab MonsterView)");
        }

        public void Tick()
        {
            if (!_started || _em == null) return;
            _em.Update();
            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (view == null) continue;
                foreach (var monster in _em.GetEntities<ActMonster>())
                {
                    if (monster != null && !monster.IsDestroyed && monster.Id == view.EntityId)
                    {
                        view.Apply(monster.Position, monster.Yaw, monster.SpeedXZ);
                        break;
                    }
                }
            }
        }

        public void Stop()
        {
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
