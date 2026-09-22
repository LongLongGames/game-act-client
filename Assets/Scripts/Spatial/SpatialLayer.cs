namespace GameAct.Spatial
{
    /// <summary>
    /// Spatial layer for filtering queries and insertions.
    /// Extend as needed (e.g. FarSwarm, Obstacle).
    /// </summary>
    [System.Flags]
    public enum SpatialLayer
    {
        None       = 0,
        Ground     = 1 << 0,   // Ground monsters, players, low objects
        LowAerial  = 1 << 1,   // Wisps, low-flying units
        Aerial     = 1 << 2,   // High-density swarms (special handling)
        All        = Ground | LowAerial | Aerial
    }
}