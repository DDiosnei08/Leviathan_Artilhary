using System.Collections.Generic;
using UnityEngine;
using ProjectLeviathan.World;

namespace ProjectLeviathan.ProceduralTerrain
{
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public class TerrainWorldController : MonoBehaviour
    {
        public TerrainSettings terrainSettings;
        public Transform viewer;
        public WorldWeatherProvider weatherProvider;
        public Material terrainMaterial;
        public int chunksVisibleInViewDistance = 1;
        public bool useBackgroundGeneration = false;
        public bool useFiniteMap = true;
        public bool previewInEditor = true;
        [Tooltip("Em mapas finitos grandes (janela ao redor do viewer), máximo de chunks cujo mesh é gerado por frame durante o Play. 0 = sem limite.")]
        [Min(0)] public int maxChunkGenerationsPerFrame = 2;

        private Dictionary<Vector2Int, TerrainChunk> terrainChunks = new Dictionary<Vector2Int, TerrainChunk>();
        public IReadOnlyDictionary<Vector2Int, TerrainChunk> TerrainChunks => terrainChunks;
        public event System.Action<TerrainChunk> ChunkCreated;
        private TerrainWorldFeatureGenerationResult worldFeatureGeneration;
        private bool featureGenerationAttempted;
        private Vector2Int viewerChunkCoord;
        private readonly HashSet<Vector2Int> visibleCoords = new HashSet<Vector2Int>();
        private ThreadedTerrainGenerator threadedGenerator;
        private bool editorPreviewGenerated;
        private bool editorPreviewSuppressed;
        private int lastAppliedWeatherRevision = -1;

        public int GeneratedChunkCount => terrainChunks.Count;

        /// <summary>True quando o mapa finito atual carrega só uma janela de chunks ao redor do viewer.</summary>
        public bool IsWindowedFiniteMap =>
            useFiniteMap && terrainSettings != null && terrainSettings.UsesWindowedFiniteMap;

        /// <summary>
        /// Calcula os limites (inclusivos) de coordenadas de chunk que devem existir agora no mapa finito.
        /// Mapa pequeno: todos os chunks. Mapa grande: interseção entre o mapa e a janela ao redor do viewer.
        /// </summary>
        public void GetRequiredFiniteChunkBounds(out Vector2Int min, out Vector2Int max)
        {
            int radius = terrainSettings.finiteMapChunksPerSide / 2;
            min = new Vector2Int(-radius, -radius);
            max = new Vector2Int(radius, radius);
            if (!terrainSettings.UsesWindowedFiniteMap)
            {
                return;
            }

            Vector2Int center = GetViewerChunkCoordinate();
            center.x = Mathf.Clamp(center.x, -radius, radius);
            center.y = Mathf.Clamp(center.y, -radius, radius);
            int window = Application.isPlaying
                ? terrainSettings.finiteMapStreamingRadiusChunks
                : Mathf.Min(terrainSettings.finiteMapStreamingRadiusChunks, terrainSettings.finiteMapEditorPreviewRadiusChunks);
            min = new Vector2Int(Mathf.Max(-radius, center.x - window), Mathf.Max(-radius, center.y - window));
            max = new Vector2Int(Mathf.Min(radius, center.x + window), Mathf.Min(radius, center.y + window));
        }

        Vector2Int GetViewerChunkCoordinate()
        {
            if (viewer == null)
            {
                return Vector2Int.zero;
            }

            Vector3 local = transform.InverseTransformPoint(viewer.position);
            return new Vector2Int(
                Mathf.FloorToInt(local.x / terrainSettings.chunkSize),
                Mathf.FloorToInt(local.z / terrainSettings.chunkSize));
        }
        public IReadOnlyList<TerrainWorldPOI> GeneratedPOIs =>
            worldFeatureGeneration != null
                ? worldFeatureGeneration.PointsOfInterest
                : System.Array.Empty<TerrainWorldPOI>();
        public int GeneratedUnconnectedRoutes =>
            worldFeatureGeneration != null ? worldFeatureGeneration.UnconnectedRoutes : 0;

        public bool TryGetTerrainChunk(Vector2Int coordinate, out TerrainChunk chunk)
        {
            return terrainChunks.TryGetValue(coordinate, out chunk);
        }
        public bool TryGetGeneratedMesh(Vector2Int coordinate, out Mesh mesh)
        {
            if (terrainChunks.TryGetValue(coordinate, out TerrainChunk chunk) &&
                chunk.MeshFilter != null && chunk.MeshFilter.sharedMesh != null)
            {
                mesh = chunk.MeshFilter.sharedMesh;
                return true;
            }

            mesh = null;
            return false;
        }

        public int GeneratedVertexCount
        {
            get
            {
                int count = 0;
                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    count += chunk.VertexCount;
                }

                return count;
            }
        }

