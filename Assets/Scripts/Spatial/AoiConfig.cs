using UnityEngine;

namespace GameAct.Spatial
{
    /// <summary>
    /// Tunable radii / hysteresis for AOI layers.
    /// Distances are on XZ plane (Y ignored for tier decisions).
    /// Hysteresis prevents rapid tier flapping when entities sit on boundaries.
    /// </summary>
    [System.Serializable]
    public struct AoiConfig
    {
        [Tooltip("Full-precision sync radius")]
        public float NearRadius;

        [Tooltip("Reduced-rate sync radius")]
        public float MidRadius;

        [Tooltip("Sparse / client-flock radius")]
        public float FarRadius;

        [Tooltip("Extra band to enter a closer tier (prevents flapping)")]
        public float EnterHysteresis;

        [Tooltip("Extra band to leave a closer tier")]
        public float LeaveHysteresis;

        [Tooltip("Layer mask used for AOI queries")]
        public SpatialLayer QueryLayerMask;

        public static AoiConfig Default => new AoiConfig
        {
            NearRadius       = 25f,
            MidRadius        = 55f,
            FarRadius        = 120f,
            EnterHysteresis  = 2f,
            LeaveHysteresis  = 4f,
            QueryLayerMask   = SpatialLayer.All
        };

        public float MaxRadius => FarRadius;
    }
}
