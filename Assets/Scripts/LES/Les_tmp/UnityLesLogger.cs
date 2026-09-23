using UnityEngine;

namespace GameAct.Les
{
    public sealed class UnityLesLogger : LiteEntitySystem.ILogger
    {
        public void Log(string log) => Debug.Log("[LES] " + log);
        public void LogWarning(string log) => Debug.LogWarning("[LES] " + log);
        public void LogError(string log) => Debug.LogError("[LES] " + log);
    }
}
