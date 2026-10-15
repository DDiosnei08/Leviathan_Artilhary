using System.Collections.Generic;
using UnityEngine;
using ProjectLeviathan.World;

namespace ProjectLeviathan.ProceduralTerrain
{
    public class TerrainChunk
    {
        public Vector2Int Coordinate { get; }
        public GameObject GameObject { get; private set; }
        public MeshFilter MeshFilter { get; private set; }
        public MeshRenderer MeshRenderer { get; private set; }
        public MeshCollider MeshCollider { get; private set; }
        public TerrainSettings Settings { get; }
        public int CurrentLod { get; private set; }
        public int AppliedLod { get; private set; }
        public Texture2D BiomeTexture => colorTexture;
        public event System.Action<TerrainChunk> DataChanged;
        public event System.Action<TerrainChunk> Disposing;
        public bool HasGeneratedMesh { get; private set; }
        public bool GenerationFailed { get; private set; }
        public HeightMap GeneratedData { get; private set; }
        public bool WorldFeaturesReady { get; private set; }
        public int GeneratedPOIVisualCount { get; private set; }
        public bool HasGeneratedRoadMesh => roadsMesh != null;
        public Mesh GeneratedRoadMesh => roadsMesh;
        public Mesh GeneratedWaterMesh => waterMesh;
        public MeshRenderer WaterRenderer => waterRoot != null ? waterRoot.GetComponent<MeshRenderer>() : null;
        public int VertexCount => MeshFilter != null && MeshFilter.sharedMesh != null ? MeshFilter.sharedMesh.vertexCount : 0;
        public bool HasMaterial => MeshRenderer != null && MeshRenderer.sharedMaterial != null;
        public Vector3 CenterWorldPosition => GameObject != null && Settings != null
            ? GameObject.transform.TransformPoint(new Vector3(Settings.chunkSize * 0.5f, 0f, Settings.chunkSize * 0.5f))
            : Vector3.zero;
        public string DebugLabel => $"Chunk {Coordinate.x}, {Coordinate.y} | LOD {CurrentLod}";

        private readonly Vector2 worldPosition;
        private readonly LODInfo[] lodInfos;
        private Texture2D colorTexture;
        private Transform propsRoot;
        private Transform roadsRoot;
        private Transform poiRoot;
        private Mesh roadsMesh;
        private Transform waterRoot;
        private Mesh waterMesh;
        private Material terrainMaterialInstance;
        private int colorTexturePropertyId;
        private bool propsGenerated;
        private bool shouldShowProps = true;

        public TerrainChunk(Vector2Int coordinate, TerrainSettings settings, Transform parent)
        {
            if (settings == null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }

            Coordinate = coordinate;
            Settings = settings;
            worldPosition = settings.GetChunkWorldOrigin(coordinate);
            lodInfos = ToLodArray(settings.lods);
            CreateGameObject(parent);
        }

        static LODInfo[] ToLodArray(System.Collections.Generic.List<LODInfo> source)
        {
            if (source == null || source.Count == 0)
            {
                return new LODInfo[0];
            }

            LODInfo[] array = new LODInfo[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                array[i] = source[i];
            }

            return array;
        }

