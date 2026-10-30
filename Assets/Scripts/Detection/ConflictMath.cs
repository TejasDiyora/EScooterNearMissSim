using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// The two measurements behind every near-miss decision. Both treat each body as a
    /// row of circles on the ground (see <see cref="AgentBase.FootprintCenter"/>).
    /// </summary>
    public static class ConflictMath
    {
        /// <summary>
        /// Free space between two bodies in metres. Zero or negative means they overlap.
        /// </summary>
        public static float Gap(AgentBase a, AgentBase b)
        {
            float radii = a.FootprintRadius + b.FootprintRadius;
            float best = float.MaxValue;
            for (int i = 0; i < a.FootprintCount; i++)
            {
                Vector3 ca = a.FootprintCenter(i);
                for (int j = 0; j < b.FootprintCount; j++)
                {
                    Vector3 cb = b.FootprintCenter(j);
                    float dx = cb.x - ca.x;
                    float dz = cb.z - ca.z;
                    float gap = Mathf.Sqrt(dx * dx + dz * dz) - radii;
                    if (gap < best) best = gap;
                }
            }
            return best;
        }

        /// <summary>
        /// Time-to-collision: seconds until the two bodies touch if both keep their current
        /// velocity. Returns infinity when they are not on a collision course, which is what
        /// separates a real conflict from two road users simply being near each other.
        /// </summary>
        /// <param name="margin">Extra clearance added around the bodies, in metres.</param>
        public static float TimeToCollision(AgentBase a, AgentBase b, float margin)
        {
            Vector3 va = a.Velocity;
            Vector3 vb = b.Velocity;
            float rvx = vb.x - va.x;
            float rvz = vb.z - va.z;
            float speedSquared = rvx * rvx + rvz * rvz;

            float reach = a.FootprintRadius + b.FootprintRadius + margin;
            float best = float.PositiveInfinity;

            for (int i = 0; i < a.FootprintCount; i++)
            {
                Vector3 ca = a.FootprintCenter(i);
                for (int j = 0; j < b.FootprintCount; j++)
                {
                    Vector3 cb = b.FootprintCenter(j);
                    float px = cb.x - ca.x;
                    float pz = cb.z - ca.z;

                    // Solve |p + v t| = reach for the earliest t >= 0.
                    float c = px * px + pz * pz - reach * reach;
                    if (c <= 0f) return 0f;                       // already touching
                    if (speedSquared < 0.000001f) continue;        // not moving relative to each other

                    float halfB = px * rvx + pz * rvz;
                    if (halfB >= 0f) continue;                     // moving apart

                    float discriminant = halfB * halfB - speedSquared * c;
                    if (discriminant < 0f) continue;               // paths miss

                    float t = (-halfB - Mathf.Sqrt(discriminant)) / speedSquared;
                    if (t >= 0f && t < best) best = t;
                }
            }
            return best;
        }

        /// <summary>
        /// Time-to-collision if both agents carry on along their planned paths at their
        /// expected speeds, checked in small time steps up to <paramref name="horizon"/> seconds.
        /// Unlike <see cref="TimeToCollision"/> this sees a turn coming, so it is what a
        /// rider can work out about a car that is indicating.
        /// </summary>
        public static float PredictedTimeToCollision(AgentBase a, AgentBase b, float horizon, float margin)
        {
            const float timeStep = 0.1f;
            float reach = a.FootprintRadius + b.FootprintRadius + margin;
            float reachSquared = reach * reach;
            int steps = Mathf.CeilToInt(horizon / timeStep);

            for (int k = 0; k <= steps; k++)
            {
                float t = k * timeStep;
                float sa = a.DistanceAlong + a.PredictionSpeed * t;
                float sb = b.DistanceAlong + b.PredictionSpeed * t;
                Vector3 pa = a.Path.PointAt(sa);
                Vector3 fa = a.Path.DirectionAt(sa);
                Vector3 pb = b.Path.PointAt(sb);
                Vector3 fb = b.Path.DirectionAt(sb);

                for (int i = 0; i < a.FootprintCount; i++)
                {
                    Vector3 ca = pa + fa * a.FootprintOffset(i);
                    for (int j = 0; j < b.FootprintCount; j++)
                    {
                        Vector3 cb = pb + fb * b.FootprintOffset(j);
                        float dx = cb.x - ca.x;
                        float dz = cb.z - ca.z;
                        if (dx * dx + dz * dz < reachSquared) return t;
                    }
                }
            }
            return float.PositiveInfinity;
        }
    }
}
