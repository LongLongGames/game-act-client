using UnityEngine;

namespace GameAct.Skill
{
    /// <summary>
    /// Single hit result returned by HitSystem.
    /// </summary>
    public struct HitResult
    {
        public int TargetEntityId;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public float Distance;
        public bool IsCritical;         // reserved
        public int ColliderIndex;       // which logic collider was hit (0 = primary)
    }
}