        public int GeneratedTriangleCount
        {
            get
            {
                int count = 0;
                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    if (chunk.MeshFilter != null && chunk.MeshFilter.sharedMesh != null)
                    {
                        count += chunk.MeshFilter.sharedMesh.triangles.Length / 3;
                    }
                }

                return count;
            }
        }

        public string TerrainShaderName
        {
            get
            {
                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    if (chunk.MeshRenderer != null && chunk.MeshRenderer.sharedMaterial != null &&
                        chunk.MeshRenderer.sharedMaterial.shader != null)
                    {
                        return chunk.MeshRenderer.sharedMaterial.shader.name;
                    }
                }

                return terrainSettings != null && terrainSettings.terrainMaterial != null &&
                    terrainSettings.terrainMaterial.shader != null
                    ? terrainSettings.terrainMaterial.shader.name
                    : "Missing terrain material";
            }
        }

        public bool HasVisibleTerrainRenderer
        {
            get
            {
                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    if (chunk.MeshRenderer != null && chunk.MeshRenderer.enabled &&
                        chunk.MeshFilter != null && chunk.MeshFilter.sharedMesh != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasGeneratedTerrain
        {
            get
            {
                if (terrainChunks.Count == 0)
                {
                    return false;
                }

                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    if (chunk.HasGeneratedMesh)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= GenerateEditorPreview;
                UnityEditor.EditorApplication.delayCall += GenerateEditorPreview;
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= GenerateEditorPreview;
            if (!Application.isPlaying)
            {
                ClearTerrain();
            }
#endif
        }

        void GenerateEditorPreview()
        {
#if UNITY_EDITOR
            if (this == null || !isActiveAndEnabled || Application.isPlaying ||
                !previewInEditor || !useFiniteMap || terrainSettings == null)
            {
                return;
            }

            RegenerateTerrain();
#endif
        }

        void Awake()
        {
            if (terrainSettings == null)
            {
                terrainSettings = ScriptableObject.CreateInstance<TerrainSettings>();
                terrainSettings.Validate();
            }

            if (terrainMaterial != null)
            {
                terrainSettings.terrainMaterial = terrainMaterial;
            }

            if (viewer == null)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    viewer = camera.transform;
                }
            }

            if (weatherProvider == null)
            {
                weatherProvider = Object.FindAnyObjectByType<WorldWeatherProvider>();
            }

            threadedGenerator = GetComponent<ThreadedTerrainGenerator>();
            if (threadedGenerator == null && useBackgroundGeneration)
            {
                threadedGenerator = gameObject.AddComponent<ThreadedTerrainGenerator>();
            }
        }

        void Update()
        {
            if (terrainSettings == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                if (previewInEditor && useFiniteMap && !editorPreviewSuppressed && !editorPreviewGenerated)
                {
                    terrainSettings.Validate();
                    EnsureFiniteTerrain();
                    editorPreviewGenerated = true;
                }
                else if (previewInEditor && useFiniteMap && !editorPreviewSuppressed)
                {
                    UpdateFiniteWorldFeatures();
                }

                UpdateWeatherVisuals();
                return;
            }

            terrainSettings.Validate();
            if (useFiniteMap)
            {
                EnsureFiniteTerrain();
                UpdateWeatherVisuals();
                return;
            }

            if (viewer == null)
            {
                return;
            }

            UpdateVisibleChunks();
            UpdateWeatherVisuals();
        }

        void OnDestroy()
        {
            ClearTerrain();
        }

        void EnsureFiniteTerrain()
        {
            GetRequiredFiniteChunkBounds(out Vector2Int min, out Vector2Int max);
            bool windowed = terrainSettings.UsesWindowedFiniteMap;
            HashSet<Vector2Int> requiredCoords = new HashSet<Vector2Int>();
            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    requiredCoords.Add(coord);
                    if (!terrainChunks.ContainsKey(coord))
                    {
                        CreateChunk(coord);
                    }
                }
            }

            List<Vector2Int> removedCoords = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, TerrainChunk> pair in terrainChunks)
            {
                if (!requiredCoords.Contains(pair.Key))
                {
                    pair.Value.Dispose();
                    removedCoords.Add(pair.Key);
                }
            }

            foreach (Vector2Int coord in removedCoords)
            {
                terrainChunks.Remove(coord);
            }

            // Orçamento de geração só no Play em mapas com janela; no editor e em mapas pequenos gera tudo de uma vez.
            int generationBudget = windowed && Application.isPlaying && maxChunkGenerationsPerFrame > 0
                ? maxChunkGenerationsPerFrame
                : int.MaxValue;
            foreach (var chunk in terrainChunks.Values)
            {
                chunk.SetVisible(true);
                // LOD só no Play: o preview do editor e a validação usam sempre a resolução cheia (121x121).
                if (windowed && viewer != null && Application.isPlaying)
                {
                    chunk.UpdateLod(viewer.position);
                }

                if (!chunk.HasGeneratedMesh && !chunk.GenerationFailed && useBackgroundGeneration && threadedGenerator != null)
                {
                    threadedGenerator.RequestChunk(chunk);
                }
                else if (!chunk.HasGeneratedMesh && !chunk.GenerationFailed && generationBudget > 0)
                {
                    chunk.GenerateMesh();
                    generationBudget--;
                }
            }

            UpdateFiniteWorldFeatures();
        }

        void UpdateFiniteWorldFeatures()
        {
            if (terrainSettings.UsesWindowedFiniteMap)
            {
                // Mapa grande: sem planejador global; cada chunk gera seus próprios POIs e estradas (como no mundo infinito).
                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    ApplyStreamingFeatures(chunk);
                }
            }
            else
            {
                TryGenerateFiniteWorldFeatures();
            }
        }

        void ApplyStreamingFeatures(TerrainChunk chunk)
        {
            if (!chunk.HasGeneratedMesh || chunk.GeneratedData == null || chunk.WorldFeaturesReady)
            {
                return;
            }

            TerrainWorldFeatureGenerationResult features = TerrainWorldFeatures.GenerateForStreamingChunk(
                terrainSettings,
                chunk.Coordinate,
                chunk.GeneratedData);
            chunk.ApplyWorldFeatureVisuals(
                features.PointsOfInterest,
                terrainMaterial != null ? terrainMaterial : terrainSettings.terrainMaterial);
            RefreshChunkAppearanceForWeather(chunk);
            if (features.UnconnectedRoutes > 0)
            {
                // Em relevo montanhoso é esperado que algumas conexões de estrada de borda não achem rota
                // navegável; informativo, não um erro.
                Debug.Log(
                    $"Chunk {chunk.Coordinate} could not route {features.UnconnectedRoutes} road connection(s) " +
                    "to its boundaries through navigable terrain (expected in steep terrain).",
                    this);
            }
        }

        void TryGenerateFiniteWorldFeatures()
        {
            if (featureGenerationAttempted ||
                terrainChunks.Count != terrainSettings.finiteMapChunksPerSide * terrainSettings.finiteMapChunksPerSide)
            {
                return;
            }

            foreach (TerrainChunk chunk in terrainChunks.Values)
            {
                if (!chunk.HasGeneratedMesh || chunk.GeneratedData == null)
                {
                    return;
                }
            }

            featureGenerationAttempted = true;
            try
            {
                worldFeatureGeneration = TerrainWorldFeatures.Generate(terrainSettings, terrainChunks);
                foreach (TerrainChunk chunk in terrainChunks.Values)
                {
                    List<TerrainWorldPOI> chunkPOIs = new List<TerrainWorldPOI>();
                    foreach (TerrainWorldPOI poi in worldFeatureGeneration.PointsOfInterest)
                    {
                        if (poi.ChunkCoordinate == chunk.Coordinate)
                        {
                            chunkPOIs.Add(poi);
                        }
                    }

                    chunk.ApplyWorldFeatureVisuals(chunkPOIs, terrainMaterial != null
                        ? terrainMaterial
                        : terrainSettings.terrainMaterial);
                    RefreshChunkAppearanceForWeather(chunk);
                }

                if (worldFeatureGeneration.UnconnectedRoutes > 0)
                {
                    Debug.LogWarning(
                        $"Terrain generation could not route {worldFeatureGeneration.UnconnectedRoutes} POI connection(s) " +
                        "through navigable terrain. Check water, slope, and POI placement settings.",
                        this);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        void UpdateWeatherVisuals()
        {
            if (weatherProvider == null || weatherProvider.ConditionsRevision == lastAppliedWeatherRevision)
            {
                return;
            }

            foreach (TerrainChunk chunk in terrainChunks.Values)
            {
                if (chunk.GeneratedData != null)
                {
                    RefreshChunkAppearanceForWeather(chunk);
                }
            }

            lastAppliedWeatherRevision = weatherProvider.ConditionsRevision;
        }

        void RefreshChunkAppearanceForWeather(TerrainChunk chunk)
        {
            if (weatherProvider != null)
            {
                chunk.RefreshTerrainAppearance(weatherProvider.SampleAt(chunk.CenterWorldPosition));
            }
            else
            {
                chunk.RefreshTerrainAppearance();
            }
        }

        void UpdateVisibleChunks()
        {
            Vector3 position = transform.InverseTransformPoint(viewer.position);
            float chunkSize = terrainSettings.chunkSize;
            Vector2Int currentChunk = new Vector2Int(
                Mathf.FloorToInt(position.x / chunkSize),
                Mathf.FloorToInt(position.z / chunkSize));

            if (currentChunk != viewerChunkCoord)
            {
                viewerChunkCoord = currentChunk;
            }

            int radius = Mathf.Max(1, chunksVisibleInViewDistance > 0 ? chunksVisibleInViewDistance : terrainSettings.chunksVisibleInViewDistance);
            int minX = currentChunk.x - radius;
            int maxX = currentChunk.x + radius;
            int minY = currentChunk.y - radius;
            int maxY = currentChunk.y + radius;

            visibleCoords.Clear();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    visibleCoords.Add(coord);

                    if (!terrainChunks.ContainsKey(coord))
                    {
                        CreateChunk(coord);
                    }
                }
            }

            List<Vector2Int> chunksToRemove = new List<Vector2Int>();
            foreach (var pair in terrainChunks)
            {
                if (!visibleCoords.Contains(pair.Key))
                {
                    pair.Value.SetVisible(false);
                    chunksToRemove.Add(pair.Key);
                }
            }

            foreach (Vector2Int coord in chunksToRemove)
            {
                terrainChunks[coord].Dispose();
                terrainChunks.Remove(coord);
            }

            foreach (var chunk in terrainChunks.Values)
            {
                chunk.UpdateLod(viewer.position);
                if (!chunk.HasGeneratedMesh && !chunk.GenerationFailed && useBackgroundGeneration && threadedGenerator != null)
                {
                    threadedGenerator.RequestChunk(chunk);
                }
                else if (!chunk.HasGeneratedMesh && !chunk.GenerationFailed)
                {
                    chunk.GenerateMesh();
                }

                ApplyStreamingFeatures(chunk);
            }
        }

        [ContextMenu("Randomize Match Seed")]
        public void RandomizeMatchSeed()
        {
            SetSeed(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }

        public void SetSeed(int seed)
        {
            if (terrainSettings == null || terrainSettings.noiseSettings == null)
            {
                Debug.LogError("Terrain settings and noise settings are required to set a match seed.", this);
                return;
            }

            ClearTerrain();
            terrainSettings.noiseSettings.seed = seed;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(terrainSettings);
            }
#endif
            EnsureFiniteTerrain();
            editorPreviewGenerated = !Application.isPlaying;
            editorPreviewSuppressed = false;
        }

        public void RegenerateTerrain()
        {
            if (terrainSettings == null)
            {
                Debug.LogError("Assign a TerrainSettings asset before generating terrain.", this);
                return;
            }

            ClearTerrain();
            terrainSettings.Validate();
            EnsureFiniteTerrain();
            editorPreviewGenerated = true;
            editorPreviewSuppressed = false;
        }

        public void ClearTerrain()
        {
            HashSet<GameObject> registeredChunks = new HashSet<GameObject>();
            foreach (TerrainChunk chunk in terrainChunks.Values)
            {
                if (chunk.GameObject != null)
                {
                    registeredChunks.Add(chunk.GameObject);
                }

                chunk.Dispose();
            }

            terrainChunks.Clear();
            worldFeatureGeneration = null;
            featureGenerationAttempted = false;
            lastAppliedWeatherRevision = -1;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (!registeredChunks.Contains(child) && child.name.StartsWith("TerrainChunk_"))
                {
                    DestroyTerrainObject(child);
                }
            }

            editorPreviewGenerated = false;
            editorPreviewSuppressed = !Application.isPlaying;
        }

        static void DestroyTerrainObject(GameObject target)
        {
            target.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        void CreateChunk(Vector2Int coord)
        {
            TerrainChunk chunk = new TerrainChunk(coord, terrainSettings, transform);
            terrainChunks[coord] = chunk;
            ChunkCreated?.Invoke(chunk);
            chunk.SetVisible(true);
        }
    }
}
