namespace GameAct.Spatial
{
    /// <summary>
    /// AOI distance tier for network sync / representation quality.
    /// Near  = full precision (position + velocity + anim + skills)
    /// Mid   = reduced rate / lower precision
    /// Far   = sparse updates or client-side flock only
    /// Swarm = pure client-side group simulation (no net authority)
    /// </summary>
    public enum AoiTier : byte
    {
        None  = 0,
        Near  = 1,
        Mid   = 2,
        Far   = 3,
        Swarm = 4
    }
}
