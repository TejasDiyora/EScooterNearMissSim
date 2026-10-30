using System.Collections.Generic;
using UnityEngine;

namespace EScooterSim
{
    /// <summary>
    /// A polyline on the ground that an agent follows.
    /// Positions are addressed by distance travelled along the line, in metres.
    /// </summary>
    public class RoutePath
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> cumulative = new List<float>();

        public float Length { get; private set; }
        public int PointCount { get { return points.Count; } }

        public RoutePath(IList<Vector3> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                Vector3 p = source[i];
                p.y = 0f;
                if (points.Count > 0 && (p - points[points.Count - 1]).sqrMagnitude < 0.0001f) continue;
                points.Add(p);
            }
            if (points.Count == 0) points.Add(Vector3.zero);
            if (points.Count == 1) points.Add(points[0] + new Vector3(0.01f, 0f, 0f));

            cumulative.Add(0f);
            for (int i = 1; i < points.Count; i++)
            {
                cumulative.Add(cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]));
            }
            Length = cumulative[cumulative.Count - 1];
        }

        public Vector3 GetPoint(int index)
        {
            return points[index];
        }

        private int SegmentIndex(float distance)
        {
            int last = points.Count - 2;
            for (int i = 0; i < last; i++)
            {
                if (distance < cumulative[i + 1]) return i;
            }
            return last;
        }

        public Vector3 PointAt(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int i = SegmentIndex(distance);
            float segmentLength = cumulative[i + 1] - cumulative[i];
            float t = segmentLength > 0.0001f ? (distance - cumulative[i]) / segmentLength : 0f;
            return Vector3.Lerp(points[i], points[i + 1], t);
        }

        public Vector3 DirectionAt(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int i = SegmentIndex(distance);
            Vector3 d = points[i + 1] - points[i];
            d.y = 0f;
            return d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// Finds the point on the path closest to <paramref name="point"/>, looking only at the
        /// stretch from <paramref name="fromDistance"/> to <paramref name="fromDistance"/> + <paramref name="lookahead"/>.
        /// </summary>
        /// <param name="along">Distance along the path of that closest point.</param>
        /// <param name="lateral">How far the point is from the path.</param>
        public bool ProjectAhead(Vector3 point, float fromDistance, float lookahead, out float along, out float lateral)
        {
            along = 0f;
            lateral = float.MaxValue;
            point.y = 0f;

            float from = Mathf.Clamp(fromDistance, 0f, Length);
            float to = Mathf.Min(Length, from + Mathf.Max(0f, lookahead));
            bool found = false;

            for (int i = 0; i < points.Count - 1; i++)
            {
                float segmentStart = cumulative[i];
                float segmentEnd = cumulative[i + 1];
                if (segmentEnd < from) continue;
                if (segmentStart > to) break;

                float segmentLength = segmentEnd - segmentStart;
                if (segmentLength < 0.0001f) continue;

                Vector3 a = points[i];
                Vector3 direction = (points[i + 1] - a) / segmentLength;
                float tMin = Mathf.Max(0f, from - segmentStart);
                float tMax = Mathf.Min(segmentLength, to - segmentStart);
                float t = Mathf.Clamp(Vector3.Dot(point - a, direction), tMin, tMax);
                float distance = Vector3.Distance(point, a + direction * t);

                if (distance < lateral - 0.0001f)
                {
                    lateral = distance;
                    along = segmentStart + t;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Distance along the path of the point on it closest to <paramref name="point"/>.</summary>
        public float DistanceAlongOf(Vector3 point)
        {
            float along;
            float lateral;
            ProjectAhead(point, 0f, Length, out along, out lateral);
            return along;
        }
    }
}
