using UnityEngine;

namespace GameAct.Gameplay.Character
{
    /// <summary>
    /// Monster 表现优先 ZomBunny；Dummy 仅作逻辑/碰撞备用 prefab。
    /// </summary>
    public static class CharacterPrefabLoader
    {
        public const string PlayerResourcePath = "Character/Player/Y_Bot";
        public const string PlayerEditorPath = "Assets/Bundles/Character/Player/Y_Bot.prefab";

        // 正式怪模型
        public const string MonsterResourcePath = "Character/Monster/ZomBunny";
        public const string MonsterEditorPath = "Assets/Bundles/Character/Monster/ZomBunny.prefab";

        // 备用（仅逻辑桩 / 未导 ZomBunny 时）
        public const string MonsterFallbackResourcePath = "Character/Monster/Dummy";
        public const string MonsterFallbackEditorPath = "Assets/Bundles/Character/Monster/Dummy.prefab";

        public static GameObject LoadPlayerPrefab()
        {
            return Load(PlayerResourcePath, PlayerEditorPath);
        }

        public static GameObject LoadMonsterPrefab()
        {
            var go = Load(MonsterResourcePath, MonsterEditorPath);
            if (go != null) return go;
            Debug.LogWarning("[CharacterPrefabLoader] ZomBunny 未找到，fallback Dummy");
            return Load(MonsterFallbackResourcePath, MonsterFallbackEditorPath);
        }

        public static GameObject InstantiatePlayer(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = LoadPlayerPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[CharacterPrefabLoader] 未找到 Player: {PlayerResourcePath}");
                return null;
            }
            return Object.Instantiate(prefab, position, rotation, parent);
        }

        public static GameObject InstantiateMonster(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = LoadMonsterPrefab();
            if (prefab == null)
            {
                Debug.LogError("[CharacterPrefabLoader] ZomBunny / Dummy 都未找到");
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
