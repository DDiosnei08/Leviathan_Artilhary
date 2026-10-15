using System;
using System.Collections.Generic;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace ProjectLeviathan.Grass
{
    [DefaultExecutionOrder(150)]
    public sealed class GrassManager : MonoBehaviour
    {
        public TerrainWorldController terrain;
        public GrassConfiguration configuration;
        public Camera renderCamera;
        public Transform player;
        public bool rendering = true;
        public bool UsingGraphicsJobsFallback { get; private set; }
        public GrassExclusionVolume[] exclusionVolumes = Array.Empty<GrassExclusionVolume>();
        public int ActiveTiles => tiles.Count;
        public int ResidentPatches => pool != null ? pool.Leased : 0;
        public uint GeneratedBlades { get; private set; }
        public uint VisibleBlades { get; private set; }
        public long BufferBytes => pool != null ? pool.Bytes + tiles.Count * 16384L : 0;
        public long TextureBytes { get; private set; }
        public long LastUpdateAllocatedBytes { get; private set; }
        public readonly int[] tilesByLod = new int[4];
        readonly Dictionary<TerrainChunk, GrassTile> tiles = new Dictionary<TerrainChunk, GrassTile>(32);
        readonly List<Vector2Int> remove = new List<Vector2Int>(128);
        readonly Stack<GrassPatch> freePatches = new Stack<GrassPatch>(128);
        readonly Plane[] planes = new Plane[6];
        readonly Vector4[] frustum = new Vector4[6];
        readonly uint[] zero = { 0 };
        readonly Mesh[] meshes = new Mesh[3];
        static readonly ProfilerMarker marker = new ProfilerMarker("Leviathan.Grass.Update");
        GrassBufferPool pool;
        Material material;
        ComputeShader compute;
        int generateKernel, cullKernel;
        float nextStatistics;
        GrassBuffers statisticsBuffer;
        int statisticsVersion;
        Action<AsyncGPUReadbackRequest> countCallback, generatedCallback;
        bool initialized;
        int candidateCount, gridResolution;
        float residentPatchSize;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (configuration == null || configuration.compute == null || configuration.material == null || !SystemInfo.supportsComputeShaders)
            { Debug.LogWarning("Grass needs a configuration, compute shader, material and a compute-capable GPU.", this); return; }
            compute = Instantiate(configuration.compute);
            material = new Material(configuration.material) { enableInstancing = true };
            RenderingThreadingMode threading = SystemInfo.renderingThreadingMode;
            UsingGraphicsJobsFallback = Application.unityVersion.StartsWith("6000.6.", StringComparison.Ordinal) &&
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 &&
                threading != RenderingThreadingMode.Direct && threading != RenderingThreadingMode.MultiThreaded && threading != RenderingThreadingMode.SingleThreaded;
            if (UsingGraphicsJobsFallback)
            {
                material.EnableKeyword("LEVIATHAN_LEGACY_INDIRECT");
                Debug.LogWarning("Grass: using the DX11 graphics-jobs compatibility draw path on Unity 6.6 (RenderMeshIndirect crashed in this project's standalone validation).", this);
            }
            generateKernel = compute.FindKernel("Generate"); cullKernel = compute.FindKernel("Cull");
            meshes[0] = GrassBladeMesh.Create(5); meshes[1] = GrassBladeMesh.Create(3); meshes[2] = GrassBladeMesh.Create(1);
            candidateCount = configuration.CandidateCount; gridResolution = configuration.GridResolution;
            float tileSize = terrain != null ? terrain.terrainSettings.chunkSize : configuration.patchSize;
            residentPatchSize = tileSize / Mathf.Max(1, Mathf.RoundToInt(tileSize / configuration.patchSize));
            pool = new GrassBufferPool(candidateCount, meshes);
            // Pay managed/native buffer setup once on enable, not while streaming nearby patches.
            var warm = new GrassBuffers[configuration.maximumResidentPatches];
            for (int i = 0; i < warm.Length; i++) { warm[i] = pool.Rent(); freePatches.Push(new GrassPatch()); }
            for (int i = 0; i < warm.Length; i++) pool.Return(warm[i]);
            countCallback = CountsReceived; generatedCallback = GeneratedReceived;
            initialized = true;
            if (terrain != null)
            {
                terrain.ChunkCreated += AttachChunk;
                foreach (TerrainChunk chunk in terrain.TerrainChunks.Values) AttachChunk(chunk);
            }
            if (renderCamera == null) renderCamera = Camera.main;
            ConfigureMaterial();
        }
        public void AttachChunk(TerrainChunk chunk)
        {
            if (initialized && !tiles.ContainsKey(chunk)) tiles.Add(chunk, new GrassTile(chunk, this));
        }
        public void DetachChunk(TerrainChunk chunk)
        {
            if (!tiles.TryGetValue(chunk, out GrassTile tile)) return;
            tile.Dispose(); tiles.Remove(chunk);
        }
        internal void Return(GrassPatch patch)
        {
            pool.Return(patch.buffers); patch.buffers = null; patch.generated = false; freePatches.Push(patch);
        }
        public void Rebuild()
        {
            if (candidateCount != configuration.CandidateCount ||
                Mathf.Abs(residentPatchSize - configuration.patchSize) > 0.01f)
            { OnDisable(); OnEnable(); return; }
            foreach (GrassTile tile in tiles.Values) tile.dirty = true;
        }
        void ConfigureMaterial()
        {
            GrassConfiguration c = configuration;
            material.SetColor("_RootColor", c.rootColor); material.SetColor("_TipColor", c.tipColor);
            material.SetVector("_Appearance", new Vector4(c.biomeColorInfluence, c.translucency, c.specular, c.roundedNormals));
            material.SetVector("_LodDistances", new Vector4(c.nearDistance, c.middleDistance, c.farDistance, c.transitionWidth));
            material.SetVector("_DensityLods", new Vector4(c.middleDensity, c.farDensity, 0, 0));
            material.SetVector("_WidthScales", new Vector4(c.widerDistantBlades.x, c.widerDistantBlades.y, 0, 0));
            material.SetFloat("_ViewThickening", c.viewThickening);
            Vector2 direction = c.windDirection.normalized;
            float speed = c.windSpeed;
            if (c.useWorldWeather && terrain != null && terrain.weatherProvider != null)
            {
                var weather = terrain.weatherProvider.SampleAt(renderCamera != null ? renderCamera.transform.position : transform.position);
                direction = new Vector2(weather.WindDirection.x, weather.WindDirection.z); speed = weather.WindSpeedMetersPerSecond;
            }
            material.SetVector("_Wind", new Vector4(direction.x, direction.y, speed, c.windStrength));
            material.SetFloat("_WindNoiseScale", c.windNoiseScale);
            Vector3 p = player != null ? player.position : Vector3.one * 100000;
            material.SetVector("_Player", new Vector4(p.x, p.y, p.z, c.playerInteraction ? c.playerRadius : 0));
            material.SetFloat("_UseCulling", c.culling ? 1 : 0);
            material.SetFloat("_ShadowDistance", c.shadowDistance);
        }
        void LateUpdate()
        {
            if (!initialized || !rendering) return;
            long before = GC.GetAllocatedBytesForCurrentThread();
            using (marker.Auto()) Tick();
            LastUpdateAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        void Tick()
        {
            if (terrain != null && terrain.viewer != null && terrain.viewer.TryGetComponent(out Camera current) && current.isActiveAndEnabled)
                renderCamera = current;
            if (renderCamera == null || !renderCamera.isActiveAndEnabled) return;
            ConfigureMaterial();
            GeometryUtility.CalculateFrustumPlanes(renderCamera, planes);
            for (int i = 0; i < 6; i++) frustum[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            compute.SetVectorArray("_Frustum", frustum);
            Vector3 camera = renderCamera.transform.position;
            compute.SetVector("_CameraPosition", camera);
            compute.SetInt("_UseCulling", configuration.culling ? 1 : 0);
            compute.SetInt("_CastShadows", configuration.castNearShadows ? 1 : 0);
            compute.SetFloat("_ShadowDistance", configuration.shadowDistance);
            compute.SetVector("_LodDistances", new Vector4(configuration.nearDistance, configuration.middleDistance, configuration.farDistance, configuration.transitionWidth));
            compute.SetVector("_DensityLods", new Vector4(configuration.middleDensity, configuration.farDensity, 0, 0));
            int uploads = configuration.uploadsPerFrame, generations = configuration.generationsPerFrame;
            GeneratedBlades = 0; VisibleBlades = 0; TextureBytes = 0;
            Array.Clear(tilesByLod, 0, 4);
            // Return distant leases first, so camera teleports reuse the same bounded pool.
            foreach (GrassTile tile in tiles.Values)
            {
                TextureBytes += tile.TextureBytes;
                remove.Clear();
                foreach (var item in tile.patches)
                    if (FlatDistance(item.Value.bounds, camera) > configuration.farDistance + residentPatchSize || tile.terrain.AppliedLod >= 3)
                        remove.Add(item.Key);
                for (int i = 0; i < remove.Count; i++) { Return(tile.patches[remove[i]]); tile.patches.Remove(remove[i]); }
            }
            foreach (GrassTile tile in tiles.Values)
            {
                TerrainChunk chunk = tile.terrain;
                if (chunk.GameObject == null || !chunk.GameObject.activeInHierarchy || chunk.GeneratedData == null) continue;
                tilesByLod[Mathf.Clamp(chunk.AppliedLod, 0, 3)]++;
                Vector3 local = chunk.GameObject.transform.InverseTransformPoint(camera);
                float size = chunk.Settings.chunkSize;
                if (local.x < -configuration.farDistance || local.z < -configuration.farDistance ||
                    local.x > size + configuration.farDistance || local.z > size + configuration.farDistance || chunk.AppliedLod >= 3) continue;
                if (tile.dirty && uploads > 0 && (chunk.WorldFeaturesReady || terrain == null || !terrain.useFiniteMap))
                {
                    tile.Upload(configuration, exclusionVolumes); uploads--;
                    foreach (GrassPatch p in tile.patches.Values) p.generated = false;
                }
                if (tile.uploadedMap == null) continue;
                BindTile(tile);
                float patchSize = residentPatchSize;
                int minX = Mathf.Max(0, Mathf.FloorToInt((local.x - configuration.farDistance) / patchSize));
                int minZ = Mathf.Max(0, Mathf.FloorToInt((local.z - configuration.farDistance) / patchSize));
                int maxX = Mathf.Min(Mathf.CeilToInt(size / patchSize) - 1, Mathf.FloorToInt((local.x + configuration.farDistance) / patchSize));
                int maxZ = Mathf.Min(Mathf.CeilToInt(size / patchSize) - 1, Mathf.FloorToInt((local.z + configuration.farDistance) / patchSize));
                // Near-first concentric rings prevent the outer edge consuming the frame budget.
                int cx = Mathf.Clamp(Mathf.FloorToInt(local.x / patchSize), minX, Mathf.Max(minX, maxX));
                int cz = Mathf.Clamp(Mathf.FloorToInt(local.z / patchSize), minZ, Mathf.Max(minZ, maxZ));
                int rings = Mathf.CeilToInt(configuration.farDistance / patchSize) + 2;
                for (int ring = 0; ring <= rings; ring++)
                for (int z = Mathf.Max(minZ, cz - ring); z <= Mathf.Min(maxZ, cz + ring); z++)
                for (int x = Mathf.Max(minX, cx - ring); x <= Mathf.Min(maxX, cx + ring); x++)
                {
                    if (Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(z - cz)) != ring) continue;
                    Vector2Int key = new Vector2Int(x, z);
                    if (!tile.patches.TryGetValue(key, out GrassPatch patch))
                    {
                        Vector2 origin = new Vector2(x * patchSize, z * patchSize);
                        Bounds b = chunk.MeshFilter.sharedMesh.bounds;
                        Bounds patchBounds = new Bounds(chunk.GameObject.transform.TransformPoint(new Vector3(origin.x + patchSize * 0.5f, b.center.y, origin.y + patchSize * 0.5f)),
                            new Vector3(patchSize + 2, b.size.y + 4, patchSize + 2));
                        // Do not repeatedly generate rectangle corners that distance eviction returns next frame.
                        if (FlatDistance(patchBounds, camera) > configuration.farDistance) continue;
                        if (pool.Leased >= configuration.maximumResidentPatches || generations <= 0) continue;
                        patch = freePatches.Count > 0 ? freePatches.Pop() : new GrassPatch();
                        patch.buffers = pool.Rent(); patch.origin = origin;
                        int side = Mathf.RoundToInt(size / patchSize);
                        patch.global = new Vector2Int(chunk.Coordinate.x * side + x, chunk.Coordinate.y * side + z);
                        patch.bounds = patchBounds;
                        tile.patches.Add(key, patch);
                    }
                    if (!patch.generated)
                    {
                        if (generations <= 0) continue;
                        Generate(tile, patch); generations--;
                    }
                    Render(tile, patch);
                    GeneratedBlades += patch.buffers.generatedCount; VisibleBlades += patch.buffers.visibleCount;
                }
            }
            if (configuration.collectGpuStatistics && SystemInfo.supportsAsyncGPUReadback && Time.unscaledTime >= nextStatistics && statisticsBuffer == null)
            {
                nextStatistics = Time.unscaledTime + 0.05f;
                GrassPatch oldest = null;
                foreach (GrassTile tile in tiles.Values)
                {
                    foreach (GrassPatch patch in tile.patches.Values)
                    {
                        if (!patch.generated || patch.buffers.readbackPending) continue;
                        if (Time.unscaledTime - patch.birth < 0.2f) continue;
                        if (oldest == null || patch.lastStatistics < oldest.lastStatistics) oldest = patch;
                    }
                }
                if (oldest != null)
                {
                    oldest.lastStatistics = Time.unscaledTime;
                    statisticsBuffer = oldest.buffers; statisticsVersion = statisticsBuffer.version;
                    statisticsBuffer.readbackPending = true;
                    AsyncGPUReadback.Request(statisticsBuffer.args, countCallback);
                }
            }
        }
        static float FlatDistance(Bounds b, Vector3 p) { p.y = b.center.y; return Mathf.Sqrt(b.SqrDistance(p)); }
        void BindTile(GrassTile tile)
        {
            TerrainChunk t = tile.terrain;
            Vector2 target = new Vector2(t.AppliedLod >= 1 ? 1 : 0, t.AppliedLod >= 2 ? 1 : 0);
            if (tile.appliedLod != t.AppliedLod)
            {
                tile.lodFrom = tile.appliedLod < 0 ? target : tile.lodBlend;
                tile.lodStarted = Time.unscaledTime; tile.appliedLod = t.AppliedLod;
            }
            tile.lodBlend = Vector2.Lerp(tile.lodFrom, target, Mathf.SmoothStep(0, 1, (Time.unscaledTime - tile.lodStarted) / 0.4f));
            compute.SetVector("_TerrainLodBlend", tile.lodBlend);
            compute.SetInt("_Resolution", tile.heights.width); compute.SetFloat("_TileSize", t.Settings.chunkSize);
            int stride = 1;
            foreach (LODInfo lod in t.Settings.lods) if (lod.lod == t.AppliedLod) stride = lod.GetSimplificationLevel();
            compute.SetInt("_TerrainStride", Mathf.Max(1, stride)); compute.SetInt("_TerrainLod", t.AppliedLod);
            compute.SetMatrix("_TileToWorld", t.GameObject.transform.localToWorldMatrix);
            compute.SetTexture(generateKernel, "_HeightMap", tile.heights); compute.SetTexture(cullKernel, "_HeightMap", tile.heights);
            compute.SetTexture(generateKernel, "_NormalDensity", tile.normalDensity);
            compute.SetTexture(generateKernel, "_BiomeMap", t.BiomeTexture != null ? t.BiomeTexture : Texture2D.whiteTexture);
            compute.SetBuffer(generateKernel, "_Exclusions", tile.exclusions); compute.SetInt("_ExclusionCount", tile.exclusionCount);
            compute.SetInt("_CandidateCount", candidateCount);
            var site = tile.uploadedMap.BaseSite;
            // ConfigureFootprint cuts the pit 0.25 m inside the concrete foundation.
            compute.SetVector("_BaseGrassSite", new Vector4(site.center.x, site.center.y,
                Mathf.Max(0f, site.carveRadius) + 0.25f, site.valid ? 1f : 0f));
            compute.SetVector("_BaseGrassEdge", new Vector4(Mathf.Max(0f, configuration.baseClearance),
                Mathf.Max(0.1f, configuration.baseEdgeFade), Mathf.Max(0f, configuration.baseEdgeVariation),
                Mathf.Max(0.001f, configuration.baseEdgeNoiseScale)));
        }
        void Generate(GrassTile tile, GrassPatch patch)
        {
            GrassBuffers b = patch.buffers;
            b.generated.SetData(zero);
            compute.SetBuffer(generateKernel, "_Blades", b.blades); compute.SetBuffer(generateKernel, "_GeneratedCount", b.generated);
            compute.SetInt("_Seed", configuration.seed ^ tile.terrain.Settings.noiseSettings.seed);
            compute.SetInt("_GridResolution", gridResolution); compute.SetFloat("_PatchSize", residentPatchSize);
            compute.SetVector("_PatchOrigin", patch.origin); compute.SetVector("_GlobalPatch", new Vector4(patch.global.x, patch.global.y, 0, 0));
            compute.SetVector("_HeightRange", configuration.heightRange); compute.SetVector("_WidthRange", configuration.widthRange);
            compute.SetVector("_BendRange", configuration.bendRange);
            compute.Dispatch(generateKernel, (candidateCount + 63) / 64, 1, 1);
            patch.birth = Time.unscaledTime; patch.generated = true;
            patch.lastStatistics = 0;
        }
        void Render(GrassTile tile, GrassPatch patch)
        {
            GrassBuffers b = patch.buffers;
            compute.SetBuffer(cullKernel, "_Blades", b.blades);
            for (int i = 0; i < 4; i++)
            {
                b.visible[i].SetCounterValue(0);
                compute.SetBuffer(cullKernel, i == 0 ? "_Visible0" : i == 1 ? "_Visible1" : i == 2 ? "_Visible2" : "_ShadowVisible", b.visible[i]);
            }
            compute.Dispatch(cullKernel, (candidateCount + 63) / 64, 1, 1);
            bool inFrustum = !configuration.culling || GeometryUtility.TestPlanesAABB(planes, patch.bounds);
            for (int i = 0; i < 4; i++)
            {
                GraphicsBuffer.CopyCount(b.visible[i], b.args, i * GraphicsBuffer.IndirectDrawIndexedArgs.size + GrassBuffers.CountOffset);
                if (i == 3 && (!configuration.castNearShadows || FlatDistance(patch.bounds, renderCamera.transform.position) > configuration.shadowDistance)) continue;
                if (i != 3 && !inFrustum) continue;
                MaterialPropertyBlock props = b.properties[i];
                props.SetMatrix("_TileToWorld", tile.terrain.GameObject.transform.localToWorldMatrix);
                props.SetFloat("_PatchFade", Mathf.Clamp01((Time.unscaledTime - patch.birth) / configuration.patchFadeSeconds));
                props.SetVector("_TerrainLodBlend", tile.lodBlend);
                RenderParams parameters = new RenderParams(material)
                {
                    camera = renderCamera, worldBounds = patch.bounds, matProps = props,
                    shadowCastingMode = i == 3 ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off,
                    receiveShadows = true, layer = gameObject.layer,
                    lightProbeUsage = LightProbeUsage.Off
                };
                if (UsingGraphicsJobsFallback)
                {
                    // Narrow Unity 6.6/DX11 graphics-jobs workaround; counts still remain GPU-driven.
#pragma warning disable CS0618
                    Graphics.DrawMeshInstancedIndirect(meshes[i == 3 ? 0 : i], 0, material, patch.bounds, b.args,
                        i * GraphicsBuffer.IndirectDrawIndexedArgs.size, props, parameters.shadowCastingMode,
                        true, gameObject.layer, renderCamera, LightProbeUsage.Off);
#pragma warning restore CS0618
                }
                else Graphics.RenderMeshIndirect(parameters, meshes[i == 3 ? 0 : i], b.args, 1, i);
            }
        }
        void CountsReceived(AsyncGPUReadbackRequest request)
        {
            if (!initialized || statisticsBuffer == null) return;
            if (!request.hasError && statisticsBuffer.version == statisticsVersion)
            {
                var commands = request.GetData<GraphicsBuffer.IndirectDrawIndexedArgs>();
                statisticsBuffer.visibleCount = commands[0].instanceCount + commands[1].instanceCount + commands[2].instanceCount;
            }
            AsyncGPUReadback.Request(statisticsBuffer.generated, generatedCallback);
        }
        void GeneratedReceived(AsyncGPUReadbackRequest request)
        {
            if (statisticsBuffer == null) return;
            if (initialized && !request.hasError && statisticsBuffer.version == statisticsVersion) statisticsBuffer.generatedCount = request.GetData<uint>()[0];
            statisticsBuffer.readbackPending = false; statisticsBuffer = null;
        }
        void OnDisable()
        {
            if (!initialized) return;
            if (terrain != null) terrain.ChunkCreated -= AttachChunk;
            initialized = false;
            // Pending readbacks must finish before their buffers are released.
            AsyncGPUReadback.WaitAllRequests(); statisticsBuffer = null;
            foreach (GrassTile tile in tiles.Values) tile.Dispose(); tiles.Clear();
            pool.Dispose(); pool = null; freePatches.Clear();
            foreach (Mesh mesh in meshes) Destroy(mesh);
            Destroy(material); Destroy(compute);
        }
        void OnDestroy() { OnDisable(); }
#if UNITY_EDITOR
        public void RenderForValidation() { if (initialized) Tick(); }
        public void UseCompatibilityForValidation()
        { UsingGraphicsJobsFallback = true; material.EnableKeyword("LEVIATHAN_LEGACY_INDIRECT"); }
        // Synchronous diagnostic reads are deliberately restricted to explicit editor tests.
        public uint[] ReadVisibleCountsForValidation()
        {
            uint[] result = new uint[4];
            var commands = new GraphicsBuffer.IndirectDrawIndexedArgs[4];
            foreach (GrassTile tile in tiles.Values) foreach (GrassPatch patch in tile.patches.Values)
            {
                patch.buffers.args.GetData(commands);
                for (int i = 0; i < 4; i++) result[i] += commands[i].instanceCount;
            }
            return result;
        }
        public uint ReadDistributionHashForValidation()
        {
            uint hash = 2166136261;
            uint[] data = new uint[candidateCount * 8];
            foreach (GrassTile tile in tiles.Values) foreach (GrassPatch patch in tile.patches.Values)
            {
                patch.buffers.blades.GetData(data);
                for (int i = 0; i < data.Length; i++) hash = (hash ^ data[i]) * 16777619;
            }
            return hash;
        }
        struct DiagnosticBlade { public Vector3 position; public uint shape, normal, bend, color, seed; }
        public float ValidateRootsForValidation()
        {
            float maximumError = 0;
            var blades = new DiagnosticBlade[candidateCount];
            foreach (GrassTile tile in tiles.Values)
            {
                HeightMap map = tile.uploadedMap;
                if (map == null) continue;
                int stride = 1;
                foreach (LODInfo lod in tile.terrain.Settings.lods) if (lod.lod == tile.terrain.AppliedLod) stride = lod.GetSimplificationLevel();
                foreach (GrassPatch patch in tile.patches.Values)
                {
                    if (!patch.generated) continue;
                    patch.buffers.blades.GetData(blades);
                    foreach (DiagnosticBlade blade in blades)
                    {
                        if (blade.shape == 0) continue;
                        float gx = blade.position.x / tile.terrain.Settings.chunkSize * (map.Width - 1);
                        float gy = (1 - blade.position.z / tile.terrain.Settings.chunkSize) * (map.Height - 1);
                        int x = Mathf.Min(Mathf.FloorToInt(gx / stride) * stride, map.Width - 2);
                        int y = Mathf.Min(Mathf.FloorToInt(gy / stride) * stride, map.Height - 2);
                        int xx = Mathf.Min(x + stride, map.Width - 1), yy = Mathf.Min(y + stride, map.Height - 1);
                        float fx = (gx - x) / (xx - x), fy = (gy - y) / (yy - y);
                        float h = fx + fy <= 1 ? map.WorldHeights[x,y] * (1 - fx - fy) + map.WorldHeights[xx,y] * fx + map.WorldHeights[x,yy] * fy
                            : map.WorldHeights[xx,yy] * (fx + fy - 1) + map.WorldHeights[xx,y] * (1 - fy) + map.WorldHeights[x,yy] * (1 - fx);
                        maximumError = Mathf.Max(maximumError, Mathf.Abs(blade.position.y - h + 0.015f));
                        int mx = Mathf.Min(Mathf.FloorToInt(gx), map.Width - 2), my = Mathf.Min(Mathf.FloorToInt(gy), map.Height - 2);
                        for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++)
                            if (map.Water[mx+i,my+j] > 0.01f || map.Roads[mx+i,my+j] > 0.01f || map.PoiMask[mx+i,my+j] > 0.01f)
                                throw new InvalidOperationException("Grass generated in a water/road/POI mask.");
                        Vector3 world = tile.terrain.GameObject.transform.TransformPoint(blade.position);
                        if (map.BaseSite.valid && Vector2.Distance(new Vector2(world.x, world.z), map.BaseSite.center) <
                            Mathf.Max(0f, map.BaseSite.carveRadius) + 0.25f + Mathf.Max(0f, configuration.baseClearance) - 0.001f)
                            throw new InvalidOperationException("Grass generated inside the base's concrete clearance.");
                        foreach (GrassExclusionVolume volume in exclusionVolumes)
                        {
                            if (volume == null || !volume.isActiveAndEnabled) continue;
                            Bounds bounds = volume.WorldBounds; bounds.Expand(0.5f);
                            if (world.x >= bounds.min.x && world.x <= bounds.max.x && world.z >= bounds.min.z && world.z <= bounds.max.z)
                                throw new InvalidOperationException("Grass generated inside a foundation exclusion.");
                        }
                    }
                }
            }
            if (maximumError > 0.002f) throw new InvalidOperationException("Grass roots do not follow applied terrain LOD: " + maximumError);
            return maximumError;
        }
#endif
        void OnDrawGizmosSelected()
        {
            if (configuration == null || !configuration.drawGizmos) return;
            Gizmos.color = new Color(0.3f, 0.9f, 0.25f);
            foreach (GrassTile tile in tiles.Values) foreach (GrassPatch patch in tile.patches.Values) Gizmos.DrawWireCube(patch.bounds.center, patch.bounds.size);
        }
    }
}
