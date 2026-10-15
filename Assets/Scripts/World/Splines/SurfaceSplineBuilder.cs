using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLeviathan.Splines
{
    [Serializable]
    public struct SurfaceSplineKnot
    {
        public Vector3 position;
        public Vector3 normal;
        [Min(.01f)] public float width;
        public SurfaceSplineKnot(Vector3 position, Vector3 normal, float width = 1)
        { this.position = position; this.normal = normal; this.width = width; }
    }

    public delegate bool SurfaceProjection(Vector3 seed, Vector3 hintNormal, out Vector3 position, out Vector3 normal);

    /// <summary>Builds a surface ribbon in world coordinates. Projection never modifies the target.</summary>
    public sealed class SurfaceSplineBuilder
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();
        public int MissedSamples { get; private set; }
        public int RejectedTriangles { get; private set; }
        public const int VertexLimit = 250000;

        struct Row { public Vector3 position, normal; public float width; public bool valid; }

        public void Append(IReadOnlyList<SurfaceSplineKnot> knots, bool closed, float width,
            float spacing, int crossSegments, float smoothing, float lift, float uvMetres,
            float maximumDeviation, SurfaceProjection project)
        {
            if (knots == null || knots.Count < (closed ? 3 : 2)) return;
            spacing = Mathf.Max(.05f, spacing); crossSegments = Mathf.Clamp(crossSegments, 1, 64);
            smoothing = Mathf.Clamp01(smoothing); width = Mathf.Max(.01f, width);
            int spans = closed ? knots.Count : knots.Count - 1;
            var rows = new List<Row>();
            for (int span = 0; span < spans; span++)
            {
                float length = 0;
                Vector3 previous = Evaluate(knots, closed, span, 0, smoothing);
                for (int k = 1; k <= 12; k++)
                {
                    Vector3 p = Evaluate(knots, closed, span, k / 12f, smoothing);
                    length += Vector3.Distance(previous, p); previous = p;
                }
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
                if ((long)(rows.Count + steps + 1) * (crossSegments + 1) + vertices.Count > VertexLimit)
                    throw new InvalidOperationException("Spline exceeds 250000 vertices. Increase spacing or split the path.");
                for (int k = 0; k < steps; k++) AddRow(span, k / (float)steps);
            }
            AddRow(spans - 1, 1);
            int start = vertices.Count, stride = crossSegments + 1;
            var valid = new bool[rows.Count * stride];
            float distance = 0;
            for (int r = 0; r < rows.Count; r++)
            {
                Row row = rows[r];
                int before = r == 0 ? (closed ? rows.Count - 2 : 0) : r - 1;
                int after = r == rows.Count - 1 ? (closed ? 1 : r) : r + 1;
                Vector3 tangent = Vector3.ProjectOnPlane(rows[after].position - rows[before].position, row.normal);
                if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.Cross(row.normal, Vector3.right);
                if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.Cross(row.normal, Vector3.forward);
                Vector3 right = Vector3.Cross(row.normal, tangent.normalized).normalized;
                if (r > 0) distance += Vector3.Distance(rows[r - 1].position, row.position);
                for (int c = 0; c <= crossSegments; c++)
                {
                    float u = c / (float)crossSegments;
                    Vector3 seed = row.position + right * ((u - .5f) * width * row.width);
                    bool hit = project(seed, row.normal, out Vector3 point, out Vector3 normal) && row.valid;
                    valid[r * stride + c] = hit;
                    if (!hit) { MissedSamples++; point = row.position; normal = row.normal; }
                    vertices.Add(point + normal * lift); normals.Add(normal);
                    uv.Add(new Vector2(u, distance / Mathf.Max(.01f, uvMetres)));
                }
            }
            for (int r = 0; r < rows.Count - 1; r++)
                for (int c = 0; c < crossSegments; c++)
                {
                    int a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
                    Triangle(a, d, b); Triangle(b, d, e);
                }

            void AddRow(int span, float t)
            {
                var a = knots[span]; var b = knots[(span + 1) % knots.Count];
                Vector3 hint = Vector3.Slerp(a.normal.normalized, b.normal.normalized, t).normalized;
                if (hint.sqrMagnitude < .5f) hint = Vector3.up;
                Vector3 seed = Evaluate(knots, closed, span, t, smoothing);
                bool hit = project(seed, hint, out Vector3 p, out Vector3 n);
                rows.Add(new Row { position = hit ? p : seed, normal = hit ? n : hint,
                    width = Mathf.Max(.01f, Mathf.Lerp(a.width, b.width, t)), valid = hit });
            }
            void Triangle(int a, int b, int c)
            {
                if (!valid[a] || !valid[b] || !valid[c]) { RejectedTriangles++; return; }
                a += start; b += start; c += start;
                Vector3 ab = vertices[b] - vertices[a], ac = vertices[c] - vertices[a];
                Vector3 face = Vector3.Cross(ab, ac), normal = (normals[a] + normals[b] + normals[c]).normalized;
                if (face.sqrMagnitude < 1e-12f) return;
                // Reject triangles spanning holes, folds or disconnected surfaces. Finer sampling
                // is necessary at sharp corners; filling an unsupported gap would create a bridge.
                Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3 - normal * lift;
                if (Vector3.Dot(normals[a], normals[b]) < 0 || Vector3.Dot(normals[a], normals[c]) < 0 ||
                    !project(center, normal, out Vector3 projected, out Vector3 _) ||
                    Vector3.Distance(center, projected) > Mathf.Max(.005f, maximumDeviation))
                { RejectedTriangles++; return; }
                triangles.Add(a);
                if (Vector3.Dot(face, normal) >= 0) { triangles.Add(b); triangles.Add(c); }
                else { triangles.Add(c); triangles.Add(b); }
            }
        }

        // Hermite form of an interpolating Catmull-Rom spline, with adjustable tangent strength.
        public static Vector3 Evaluate(IReadOnlyList<SurfaceSplineKnot> knots, bool closed, int span, float t, float smoothing)
        {
            int n = knots.Count;
            Vector3 P(int i) => knots[closed ? (i % n + n) % n : Mathf.Clamp(i, 0, n - 1)].position;
            Vector3 a = P(span), b = P(span + 1);
            Vector3 m0 = (P(span + 1) - P(span - 1)) * (.5f * smoothing);
            Vector3 m1 = (P(span + 2) - P(span)) * (.5f * smoothing);
            float t2 = t * t, t3 = t2 * t;
            return (2*t3 - 3*t2 + 1)*a + (t3 - 2*t2 + t)*m0 + (-2*t3 + 3*t2)*b + (t3 - t2)*m1;
        }

        public Mesh CreateMesh(Matrix4x4 worldToLocal, string name)
        {
            var localVertices = new List<Vector3>(vertices.Count);
            var localNormals = new List<Vector3>(normals.Count);
            Matrix4x4 normalMatrix = worldToLocal.inverse.transpose;
            foreach (Vector3 v in vertices) localVertices.Add(worldToLocal.MultiplyPoint3x4(v));
            foreach (Vector3 n in normals) localNormals.Add(normalMatrix.MultiplyVector(n).normalized);
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(localVertices); mesh.SetNormals(localNormals); mesh.SetUVs(0, uv);
            if (worldToLocal.determinant < 0)
            {
                var reversed = new List<int>(triangles);
                for (int i = 0; i < reversed.Count; i += 3) (reversed[i + 1], reversed[i + 2]) = (reversed[i + 2], reversed[i + 1]);
                mesh.SetTriangles(reversed, 0);
            }
            else mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
