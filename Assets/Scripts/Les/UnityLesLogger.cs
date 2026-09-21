using UnityEngine;
using LiteEntitySystem;

namespace GameAct.Les
{
    /// <summary>显式实现 LiteEntitySystem.ILogger，避免与 UnityEngine.ILogger 冲突。</summary>
    public sealed class UnityLesLogger : LiteEntitySystem.ILogger
    {
        public void Log(string log) => Debug.Log("[LES] " + log);
        public void LogWarning(string log) => Debug.LogWarning("[LES] " + log);
        public void LogError(string log) => Debug.LogError("[LES] " + log);
    }
}