        void CreateGameObject(Transform parent)
        {
            GameObject = new GameObject($"TerrainChunk_{Coordinate.x}_{Coordinate.y}");
            GameObject.transform.SetParent(parent, false);
            GameObject.transform.localPosition = new Vector3(worldPosition.x, 0f, worldPosition.y);
            if (!Application.isPlaying)
            {
                GameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            MeshFilter = GameObject.AddComponent<MeshFilter>();
            MeshRenderer = GameObject.AddComponent<MeshRenderer>();
            MeshCollider = GameObject.AddComponent<MeshCollider>();
            colorTexturePropertyId = Settings != null && Settings.terrainMaterial != null &&
                Settings.terrainMaterial.HasProperty("_BaseMap")
                ? Shader.PropertyToID("_BaseMap")
                : Shader.PropertyToID("_MainTex");

            if (Settings != null && Settings.terrainMaterial != null)
            {
                MeshRenderer.sharedMaterial = Settings.terrainMaterial;
            }
        }

        public void UpdateLod(Vector3 viewerPosition)
        {
            float distance = Vector3.Distance(viewerPosition, CenterWorldPosition);
            shouldShowProps = distance <= Settings.propViewDistance;
            if (propsRoot != null)
            {
                if (propsRoot.gameObject.activeSelf != shouldShowProps)
                {
                    propsRoot.gameObject.SetActive(shouldShowProps);
                }
            }

            int lod = 0;

            for (int i = lodInfos.Length - 1; i >= 0; i--)
            {
                if (distance > lodInfos[i].visibleDistance)
                {
                    lod = lodInfos[i].lod;
                    break;
                }
            }

            var site = Settings.RuntimeBaseSite;
            Vector2 nearest = new Vector2(Mathf.Clamp(site.center.x, worldPosition.x, worldPosition.x + Settings.chunkSize),
                Mathf.Clamp(site.center.y, worldPosition.y, worldPosition.y + Settings.chunkSize));
            if (site.valid && (nearest - site.center).sqrMagnitude <= site.InfluenceRadius * site.InfluenceRadius) lod = 0;

            if (CurrentLod != lod)
            {
                CurrentLod = lod;
                HasGeneratedMesh = false;
                GenerationFailed = false;
            }
        }

        public void GenerateMesh()
        {
            if (Settings == null || GameObject == null)
            {
                return;
            }

            Vector2 chunkWorldOrigin = Settings.GetChunkWorldOrigin(Coordinate);
            TerrainGenerationSettings generationSettings = Settings.CaptureGenerationSettings();
            HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(
                generationSettings,
                Settings.chunkResolution,
                Settings.chunkResolution,
                chunkWorldOrigin);
            MeshData data = BaseTerrainMesh.Generate(heightMap, generationSettings, CurrentLod);
            ApplyGeneratedData(data, heightMap);
            GameObject.SetActive(true);
        }

        public void ApplyGeneratedMesh(MeshData data)
        {
            ApplyGeneratedData(data, (HeightMap)null);
        }

        public void ApplyGeneratedData(MeshData data, float[,] heightMap, float[,] biomeMap)
        {
            ApplyGeneratedData(
                data,
                heightMap == null ? null : new HeightMap(heightMap, null, biomeMap));
        }

        public void ApplyGeneratedData(MeshData data, HeightMap heightMap)
        {
            if (data == null || MeshFilter == null)
            {
                return;
            }

            if (WorldFeaturesReady)
            {
                ClearWorldFeatureVisuals();
            }

            Mesh previousMesh = MeshFilter.sharedMesh;
            Mesh newMesh = data.CreateMesh();
            MeshFilter.sharedMesh = newMesh;
            if (MeshCollider != null)
            {
                MeshCollider.sharedMesh = null;
                MeshCollider.sharedMesh = newMesh;
            }
            if (previousMesh != null)
            {
                DestroyUnityObject(previousMesh);
            }
            GeneratedData = heightMap;
            RefreshTerrainAppearance();
            RefreshWaterGeometry();

            if (heightMap != null && !propsGenerated)
            {
                propsRoot = TerrainPropGenerator.Generate(this, heightMap);
                propsGenerated = true;
                if (propsRoot != null && propsRoot.gameObject.activeSelf != shouldShowProps)
                {
                    propsRoot.gameObject.SetActive(shouldShowProps);
                }
            }

            GenerationFailed = false;
            HasGeneratedMesh = true;
            AppliedLod = CurrentLod;
            DataChanged?.Invoke(this);
        }

        public void RefreshTerrainAppearance()
        {
            RefreshTerrainAppearance(default);
        }

        public void RefreshTerrainAppearance(WorldWeatherSample weather)
        {
            if (GeneratedData == null || Settings == null || MeshRenderer == null ||
                MeshRenderer.sharedMaterial == null)
            {
                return;
            }

            if (colorTexture != null)
            {
                DestroyUnityObject(colorTexture);
            }

            colorTexture = TextureGenerator.TextureFromColorMap(
                TextureGenerator.BuildColorMap(
                    GeneratedData,
                    Settings,
                    Settings.GetChunkWorldOrigin(Coordinate),
                    weather),
                GeneratedData.Width,
                GeneratedData.Height);
            // Material próprio por chunk em vez de MaterialPropertyBlock: o GPU Resident Drawer / SRP Batcher
            // podem ignorar o property block e deixar o terreno sem a textura de cores.
            if (terrainMaterialInstance == null)
            {
                terrainMaterialInstance = new Material(MeshRenderer.sharedMaterial)
                {
                    name = $"TerrainChunkMaterial_{Coordinate.x}_{Coordinate.y}"
                };
                if (!Application.isPlaying)
                {
                    terrainMaterialInstance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                }

                MeshRenderer.sharedMaterial = terrainMaterialInstance;
            }

            terrainMaterialInstance.SetTexture(colorTexturePropertyId, colorTexture);
        }

        public void ApplyWorldFeatureVisuals(
            IEnumerable<TerrainWorldPOI> pointsOfInterest,
            Material defaultRoadMaterial)
        {
            if (GeneratedData == null || GameObject == null)
            {
                return;
            }

            ClearWorldFeatureVisuals();
            RefreshTerrainAppearance();
            RefreshRoadGeometry(defaultRoadMaterial);
            poiRoot = new GameObject("Generated Points of Interest").transform;
            poiRoot.SetParent(GameObject.transform, false);
            if (!Application.isPlaying)
            {
                poiRoot.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            float chunkOriginX = Coordinate.x * Settings.chunkSize;
            float chunkOriginZ = Coordinate.y * Settings.chunkSize;
            foreach (TerrainWorldPOI poi in pointsOfInterest)
            {
                TerrainPOIDefinition definition = FindPOIDefinition(poi.Id);
                if (definition == null)
                {
                    Debug.LogError($"No TerrainPOIDefinition found for generated POI '{poi.Id}'.", GameObject);
                    continue;
                }

                GameObject visual = definition.visualPrefab != null
                    ? UnityEngine.Object.Instantiate(definition.visualPrefab, poiRoot, false)
                    : CreateFallbackPOI(definition);
                if (visual == null)
                {
                    Debug.LogError(
                        $"POI '{definition.id}' has no prefab and fallback visuals are disabled.",
                        GameObject);
                    continue;
                }

                visual.name = $"POI_{definition.id}";
                visual.transform.SetParent(poiRoot, false);
                visual.transform.localPosition = new Vector3(
                    poi.TerrainPosition.x - chunkOriginX,
                    poi.TerrainPosition.y + definition.visualSurfaceOffsetMeters,
                    poi.TerrainPosition.z - chunkOriginZ);
                visual.transform.localRotation = Quaternion.Euler(0f, GetPOIRotation(poi), 0f);
                visual.transform.localScale *= definition.visualScale;
                GeneratedPOIVisualCount++;
            }

            WorldFeaturesReady = true;
            DataChanged?.Invoke(this);
        }

        void RefreshWaterGeometry()
        {
            ClearWaterGeometry();
            if (GeneratedData == null || !Settings.generateWaterSurfaces || Settings.waterMaterial == null) return;
            waterMesh = TerrainWaterMeshGenerator.Generate(GeneratedData, Settings);
            if (waterMesh == null) return;
            waterRoot = new GameObject("Generated Water Surface").transform;
            waterRoot.SetParent(GameObject.transform, false);
            if (!Application.isPlaying)
                waterRoot.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            waterRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            MeshRenderer renderer = waterRoot.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Settings.waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        void ClearWaterGeometry()
        {
            if (waterRoot != null) DestroyUnityObject(waterRoot.gameObject);
            if (waterMesh != null) DestroyUnityObject(waterMesh);
            waterRoot = null;
            waterMesh = null;
        }

        TerrainPOIDefinition FindPOIDefinition(string id)
        {
            for (int i = 0; i < Settings.poiDefinitions.Count; i++)
            {
                TerrainPOIDefinition definition = Settings.poiDefinitions[i];
                if (definition != null && definition.id == id)
                {
                    return definition;
                }
            }

            return null;
        }

        GameObject CreateFallbackPOI(TerrainPOIDefinition definition)
        {
            if (!definition.useFallbackVisual)
            {
                return null;
            }

            GameObject root = new GameObject(definition.displayName);
            if (definition.fallbackVisualStyle == TerrainPOIVisualStyle.Depot)
            {
                AddMarkerPart(root.transform, "Depot Foundation", PrimitiveType.Cube,
                    new Vector3(0f, 2f, 0f), new Vector3(24f, 4f, 18f), definition.markerColor);
                AddMarkerPart(root.transform, "Depot Storehouse", PrimitiveType.Cube,
                    new Vector3(0f, 8f, 0f), new Vector3(16f, 8f, 12f),
                    Color.Lerp(definition.markerColor, Color.black, 0.3f));
                AddMarkerPart(root.transform, "Depot Tank", PrimitiveType.Cylinder,
                    new Vector3(10f, 6f, 0f), new Vector3(4f, 6f, 4f),
                    Color.Lerp(definition.markerColor, Color.white, 0.18f));
            }
            else if (definition.fallbackVisualStyle == TerrainPOIVisualStyle.Landmark)
            {
                AddMarkerPart(root.transform, "Landmark Base", PrimitiveType.Cylinder,
                    new Vector3(0f, 2f, 0f), new Vector3(10f, 2f, 10f), definition.markerColor);
                AddMarkerPart(root.transform, "Landmark Beacon", PrimitiveType.Cylinder,
                    new Vector3(0f, 12f, 0f), new Vector3(2f, 10f, 2f),
                    Color.Lerp(definition.markerColor, Color.white, 0.25f));
            }
            else
            {
                AddMarkerPart(root.transform, "Outpost Foundation", PrimitiveType.Cube,
                    new Vector3(0f, 2f, 0f), new Vector3(18f, 4f, 18f), definition.markerColor);
                AddMarkerPart(root.transform, "Outpost Bunker", PrimitiveType.Cube,
                    new Vector3(0f, 8f, 0f), new Vector3(10f, 8f, 10f),
                    Color.Lerp(definition.markerColor, Color.black, 0.35f));
                AddMarkerPart(root.transform, "Outpost Signal", PrimitiveType.Cylinder,
                    new Vector3(0f, 15f, 0f), new Vector3(1.2f, 7f, 1.2f),
                    Color.Lerp(definition.markerColor, Color.white, 0.2f));
            }

            if (!Application.isPlaying)
            {
                root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            return root;
        }

        void AddMarkerPart(
            Transform root,
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject part = CreateMarkerPart(name, primitive, position, scale, color);
            part.transform.SetParent(root, false);
        }

        GameObject CreateMarkerPart(
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject part = UnityEngine.GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyUnityObject(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            Material material = Settings.terrainMaterial;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
            return part;
        }

        float GetPOIRotation(TerrainWorldPOI poi)
        {
            unchecked
            {
                uint hash = (uint)Settings.noiseSettings.seed;
                hash ^= (uint)poi.ChunkCoordinate.x * 0x8da6b343u;
                hash ^= (uint)poi.ChunkCoordinate.y * 0xd8163841u;
                for (int i = 0; i < poi.Id.Length; i++)
                {
                    hash = (hash ^ poi.Id[i]) * 16777619u;
                }

                return (hash & 0xffffu) / 65535f * 360f;
            }
        }

        void RefreshRoadGeometry(Material defaultRoadMaterial)
        {
            if (roadsMesh != null)
            {
                DestroyUnityObject(roadsMesh);
                roadsMesh = null;
            }

            if (roadsRoot != null)
            {
                DestroyUnityObject(roadsRoot.gameObject);
                roadsRoot = null;
            }

            roadsMesh = TerrainRoadMeshGenerator.Generate(GeneratedData, Settings);
            if (roadsMesh == null)
            {
                return;
            }

            roadsRoot = new GameObject("Generated Road Surface").transform;
            roadsRoot.SetParent(GameObject.transform, false);
            if (!Application.isPlaying)
            {
                roadsRoot.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            MeshFilter roadFilter = roadsRoot.gameObject.AddComponent<MeshFilter>();
            MeshRenderer roadRenderer = roadsRoot.gameObject.AddComponent<MeshRenderer>();
            roadFilter.sharedMesh = roadsMesh;
            roadRenderer.sharedMaterial = Settings.roadMaterial != null
                ? Settings.roadMaterial
                : defaultRoadMaterial;
            roadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            roadRenderer.receiveShadows = false;
            MaterialPropertyBlock roadProperties = new MaterialPropertyBlock();
            Color gravelColor = new Color(0.42f, 0.35f, 0.25f);
            roadProperties.SetColor("_BaseColor", gravelColor);
            roadProperties.SetColor("_Color", gravelColor);
            roadProperties.SetTexture("_BaseMap", Texture2D.whiteTexture);
            roadProperties.SetTexture("_MainTex", Texture2D.whiteTexture);
            roadRenderer.SetPropertyBlock(roadProperties);
        }

        void ClearWorldFeatureVisuals()
        {
            if (roadsMesh != null)
            {
                DestroyUnityObject(roadsMesh);
                roadsMesh = null;
            }

            if (roadsRoot != null)
            {
                DestroyUnityObject(roadsRoot.gameObject);
                roadsRoot = null;
            }

            if (poiRoot != null)
            {
                DestroyUnityObject(poiRoot.gameObject);
                poiRoot = null;
            }

            GeneratedPOIVisualCount = 0;
            WorldFeaturesReady = false;
        }

        public void MarkGenerationFailed()
        {
            GenerationFailed = true;
        }

        static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public void SetVisible(bool visible)
        {
            if (GameObject != null)
            {
                GameObject.SetActive(visible);
            }
        }

        public void Dispose()
        {
            Disposing?.Invoke(this);
            DataChanged = null;
            Disposing = null;
            ClearWaterGeometry();
            ClearWorldFeatureVisuals();
            Mesh mesh = MeshFilter != null ? MeshFilter.sharedMesh : null;
            if (GameObject != null)
            {
                GameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(GameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(GameObject);
                }
            }

            if (mesh != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }

            if (colorTexture != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(colorTexture);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(colorTexture);
                }
            }

            if (terrainMaterialInstance != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(terrainMaterialInstance);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(terrainMaterialInstance);
                }
            }
        }
    }
}
