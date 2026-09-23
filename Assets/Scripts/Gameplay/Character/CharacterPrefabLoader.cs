using UnityEngine;

namespace GameAct.Gameplay.Character
{
    /// <summary>
    /// 统一加载 Character Prefab。
    /// Monster 只用标准 Dummy（整理后的 prefab），禁止再 fallback ZomBunny。
    /// </summary>
    public static class CharacterPrefabLoader
    {
        public const string PlayerResourcePath = "Character/Player/Y_Bot";
        public const string PlayerEditorPath = "Assets/Bundles/Character/Player/Y_Bot.prefab";

        public const string MonsterResourcePath = "Character/Monster/Dummy";
        public const string MonsterEditorPath = "Assets/Bundles/Character/Monster/Dummy.prefab";

        public static GameObject LoadPlayerPrefab()
        {
            return Load(PlayerResourcePath, PlayerEditorPath);
        }

        public static GameObject LoadMonsterPrefab()
        {
            return Load(MonsterResourcePath, MonsterEditorPath);
        }

        public static GameObject InstantiatePlayer(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = LoadPlayerPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[CharacterPrefabLoader] 未找到 Player Prefab: {PlayerResourcePath}");
                return null;
            }
            return Object.Instantiate(prefab, position, rotation, parent);
        }

        public static GameObject InstantiateMonster(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = LoadMonsterPrefab();
            if (prefab == null)
            {
                Debug.LogError($"[CharacterPrefabLoader] 未找到 Monster Prefab: {MonsterResourcePath}（请确认 Dummy.prefab 已按标准整理）");
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
