using System.Collections.Generic;
using UnityEngine;

namespace TMPTextOnPath
{
    /// <summary>
    /// Sampled polyline with an arc-length table, used to evaluate position and tangent
    /// at a given distance along the path.
    /// </summary>
    public sealed class PathSampler
    {
        readonly List<Vector2> m_Points = new List<Vector2>();
        readonly List<Vector2> m_Tangents = new List<Vector2>();
        readonly List<float> m_Distances = new List<float>();

        public bool IsClosed { get; private set; }
        public float Length { get; private set; }
        /// <summary>Number of sampled points. A closed path repeats its first point at the end.</summary>
        public int Count => m_Points.Count;

        public Vector2 GetPoint(int index) => m_Points[index];

        /// <summary>
        /// Builds the path. For a closed path, the first point must not be repeated at the end.
        /// </summary>
        public void Build(List<Vector2> points, bool closed)
        {
            m_Points.Clear();
            m_Tangents.Clear();
            m_Distances.Clear();
            Length = 0f;

            int n = points.Count;
            IsClosed = closed && n > 2;
            if (n == 0)
                return;

            Vector2 lastTangent = Vector2.right;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = IsClosed ? points[(i - 1 + n) % n] : points[Mathf.Max(i - 1, 0)];
                Vector2 next = IsClosed ? points[(i + 1) % n] : points[Mathf.Min(i + 1, n - 1)];
                Vector2 tangent = next - prev;
                if (tangent.sqrMagnitude < 1e-12f)
                    tangent = lastTangent;
                else
                    tangent.Normalize();
                lastTangent = tangent;

                m_Points.Add(points[i]);
                m_Tangents.Add(tangent);
            }

            if (IsClosed)
            {
                m_Points.Add(points[0]);
                m_Tangents.Add(m_Tangents[0]);
            }

            float distance = 0f;
            m_Distances.Add(0f);
            for (int i = 1; i < m_Points.Count; i++)
            {
                distance += Vector2.Distance(m_Points[i - 1], m_Points[i]);
                m_Distances.Add(distance);
            }
            Length = distance;
        }

        /// <summary>
        /// Evaluates the path at the given distance. Returns false when the point is off the path
        /// and <paramref name="overflow"/> is <see cref="PathOverflow.Hide"/>.
        /// </summary>
        public bool Evaluate(float distance, PathOverflow overflow, out Vector2 position, out Vector2 tangent)
        {
            int count = m_Points.Count;
            if (count == 0)
            {
                position = Vector2.zero;
                tangent = Vector2.right;
                return false;
            }

            if (count == 1 || Length <= 1e-6f)
            {
                position = m_Points[0];
                tangent = m_Tangents[0];
                return true;
            }

            if (IsClosed)
            {
                distance = Mathf.Repeat(distance, Length);
            }
            else if (distance < 0f)
            {
                tangent = m_Tangents[0];
                position = m_Points[0] + tangent * distance;
                return overflow == PathOverflow.Extrapolate;
            }
            else if (distance > Length)
            {
                tangent = m_Tangents[count - 1];
                position = m_Points[count - 1] + tangent * (distance - Length);
                return overflow == PathOverflow.Extrapolate;
            }

            int lo = 0, hi = count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (m_Distances[mid] <= distance)
                    lo = mid;
                else
                    hi = mid;
            }

            float segmentLength = m_Distances[hi] - m_Distances[lo];
            float f = segmentLength > 1e-6f ? (distance - m_Distances[lo]) / segmentLength : 0f;
            position = Vector2.LerpUnclamped(m_Points[lo], m_Points[hi], f);
            tangent = Vector2.Lerp(m_Tangents[lo], m_Tangents[hi], f);
            if (tangent.sqrMagnitude < 1e-12f)
                tangent = m_Tangents[lo];
            tangent.Normalize();
            return true;
        }

        /// <summary>Distance along the path of the point closest to <paramref name="point"/>.</summary>
        public float GetClosestDistance(Vector2 point)
        {
            float bestSqr = float.MaxValue;
            float bestDistance = 0f;
            for (int i = 0; i < m_Points.Count - 1; i++)
            {
                Vector2 a = m_Points[i];
                Vector2 ab = m_Points[i + 1] - a;
                float lengthSqr = ab.sqrMagnitude;
                float t = lengthSqr > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr) : 0f;
                float sqr = (point - (a + ab * t)).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestDistance = Mathf.Lerp(m_Distances[i], m_Distances[i + 1], t);
                }
            }
            return bestDistance;
        }
    }
}
