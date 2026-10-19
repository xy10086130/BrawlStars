
public static class PlayerMoveStep
{
    public delegate bool BlockedFn(float x, float z, float r);

    public static void Step(float px, float pz, float dx, float dz, float step, float radius,
                            BlockedFn blocked, out float nx, out float nz)
    {
        nx = px;
        nz = pz;
        if (dx != 0f && !blocked(px + dx * step, pz, radius))
            nx = px + dx * step;
        if (dz != 0f && !blocked(nx, pz + dz * step, radius))
            nz = pz + dz * step;
    }
}
