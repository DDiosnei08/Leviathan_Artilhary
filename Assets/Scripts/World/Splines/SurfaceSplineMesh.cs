using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.Splines
{
    public enum SurfaceProjectionMode { Directional, Radial, KnotNormals }

    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SurfaceSplineMesh : MonoBehaviour
    {
        [Header("Path (local coordinates)")]
        public List<SurfaceSplineKnot> knots = new List<SurfaceSplineKnot> {
            new SurfaceSplineKnot(new Vector3(-5, 1, 0), Vector3.up),
            new SurfaceSplineKnot(new Vector3(0, 1, 3), Vector3.up),
            new SurfaceSplineKnot(new Vector3(5, 1, 0), Vector3.up) };
        public bool closed;
        [Range(0, 1)] public float smoothing = 1;
        [Min(.01f)] public float width = 2;
        [Min(.05f)] public float sampleSpacing = .25f;
        [Range(1, 64)] public int widthSegments = 8;
        [Min(0)] public float surfaceOffset = .025f;
        [Min(.005f)] public float maximumSurfaceError = .08f;
        [Min(.01f)] public float textureMetres = 4;
        [Header("Projection")]
        public SurfaceProjectionMode projection;
        public Vector3 direction = Vector3.down;
        public Transform radialCenter;
        [Min(.1f)] public float probeAbove = 50;
        [Min(.1f)] public float probeBelow = 50;
        public LayerMask surfaceLayers = ~0;
        [Tooltip("An explicit list avoids hitting unrelated objects. Empty uses the layer mask.")]
        public Collider[] surfaces = Array.Empty<Collider>();
        [Header("Output")]
        public Material material;
        public bool generateCollider;
        public bool autoRebuild = true;
        public Mesh GeneratedMesh => ownedMesh;
        public int MissedSamples { get; private set; }
        public int RejectedTriangles { get; private set; }
        public string LastError { get; private set; }
        Mesh ownedMesh;
        MeshCollider ownedCollider;
        MeshFilter filter;
        MeshRenderer meshRenderer;
        RaycastHit[] hits = new RaycastHit[32];
        bool dirty = true;
        Matrix4x4 lastMatrix;
        int lastSurfaceState;

        void OnEnable() { dirty = true; }
        void OnValidate() { dirty = true; }
        public void RequestRebuild() { dirty = true; }
        void Update()
        {
            if (autoRebuild && (dirty || transform.localToWorldMatrix != lastMatrix || SurfaceState() != lastSurfaceState)) Rebuild();
        }
        int SurfaceState()
        {
            unchecked
            {
                int hash = radialCenter != null ? radialCenter.position.GetHashCode() : 0;
                if (surfaces != null) foreach (var surface in surfaces) if (surface != null)
                    hash = hash * 397 ^ surface.bounds.GetHashCode() ^ surface.transform.localToWorldMatrix.GetHashCode() ^ (surface.enabled ? 1 : 0);
                return hash;
            }
        }

        [ContextMenu("Rebuild surface spline")]
        public void Rebuild()
        {
            if (!isActiveAndEnabled) return;
            dirty = false; LastError = null; lastMatrix = transform.localToWorldMatrix;
            filter = GetComponent<MeshFilter>(); meshRenderer = GetComponent<MeshRenderer>();
            if (knots == null || knots.Count < (closed ? 3 : 2)) { Fail("At least two knots (three when closed) are required."); return; }
            if (Mathf.Abs(lastMatrix.determinant) < 1e-8f) { Fail("Transform scale cannot be zero."); return; }
            if (projection == SurfaceProjectionMode.Directional && direction.sqrMagnitude < 1e-8f)
            { Fail("Projection direction cannot be zero."); return; }
            // Rebuild runs on the main thread, never in OnValidate's import callback.
            Physics.SyncTransforms();
            lastSurfaceState = SurfaceState();
            var world = new List<SurfaceSplineKnot>(knots.Count);
            Matrix4x4 normals = transform.worldToLocalMatrix.transpose;
            foreach (var knot in knots) world.Add(new SurfaceSplineKnot(transform.TransformPoint(knot.position),
                normals.MultiplyVector(knot.normal).normalized, knot.width));
            var builder = new SurfaceSplineBuilder();
            try
            {
                builder.Append(world, closed, width, sampleSpacing, widthSegments, smoothing,
                    surfaceOffset, textureMetres, maximumSurfaceError, Project);
                Mesh next = builder.CreateMesh(transform.worldToLocalMatrix, "Projected Spline (generated)");
                next.hideFlags = HideFlags.DontSave;
                ReleaseMesh(); ownedMesh = next; filter.sharedMesh = next;
                if (material != null) meshRenderer.sharedMaterial = material;
                if (generateCollider)
                {
                    if (ownedCollider == null)
                    {
                        ownedCollider = gameObject.AddComponent<MeshCollider>();
                        ownedCollider.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                    }
                    ownedCollider.sharedMesh = next.vertexCount > 0 && next.GetIndexCount(0) > 0 ? next : null;
                }
                else ReleaseCollider();
                MissedSamples = builder.MissedSamples; RejectedTriangles = builder.RejectedTriangles;
            }
            catch (Exception e) { Fail(e.Message); }
        }

        public bool Project(Vector3 seed, Vector3 hint, out Vector3 position, out Vector3 normal)
        {
            Vector3 rayDirection = projection == SurfaceProjectionMode.Radial
                ? (radialCenter != null ? radialCenter.position : transform.position) - seed
                : projection == SurfaceProjectionMode.KnotNormals ? -hint : direction;
            position = seed; normal = hint;
            if (rayDirection.sqrMagnitude < 1e-8f) return false;
            rayDirection.Normalize();
            var ray = new Ray(seed - rayDirection * Mathf.Max(.1f, probeAbove), rayDirection);
            float maxDistance = Mathf.Max(.1f, probeAbove) + Mathf.Max(.1f, probeBelow);
            float best = float.PositiveInfinity; RaycastHit nearest = default;
            if (surfaces != null && surfaces.Length > 0)
            {
                foreach (var surface in surfaces)
                    if (Eligible(surface) && surface.Raycast(ray, out RaycastHit hit, maxDistance) && hit.distance < best)
                    { best = hit.distance; nearest = hit; }
            }
            else
            {
                int count;
                while ((count = Physics.RaycastNonAlloc(ray, hits, maxDistance, surfaceLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
                {
                    if (hits.Length >= 4096) return false;
                    Array.Resize(ref hits, hits.Length * 2);
                }
                for (int i = 0; i < count; i++) if (Eligible(hits[i].collider) && hits[i].distance < best)
                { best = hits[i].distance; nearest = hits[i]; }
            }
            if (float.IsPositiveInfinity(best)) return false;
            position = nearest.point; normal = nearest.normal.normalized;
            return normal.sqrMagnitude > .5f;
        }
        bool Eligible(Collider c) => c != null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
            c != ownedCollider && c.transform != transform && !c.transform.IsChildOf(transform) &&
            (surfaceLayers.value & (1 << c.gameObject.layer)) != 0;
        void Fail(string message) { LastError = message; ReleaseMesh(); }
        void ReleaseMesh()
        {
            if (ownedCollider != null) ownedCollider.sharedMesh = null;
            if (ownedMesh == null) return;
            if (filter != null && filter.sharedMesh == ownedMesh) filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
            ownedMesh = null;
        }
        void ReleaseCollider()
        {
            if (ownedCollider == null) return;
            if (Application.isPlaying) Destroy(ownedCollider); else DestroyImmediate(ownedCollider);
            ownedCollider = null;
        }
        void OnDisable() { ReleaseMesh(); ReleaseCollider(); }
        void OnDestroy() { ReleaseMesh(); ReleaseCollider(); }
    }
}
