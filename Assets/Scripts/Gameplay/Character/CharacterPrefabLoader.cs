using UnityEngine;

namespace GameAct.Gameplay.Character
{
    /// <summary>
    /// 统一从 Resources / Editor AssetDatabase 加载 Character Prefab。
    /// Prefab 标准结构：Root(View + LogicColliderAuthoring + LogicCollider + HitReceiver) → Model(Animator)。
    /// </summary>
    public static class CharacterPrefabLoader
    {
        public const string PlayerResourcePath = "Character/Player/Y_Bot";
        public const string PlayerEditorPath = "Assets/Bundles/Character/Player/Y_Bot.prefab";

        public const string MonsterResourcePath = "Character/Monster/Dummy";
        public const string MonsterEditorPath = "Assets/Bundles/Character/Monster/Dummy.prefab";

        /// <summary>备用：旧 ZomBunny 路径。</summary>
        public const string MonsterFallbackResourcePath = "Character/Monster/ZomBunny";
        public const string MonsterFallbackEditorPath = "Assets/Bundles/Character/Monster/ZomBunny.prefab";

        public static GameObject LoadPlayerPrefab()
        {
            return Load(PlayerResourcePath, PlayerEditorPath);
        }

        public static GameObject LoadMonsterPrefab()
        {
            var go = Load(MonsterResourcePath, MonsterEditorPath);
            if (go != null) return go;
            return Load(MonsterFallbackResourcePath, MonsterFallbackEditorPath);
        }

        public static GameObject InstantiatePlayer(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = LoadPlayerPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[CharacterPrefabLoader] 未找到 Player Prefab: {PlayerResourcePath} / {PlayerEditorPath}");
                return null;
            }
            return Object.Instantiate(prefab, position, rotation, parent);
        }

        public static GameObject InstantiateMonster(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = LoadMonsterPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[CharacterPrefabLoader] 未找到 Monster Prefab: {MonsterResourcePath} 或 fallback ZomBunny");
                return null;
            }
            return Object.Instantiate(prefab, position, rotation, parent);
        }

        static GameObject Load(string resourcesPath, string editorPath)
        {
            var res = Resources.Load<GameObject>(resourcesPath);
            if (res != null) return res;

#if UNITY_EDITOR
            var adType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (adType != null)
            {
                var method = adType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) });
                if (method != null)
                {
                    var prefab = method.Invoke(null, new object[] { editorPath, typeof(GameObject) }) as GameObject;
                    if (prefab != null) return prefab;
                }
            }
#endif
            return null;
        }
    }
}
