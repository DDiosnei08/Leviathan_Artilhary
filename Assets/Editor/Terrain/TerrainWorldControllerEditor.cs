using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain.Editor
{
    [CustomEditor(typeof(TerrainWorldController))]
    public class TerrainWorldControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool settingsChanged = EditorGUI.EndChangeCheck();

            TerrainWorldController controller = (TerrainWorldController)target;
            if (controller.useFiniteMap && controller.terrainSettings != null)
            {
                TerrainSettings terrainSettings = controller.terrainSettings;
                string windowNote = terrainSettings.UsesWindowedFiniteMap
                    ? $" Large map: only a {terrainSettings.finiteMapStreamingRadiusChunks * 2 + 1} x " +
                      $"{terrainSettings.finiteMapStreamingRadiusChunks * 2 + 1} chunk window around the viewer is loaded."
                    : string.Empty;
                EditorGUILayout.HelpBox(
                    $"Finite battlefield: {terrainSettings.finiteMapChunksPerSide} x {terrainSettings.finiteMapChunksPerSide} chunks " +
                    $"({terrainSettings.FiniteMapWorldSize:F0}m x {terrainSettings.FiniteMapWorldSize:F0}m)." + windowNote +
                    " Change Finite Map Chunks Per Side on the Terrain Settings asset.",
                    MessageType.Info);
            }

            if (settingsChanged && controller.terrainSettings != null)
            {
                Undo.RecordObject(controller, "Update Terrain Preview");
                controller.RegenerateTerrain();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Terrain Preview", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(controller.terrainSettings == null))
            {
                if (GUILayout.Button("Generate / Refresh Terrain"))
                {
                    bool useBackgroundGeneration = controller.useBackgroundGeneration;
                    controller.useBackgroundGeneration = false;
                    try
                    {
                        controller.RegenerateTerrain();
                    }
                    finally
                    {
                        controller.useBackgroundGeneration = useBackgroundGeneration;
                    }

                    FrameTerrain(controller);
                }

                if (GUILayout.Button("Randomize Seed and Refresh"))
                {
                    controller.RandomizeMatchSeed();
                    FrameTerrain(controller);
                }
            }

            using (new EditorGUI.DisabledScope(controller.terrainSettings == null))
            {
                if (GUILayout.Button("Frame Terrain in Scene View"))
                {
                    FrameTerrain(controller);
                }

                if (GUILayout.Button("Validate Terrain Setup"))
                {
                    ValidateTerrain(controller);
                }
            }

            if (GUILayout.Button("Clear Preview"))
            {
                controller.ClearTerrain();
            }

            if (controller.terrainSettings == null)
            {
                EditorGUILayout.HelpBox("Assign a TerrainSettings asset before generating the preview.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    controller.HasGeneratedTerrain
                        ? $"Mesh OK: {controller.GeneratedChunkCount} chunk(s), {controller.GeneratedVertexCount:N0} vertices, {controller.GeneratedTriangleCount:N0} triangles.\nShader: {controller.TerrainShaderName}"
                        : "No terrain mesh generated. Click Generate / Refresh Terrain.",
                    controller.HasGeneratedTerrain ? MessageType.Info : MessageType.Warning);

                if (controller.terrainSettings.terrainMaterial == null)
                {
                    EditorGUILayout.HelpBox("TerrainSettings has no terrain material assigned.", MessageType.Error);
                }
            }
        }

        static void FrameTerrain(TerrainWorldController controller)
        {
            if (controller == null || controller.terrainSettings == null || SceneView.lastActiveSceneView == null)
            {
                return;
            }

            TerrainSettings settings = controller.terrainSettings;
            Vector3 center = controller.transform.TransformPoint(
                new Vector3(settings.chunkSize * 0.5f, 0f, settings.chunkSize * 0.5f));
            float frameSize = controller.useFiniteMap
                ? settings.FiniteMapWorldSize
                : settings.chunkSize;
            float distance = frameSize * 1.15f;
            SceneView.lastActiveSceneView.LookAt(
                center,
                Quaternion.Euler(55f, -35f, 0f),
                distance,
                false);
        }

        static void ValidateTerrain(TerrainWorldController controller)
        {
            TerrainSettings settings = controller.terrainSettings;
            if (settings == null)
            {
                Debug.LogError("Terrain validation failed: assign TerrainSettings.", controller);
                return;
            }

            settings.Validate();
            if (!Mathf.Approximately(settings.chunkSize, 1200f) ||
                settings.chunkResolution != 121 ||
                !Mathf.Approximately(settings.CellSize, 10f))
            {
                Debug.LogError(
                    $"Terrain validation failed: expected 1200m chunks with 121x121 vertices and 10m cells, " +
                    $"found {settings.chunkSize:F3}m, {settings.chunkResolution}x{settings.chunkResolution}, " +
                    $"{settings.CellSize:F6}m cells.",
                    settings);
                return;
            }

            Vector3 worldScale = controller.transform.lossyScale;
            if (!Mathf.Approximately(worldScale.x, 1f) || !Mathf.Approximately(worldScale.z, 1f))
            {
                Debug.LogError(
                    $"Terrain validation failed: controller world X/Z scale must be 1 for 1 Unity unit to equal 1 meter; " +
                    $"found ({worldScale.x:F3}, {worldScale.z:F3}).",
                    controller);
                return;
            }

            if (settings.terrainMaterial == null)
            {
                Debug.LogError("Terrain validation failed: no terrain material is assigned.", settings);
                return;
            }

            if (settings.terrainMaterial.shader == null || !settings.terrainMaterial.shader.isSupported)
            {
                Debug.LogError("Terrain validation failed: the terrain material shader is missing or unsupported.", settings.terrainMaterial);
                return;
            }

            if (!ValidateDeterministicNoise(settings.noiseSettings, out string noiseFailure))
            {
                Debug.LogError("Terrain validation failed: " + noiseFailure, settings);
                return;
            }

            if (!ValidateWorldCoordinateSampling(settings.noiseSettings, out string worldNoiseFailure))
            {
                Debug.LogError("Terrain validation failed: " + worldNoiseFailure, settings);
                return;
            }

            if (!ValidateHeightMapWithUnnormalizedNoise(settings, out string heightMapFailure))
            {
                Debug.LogError("Terrain validation failed: " + heightMapFailure, settings);
                return;
            }

            if (!ValidateGeneratedTerrainData(settings, out string terrainDataFailure))
            {
                Debug.LogError("Terrain validation failed: " + terrainDataFailure, settings);
                return;
            }

            if (!ValidateStreamingFeatures(settings, out string streamingFailure))
            {
                Debug.LogError("Terrain validation failed: " + streamingFailure, settings);
                return;
            }

            if (!ValidateChunkSeams(settings, out string seamFailure))
            {
                Debug.LogError("Terrain validation failed: " + seamFailure, settings);
                return;
            }

            if (!ValidateLodCoverage(settings, out string lodFailure))
            {
                Debug.LogError("Terrain validation failed: " + lodFailure, settings);
                return;
            }

            controller.RegenerateTerrain();
            controller.GetRequiredFiniteChunkBounds(out Vector2Int boundsMin, out Vector2Int boundsMax);
            int expectedChunkCount = (boundsMax.x - boundsMin.x + 1) * (boundsMax.y - boundsMin.y + 1);
            int expectedVertices = expectedChunkCount * settings.chunkResolution * settings.chunkResolution;
            int cellsPerSide = settings.chunkResolution - 1;
            int expectedTriangles = expectedChunkCount * cellsPerSide * cellsPerSide * 2;
            if (!controller.HasGeneratedTerrain || controller.GeneratedChunkCount != expectedChunkCount ||
                controller.GeneratedVertexCount != expectedVertices ||
                controller.GeneratedTriangleCount != expectedTriangles)
            {
                Debug.LogError(
                    $"Terrain validation failed: expected {expectedChunkCount} chunks, found {controller.GeneratedChunkCount}, " +
                    $"expected {expectedVertices} vertices/{expectedTriangles} triangles, " +
                    $"found {controller.GeneratedVertexCount}/{controller.GeneratedTriangleCount}.",
                    controller);
                return;
            }

            if (!settings.UsesWindowedFiniteMap &&
                !ValidateWorldFeatures(controller, settings, out string featureFailure))
            {
                Debug.LogError("Terrain validation failed: " + featureFailure, controller);
                return;
            }

            for (int z = boundsMin.y; z <= boundsMax.y; z++)
            {
                for (int x = boundsMin.x; x <= boundsMax.x; x++)
                {
                    if (settings.UsesWindowedFiniteMap &&
                        (!controller.TryGetTerrainChunk(new Vector2Int(x, z), out TerrainChunk windowChunk) ||
                         !windowChunk.WorldFeaturesReady))
                    {
                        Debug.LogError($"Terrain validation failed: streaming features for chunk ({x}, {z}) are not ready.", controller);
                        return;
                    }

                    if (!controller.TryGetGeneratedMesh(new Vector2Int(x, z), out Mesh generatedMesh))
                    {
                        Debug.LogError($"Terrain validation failed: chunk ({x}, {z}) has no generated mesh.", controller);
                        return;
                    }

                    Bounds bounds = generatedMesh.bounds;
                    if (!IsFinite(bounds.min.y) || !IsFinite(bounds.max.y))
                    {
                        Debug.LogError($"Terrain validation failed: chunk ({x}, {z}) has non-finite vertical bounds.", controller);
                        return;
                    }

                    if (!Mathf.Approximately(bounds.min.x, 0f) ||
                        !Mathf.Approximately(bounds.min.z, 0f) ||
                        !Mathf.Approximately(bounds.size.x, settings.chunkSize) ||
                        !Mathf.Approximately(bounds.size.z, settings.chunkSize))
                    {
                        Debug.LogError(
                            $"Terrain validation failed: chunk ({x}, {z}) local mesh bounds are min " +
                            $"({bounds.min.x:F3}, {bounds.min.z:F3}), size {bounds.size.x:F3} x {bounds.size.z:F3}; " +
                            $"expected min (0, 0), size {settings.chunkSize:F3}m x {settings.chunkSize:F3}m.",
                            controller);
                        return;
                    }

                    Transform chunkTransform = controller.transform.Find($"TerrainChunk_{x}_{z}");
                    Vector2 expectedOrigin = settings.GetChunkWorldOrigin(new Vector2Int(x, z));
                    if (chunkTransform == null ||
                        !Mathf.Approximately(chunkTransform.localPosition.x, expectedOrigin.x) ||
                        !Mathf.Approximately(chunkTransform.localPosition.z, expectedOrigin.y))
                    {
                        Debug.LogError(
                            $"Terrain validation failed: chunk ({x}, {z}) origin does not match " +
                            $"({expectedOrigin.x:F3}, {expectedOrigin.y:F3}) meters.",
                            controller);
                        return;
                    }
                }
            }

            if (!controller.HasVisibleTerrainRenderer)
            {
                Debug.LogError("Terrain validation failed: generated mesh has no enabled renderer.", controller);
                return;
            }

            Debug.Log(
                $"Terrain validation passed: {expectedChunkCount} loaded chunks " +
                $"(map {settings.finiteMapChunksPerSide}x{settings.finiteMapChunksPerSide}{(settings.UsesWindowedFiniteMap ? ", windowed" : string.Empty)}), " +
                $"{settings.chunkSize:F0}m chunks, {settings.chunkResolution}x{settings.chunkResolution} vertices, " +
                $"{settings.CellSize:F2}m cells, {settings.FiniteMapWorldSize:F0}m finite map, " +
                $"seed {settings.noiseSettings.seed}, " +
                $"{controller.GeneratedVertexCount:N0} vertices, {controller.GeneratedTriangleCount:N0} triangles, shader {controller.TerrainShaderName}.",
                controller);
        }

        static bool ValidateDeterministicNoise(NoiseSettings settings, out string failure)
        {
            float[,] first = Noise.GenerateNoiseMap(17, 17, settings, Vector2.zero);
            float[,] repeated = Noise.GenerateNoiseMap(17, 17, settings, Vector2.zero);
            NoiseSettings differentSeed = new NoiseSettings
            {
                seed = settings.seed + 1,
                scale = settings.scale,
                offset = settings.offset,
                octaves = settings.octaves,
                persistence = settings.persistence,
                lacunarity = settings.lacunarity,
                normalize = settings.normalize
            };
            float[,] changedSeed = Noise.GenerateNoiseMap(17, 17, differentSeed, Vector2.zero);
            bool changed = false;

            for (int y = 0; y < 17; y++)
            {
                for (int x = 0; x < 17; x++)
                {
                    if (!Mathf.Approximately(first[x, y], repeated[x, y]))
                    {
                        failure = "same seed and settings produced different height values.";
                        return false;
                    }

                    if (!Mathf.Approximately(first[x, y], changedSeed[x, y]))
                    {
                        changed = true;
                    }
                }
            }

            failure = changed ? string.Empty : "changing the seed did not change the noise map.";
            return changed;
        }

        static bool ValidateWorldFeatures(
            TerrainWorldController controller,
            TerrainSettings settings,
            out string failure)
        {
            System.Collections.Generic.Dictionary<Vector2Int, TerrainChunk> chunks =
                new System.Collections.Generic.Dictionary<Vector2Int, TerrainChunk>();
            System.Collections.Generic.Dictionary<Vector2Int, float[,]> roadSnapshots =
                new System.Collections.Generic.Dictionary<Vector2Int, float[,]>();
            System.Collections.Generic.Dictionary<Vector2Int, float[,]> poiSnapshots =
                new System.Collections.Generic.Dictionary<Vector2Int, float[,]>();
            System.Collections.Generic.Dictionary<string, TerrainPOIDefinition> definitions =
                new System.Collections.Generic.Dictionary<string, TerrainPOIDefinition>();
            System.Collections.Generic.Dictionary<string, int> placedCounts =
                new System.Collections.Generic.Dictionary<string, int>();
            int radius = settings.finiteMapChunksPerSide / 2;
            int targetPoiCount = 0;
            for (int i = 0; i < settings.poiDefinitions.Count; i++)
            {
                TerrainPOIDefinition definition = settings.poiDefinitions[i];
                targetPoiCount += definition.targetCount;
                definitions.Add(definition.id, definition);
            }

            if (controller.GeneratedPOIs.Count > targetPoiCount)
            {
                failure = $"placed {controller.GeneratedPOIs.Count} POIs, exceeding the configured maximum {targetPoiCount}.";
                return false;
            }

            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    Vector2Int coordinate = new Vector2Int(x, z);
                    if (!controller.TryGetTerrainChunk(coordinate, out TerrainChunk chunk) ||
                        chunk.GeneratedData == null)
                    {
                        failure = $"finite-world data for chunk ({x}, {z}) is unavailable.";
                        return false;
                    }

                    chunks.Add(coordinate, chunk);
                    roadSnapshots.Add(coordinate, CloneMap(chunk.GeneratedData.Roads));
                    poiSnapshots.Add(coordinate, CloneMap(chunk.GeneratedData.PoiMask));
                }
            }

            for (int i = 0; i < controller.GeneratedPOIs.Count; i++)
            {
                TerrainWorldPOI poi = controller.GeneratedPOIs[i];
                if (poi == null || string.IsNullOrWhiteSpace(poi.Id) ||
                    !IsFinite(poi.TerrainPosition.x) || !IsFinite(poi.TerrainPosition.y) ||
                    !IsFinite(poi.TerrainPosition.z))
                {
                    failure = $"POI {i} contains an invalid id or non-finite position.";
                    return false;
                }

                if (!definitions.TryGetValue(poi.Id, out TerrainPOIDefinition definition))
                {
                    failure = $"POI {poi.Id} has no matching settings definition.";
                    return false;
                }

                placedCounts.TryGetValue(poi.Id, out int count);
                count++;
                placedCounts[poi.Id] = count;
                if (count > definition.targetCount)
                {
                    failure = $"POI definition {poi.Id} exceeded its target count.";
                    return false;
                }

                for (int otherIndex = 0; otherIndex < i; otherIndex++)
                {
                    TerrainWorldPOI other = controller.GeneratedPOIs[otherIndex];
                    TerrainPOIDefinition otherDefinition = definitions[other.Id];
                    float minSpacing = Mathf.Max(
                        definition.minimumSpacingMeters,
                        otherDefinition.minimumSpacingMeters);
                    if (Vector3.Distance(poi.TerrainPosition, other.TerrainPosition) + 0.001f < minSpacing)
                    {
                        failure = $"POIs {other.Id} and {poi.Id} violate the configured minimum spacing.";
                        return false;
                    }
                }
            }

            TerrainWorldFeatureGenerationResult repeated = TerrainWorldFeatures.Generate(settings, chunks);
            if (repeated.PointsOfInterest.Count != controller.GeneratedPOIs.Count ||
                repeated.UnconnectedRoutes != controller.GeneratedUnconnectedRoutes)
            {
                failure = "POI or road generation changed its output when repeated with the same seed and chunk data.";
                return false;
            }

            for (int i = 0; i < repeated.PointsOfInterest.Count; i++)
            {
                TerrainWorldPOI expected = controller.GeneratedPOIs[i];
                TerrainWorldPOI actual = repeated.PointsOfInterest[i];
                if (expected.Id != actual.Id ||
                    expected.ChunkCoordinate != actual.ChunkCoordinate ||
                    Vector3.Distance(expected.TerrainPosition, actual.TerrainPosition) > 0.0001f)
                {
                    failure = $"POI output is not deterministic at index {i}.";
                    return false;
                }
            }

            foreach (KeyValuePair<Vector2Int, TerrainChunk> pair in chunks)
            {
                HeightMap map = pair.Value.GeneratedData;
                float[,] roadsBefore = roadSnapshots[pair.Key];
                float[,] poisBefore = poiSnapshots[pair.Key];
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (!Mathf.Approximately(roadsBefore[x, y], map.Roads[x, y]) ||
                            !Mathf.Approximately(poisBefore[x, y], map.PoiMask[x, y]))
                        {
                            failure = $"road/POI maps changed on a repeated generation in chunk {pair.Key}.";
                            return false;
                        }
                    }
                }

                int expectedVisuals = 0;
                foreach (TerrainWorldPOI poi in controller.GeneratedPOIs)
                {
                    if (poi.ChunkCoordinate != pair.Key)
                    {
                        continue;
                    }

                    TerrainPOIDefinition definition = definitions[poi.Id];
                    if (definition.visualPrefab != null || definition.useFallbackVisual)
                    {
                        expectedVisuals++;
                    }
                }

                if (!pair.Value.WorldFeaturesReady || pair.Value.GeneratedPOIVisualCount != expectedVisuals)
                {
                    failure = $"POI visuals are missing or duplicated in chunk {pair.Key}: expected {expectedVisuals}, " +
                        $"found {pair.Value.GeneratedPOIVisualCount}.";
                    return false;
                }

                bool hasRoads = ContainsMarkedSample(map.Roads);
                if (hasRoads != pair.Value.HasGeneratedRoadMesh ||
                    (hasRoads && (pair.Value.GeneratedRoadMesh == null ||
                     pair.Value.GeneratedRoadMesh.triangles.Length == 0)))
                {
                    failure = $"road mesh does not match the road map in chunk {pair.Key}.";
                    return false;
                }

                if (pair.Key.x < radius)
                {
                    HeightMap east = chunks[new Vector2Int(pair.Key.x + 1, pair.Key.y)].GeneratedData;
                    for (int y = 0; y < map.Height; y++)
                    {
                        if (!Mathf.Approximately(map.Roads[map.Width - 1, y], east.Roads[0, y]) ||
                            !Mathf.Approximately(map.PoiMask[map.Width - 1, y], east.PoiMask[0, y]))
                        {
                            failure = $"road/POI maps do not match across the +X seam at chunk {pair.Key}, sample {y}.";
                            return false;
                        }
                    }
                }

                if (pair.Key.y < radius)
                {
                    HeightMap north = chunks[new Vector2Int(pair.Key.x, pair.Key.y + 1)].GeneratedData;
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (!Mathf.Approximately(map.Roads[x, 0], north.Roads[x, north.Height - 1]) ||
                            !Mathf.Approximately(map.PoiMask[x, 0], north.PoiMask[x, north.Height - 1]))
                        {
                            failure = $"road/POI maps do not match across the +Z seam at chunk {pair.Key}, sample {x}.";
                            return false;
                        }
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        static bool ValidateStreamingFeatures(TerrainSettings settings, out string failure)
        {
            TerrainSettings testSettings = UnityEngine.Object.Instantiate(settings);
            try
            {
                Vector2Int westCoordinate = new Vector2Int(-1, 2);
                Vector2Int eastCoordinate = new Vector2Int(0, 2);
                HeightMap west = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    testSettings.chunkResolution,
                    testSettings.chunkResolution,
                    testSettings.GetChunkWorldOrigin(westCoordinate));
                HeightMap east = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    testSettings.chunkResolution,
                    testSettings.chunkResolution,
                    testSettings.GetChunkWorldOrigin(eastCoordinate));
                TerrainWorldFeatureGenerationResult westFirst = TerrainWorldFeatures.GenerateForStreamingChunk(
                    testSettings,
                    westCoordinate,
                    west);
                TerrainWorldFeatureGenerationResult eastSecond = TerrainWorldFeatures.GenerateForStreamingChunk(
                    testSettings,
                    eastCoordinate,
                    east);
                float[,] westRoads = CloneMap(west.Roads);
                float[,] eastRoads = CloneMap(east.Roads);
                float[,] westPois = CloneMap(west.PoiMask);
                float[,] eastPois = CloneMap(east.PoiMask);

                HeightMap eastReloaded = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    testSettings.chunkResolution,
                    testSettings.chunkResolution,
                    testSettings.GetChunkWorldOrigin(eastCoordinate));
                TerrainWorldFeatureGenerationResult eastFirst = TerrainWorldFeatures.GenerateForStreamingChunk(
                    testSettings,
                    eastCoordinate,
                    eastReloaded);
                HeightMap westReloaded = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    testSettings.chunkResolution,
                    testSettings.chunkResolution,
                    testSettings.GetChunkWorldOrigin(westCoordinate));
                TerrainWorldFeatureGenerationResult westSecond = TerrainWorldFeatures.GenerateForStreamingChunk(
                    testSettings,
                    westCoordinate,
                    westReloaded);

                if (!FeatureResultsMatch(westFirst, westSecond) ||
                    !FeatureResultsMatch(eastSecond, eastFirst) ||
                    !MapsMatch(westRoads, westReloaded.Roads) ||
                    !MapsMatch(eastRoads, eastReloaded.Roads) ||
                    !MapsMatch(westPois, westReloaded.PoiMask) ||
                    !MapsMatch(eastPois, eastReloaded.PoiMask))
                {
                    failure = "streaming POIs or roads changed after regenerating neighboring chunks in a different order.";
                    return false;
                }

                for (int y = 0; y < west.Height; y++)
                {
                    if (!Mathf.Approximately(
                            westReloaded.Roads[westReloaded.Width - 1, y],
                            eastReloaded.Roads[0, y]) ||
                        !Mathf.Approximately(
                            westReloaded.PoiMask[westReloaded.Width - 1, y],
                            eastReloaded.PoiMask[0, y]))
                    {
                        failure = $"streaming world-feature data does not match across the chunk edge at sample {y}.";
                        return false;
                    }
                }

                HeightMap blocked = new HeightMap(
                    new float[testSettings.chunkResolution, testSettings.chunkResolution],
                    water: FilledMap(testSettings.chunkResolution, 1f),
                    navigation: new float[testSettings.chunkResolution, testSettings.chunkResolution]);
                TerrainWorldFeatureGenerationResult blockedResult =
                    TerrainWorldFeatures.GenerateForStreamingChunk(testSettings, Vector2Int.zero, blocked);
                if (blockedResult.PointsOfInterest.Count != 0 ||
                    !Mathf.Approximately(blockedResult.UnconnectedRoutes, testSettings.connectPOIsWithRoads &&
                        testSettings.roadCount > 0 ? 4f : 0f) ||
                    ContainsMarkedSample(blocked.Roads))
                {
                    failure = "blocked streaming terrain generated a POI or a road through water.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testSettings);
            }
        }

        static bool FeatureResultsMatch(
            TerrainWorldFeatureGenerationResult first,
            TerrainWorldFeatureGenerationResult second)
        {
            if (first.UnconnectedRoutes != second.UnconnectedRoutes ||
                first.PointsOfInterest.Count != second.PointsOfInterest.Count)
            {
                return false;
            }

            for (int i = 0; i < first.PointsOfInterest.Count; i++)
            {
                TerrainWorldPOI left = first.PointsOfInterest[i];
                TerrainWorldPOI right = second.PointsOfInterest[i];
                if (left.Id != right.Id || left.ChunkCoordinate != right.ChunkCoordinate ||
                    Vector3.Distance(left.TerrainPosition, right.TerrainPosition) > 0.0001f)
                {
                    return false;
                }
            }

            return true;
        }

        static bool MapsMatch(float[,] first, float[,] second)
        {
            if (first.GetLength(0) != second.GetLength(0) ||
                first.GetLength(1) != second.GetLength(1))
            {
                return false;
            }

            for (int y = 0; y < first.GetLength(1); y++)
            {
                for (int x = 0; x < first.GetLength(0); x++)
                {
                    if (!Mathf.Approximately(first[x, y], second[x, y]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static bool ContainsMarkedSample(float[,] map)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                for (int x = 0; x < map.GetLength(0); x++)
                {
                    if (map[x, y] > 0.5f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static float[,] FilledMap(int resolution, float value)
        {
            float[,] map = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    map[x, y] = value;
                }
            }

            return map;
        }

        static float[,] CloneMap(float[,] source)
        {
            float[,] copy = new float[source.GetLength(0), source.GetLength(1)];
            System.Array.Copy(source, copy, source.Length);
            return copy;
        }

        static bool ValidateHeightMapWithUnnormalizedNoise(TerrainSettings settings, out string failure)
        {
            TerrainSettings testSettings = UnityEngine.Object.Instantiate(settings);
            try
            {
                testSettings.noiseSettings.normalize = false;
                HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(testSettings, 17, 17, Vector2.zero);
                for (int y = 0; y < 17; y++)
                {
                    for (int x = 0; x < 17; x++)
                    {
                        float value = heightMap.Values[x, y];
                        if (!IsFinite(value))
                        {
                            failure = $"unnormalized noise produced a non-finite height at ({x}, {y}).";
                            return false;
                        }
                    }
                }

                failure = string.Empty;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testSettings);
            }
        }

        static bool ValidateGeneratedTerrainData(TerrainSettings settings, out string failure)
        {
            const int sampleSize = 17;
            Vector2 origin = settings.GetChunkWorldOrigin(new Vector2Int(-2, 3));
            HeightMap first = HeightMapGenerator.GenerateHeightMap(settings, sampleSize, sampleSize, origin);
            HeightMap repeated = HeightMapGenerator.GenerateHeightMap(settings, sampleSize, sampleSize, origin);
            float[][,] maps =
            {
                first.Values,
                first.WorldHeights,
                first.Biomes,
                first.Slopes,
                first.Moisture,
                first.Temperature,
                first.Water,
                first.Navigation,
                first.PoiSuitability,
                first.PoiMask,
                first.Roads
            };
            float[][,] repeatedMaps =
            {
                repeated.Values,
                repeated.WorldHeights,
                repeated.Biomes,
                repeated.Slopes,
                repeated.Moisture,
                repeated.Temperature,
                repeated.Water,
                repeated.Navigation,
                repeated.PoiSuitability,
                repeated.PoiMask,
                repeated.Roads
            };

            for (int mapIndex = 0; mapIndex < maps.Length; mapIndex++)
            {
                for (int y = 0; y < sampleSize; y++)
                {
                    for (int x = 0; x < sampleSize; x++)
                    {
                        float value = maps[mapIndex][x, y];
                        if (!IsFinite(value) || !Mathf.Approximately(value, repeatedMaps[mapIndex][x, y]))
                        {
                            failure = $"terrain data map {mapIndex} is non-finite or non-deterministic at ({x}, {y}).";
                            return false;
                        }
                    }
                }
            }

            for (int y = 0; y < sampleSize; y++)
            {
                for (int x = 0; x < sampleSize; x++)
                {
                    if (first.Moisture[x, y] < 0f || first.Moisture[x, y] > 1f ||
                        first.Temperature[x, y] < 0f || first.Temperature[x, y] > 1f ||
                        first.Water[x, y] < 0f || first.Water[x, y] > 1f ||
                        first.Navigation[x, y] < 0f || first.Navigation[x, y] > 3f ||
                        first.PoiSuitability[x, y] < 0f || first.PoiSuitability[x, y] > 1f ||
                        first.PoiMask[x, y] < 0f || first.PoiMask[x, y] > 1f ||
                        first.Roads[x, y] < 0f || first.Roads[x, y] > 1f)
                    {
                        failure = $"terrain data map values are outside their documented ranges at ({x}, {y}).";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        static bool ValidateWorldCoordinateSampling(NoiseSettings settings, out string failure)
        {
            settings.Validate();
            const int coarseResolution = 7;
            const int fineResolution = (coarseResolution - 1) * 2 + 1;
            Vector2 worldOrigin = new Vector2(-30f, 30f) + settings.offset;
            float[,] coarse = Noise.GenerateNoiseMap(
                coarseResolution,
                coarseResolution,
                settings.scale,
                settings.octaves,
                settings.persistence,
                settings.lacunarity,
                settings.seed,
                worldOrigin,
                new Vector2(20f, -20f),
                settings.normalize);
            float[,] fine = Noise.GenerateNoiseMap(
                fineResolution,
                fineResolution,
                settings.scale,
                settings.octaves,
                settings.persistence,
                settings.lacunarity,
                settings.seed,
                worldOrigin,
                new Vector2(10f, -10f),
                settings.normalize);

            for (int y = 0; y < coarseResolution; y++)
            {
                for (int x = 0; x < coarseResolution; x++)
                {
                    if (Mathf.Abs(coarse[x, y] - fine[x * 2, y * 2]) > 0.0001f)
                    {
                        failure = $"the same world point produced different noise at resolutions 7 and 13 ({x}, {y}).";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        static bool ValidateChunkSeams(TerrainSettings settings, out string failure)
        {
            TerrainSettings testSettings = UnityEngine.Object.Instantiate(settings);
            try
            {
                testSettings.generateIsland = false;
                const int sampleSize = 33;
                Vector2Int originCoordinate = Vector2Int.zero;
                Vector2Int eastCoordinate = Vector2Int.right;
                Vector2Int northCoordinate = Vector2Int.up;
                HeightMap left = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    sampleSize,
                    sampleSize,
                    testSettings.GetChunkWorldOrigin(originCoordinate));
                HeightMap right = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    sampleSize,
                    sampleSize,
                    testSettings.GetChunkWorldOrigin(eastCoordinate));
                HeightMap north = HeightMapGenerator.GenerateHeightMap(
                    testSettings,
                    sampleSize,
                    sampleSize,
                    testSettings.GetChunkWorldOrigin(northCoordinate));
                MeshData leftMesh = MeshGenerator.GenerateTerrainMesh(
                    left.Values,
                    testSettings,
                    0,
                    left.NormalHeights);
                MeshData rightMesh = MeshGenerator.GenerateTerrainMesh(
                    right.Values,
                    testSettings,
                    0,
                    right.NormalHeights);
                MeshData northMesh = MeshGenerator.GenerateTerrainMesh(
                    north.Values,
                    testSettings,
                    0,
                    north.NormalHeights);
                Color[] leftColors = TextureGenerator.BuildColorMap(
                    left, testSettings, testSettings.GetChunkWorldOrigin(originCoordinate));
                Color[] rightColors = TextureGenerator.BuildColorMap(
                    right, testSettings, testSettings.GetChunkWorldOrigin(eastCoordinate));
                Color[] northColors = TextureGenerator.BuildColorMap(
                    north, testSettings, testSettings.GetChunkWorldOrigin(northCoordinate));

                for (int y = 0; y < sampleSize; y++)
                {
                    if (Mathf.Abs(left.Values[sampleSize - 1, y] - right.Values[0, y]) > 0.0001f ||
                        Mathf.Abs(left.WorldHeights[sampleSize - 1, y] - right.WorldHeights[0, y]) > 0.0001f ||
                        Mathf.Abs(left.Biomes[sampleSize - 1, y] - right.Biomes[0, y]) > 0.0001f ||
                        Mathf.Abs(left.Slopes[sampleSize - 1, y] - right.Slopes[0, y]) > 0.0001f ||
                        Mathf.Abs(left.Moisture[sampleSize - 1, y] - right.Moisture[0, y]) > 0.0001f ||
                        Mathf.Abs(left.Temperature[sampleSize - 1, y] - right.Temperature[0, y]) > 0.0001f ||
                        Mathf.Abs(left.Water[sampleSize - 1, y] - right.Water[0, y]) > 0.0001f ||
                        Mathf.Abs(left.Navigation[sampleSize - 1, y] - right.Navigation[0, y]) > 0.0001f ||
                        !ColorsMatch(
                            leftColors[y * sampleSize + sampleSize - 1],
                            rightColors[y * sampleSize]))
                    {
                        failure = $"chunks (0,0) and (1,0) do not share matching terrain, biome, or colors at edge sample {y}.";
                        return false;
                    }

                    int leftEdgeVertex = y * sampleSize + sampleSize - 1;
                    int rightEdgeVertex = y * sampleSize;
                    if (Mathf.Abs(leftMesh.vertices[leftEdgeVertex].y - rightMesh.vertices[rightEdgeVertex].y) > 0.0001f ||
                        Vector3.Distance(leftMesh.normals[leftEdgeVertex], rightMesh.normals[rightEdgeVertex]) > 0.0001f)
                    {
                        failure = $"chunks (0,0) and (1,0) do not share matching surface normals at edge sample {y}.";
                        return false;
                    }

                    if (Mathf.Abs(left.Values[y, 0] - north.Values[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.WorldHeights[y, 0] - north.WorldHeights[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.Biomes[y, 0] - north.Biomes[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.Slopes[y, 0] - north.Slopes[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.Moisture[y, 0] - north.Moisture[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.Temperature[y, 0] - north.Temperature[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.Water[y, 0] - north.Water[y, sampleSize - 1]) > 0.0001f ||
                        Mathf.Abs(left.Navigation[y, 0] - north.Navigation[y, sampleSize - 1]) > 0.0001f ||
                        !ColorsMatch(
                            leftColors[y],
                            northColors[(sampleSize - 1) * sampleSize + y]))
                    {
                        failure = $"chunks (0,0) and (0,1) do not share matching terrain, biome, or colors at edge sample {y}.";
                        return false;
                    }

                    int upperEdgeVertex = y;
                    int lowerEdgeVertex = (sampleSize - 1) * sampleSize + y;
                    if (Mathf.Abs(leftMesh.vertices[upperEdgeVertex].y - northMesh.vertices[lowerEdgeVertex].y) > 0.0001f ||
                        Vector3.Distance(leftMesh.normals[upperEdgeVertex], northMesh.normals[lowerEdgeVertex]) > 0.0001f)
                    {
                        failure = $"chunks (0,0) and (0,1) do not share matching surface normals at edge sample {y}.";
                        return false;
                    }
                }

                failure = string.Empty;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testSettings);
            }
        }

        static bool ValidateLodCoverage(TerrainSettings settings, out string failure)
        {
            TerrainSettings testSettings = UnityEngine.Object.Instantiate(settings);
            try
            {
                float[,] flatHeightMap = new float[testSettings.chunkResolution, testSettings.chunkResolution];
                int[] increments = { 1, 2, 4, 8, 16 };
                for (int i = 0; i < increments.Length; i++)
                {
                    int increment = increments[i];
                    testSettings.lods = new System.Collections.Generic.List<LODInfo>
                    {
                        new LODInfo { lod = 0, meshResolution = increment }
                    };
                    MeshData meshData = MeshGenerator.GenerateTerrainMesh(flatHeightMap, testSettings, 0);
                    float minX = float.PositiveInfinity;
                    float maxX = float.NegativeInfinity;
                    float minZ = float.PositiveInfinity;
                    float maxZ = float.NegativeInfinity;
                    for (int vertex = 0; vertex < meshData.vertices.Length; vertex++)
                    {
                        Vector3 point = meshData.vertices[vertex];
                        minX = Mathf.Min(minX, point.x);
                        maxX = Mathf.Max(maxX, point.x);
                        minZ = Mathf.Min(minZ, point.z);
                        maxZ = Mathf.Max(maxZ, point.z);
                    }

                    if (!Mathf.Approximately(minX, 0f) ||
                        !Mathf.Approximately(minZ, 0f) ||
                        !Mathf.Approximately(maxX, testSettings.chunkSize) ||
                        !Mathf.Approximately(maxZ, testSettings.chunkSize))
                    {
                        failure = $"LOD simplification increment {increment} covers {maxX - minX:F3}m x " +
                            $"{maxZ - minZ:F3}m instead of {testSettings.chunkSize:F3}m x {testSettings.chunkSize:F3}m.";
                        return false;
                    }
                }

                failure = string.Empty;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testSettings);
            }
        }

        static bool ColorsMatch(Color first, Color second)
        {
            return Mathf.Abs(first.r - second.r) <= 0.0001f &&
                Mathf.Abs(first.g - second.g) <= 0.0001f &&
                Mathf.Abs(first.b - second.b) <= 0.0001f &&
                Mathf.Abs(first.a - second.a) <= 0.0001f;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [CustomEditor(typeof(TerrainSettings))]
    public class TerrainSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool settingsChanged = EditorGUI.EndChangeCheck();

            TerrainSettings settings = (TerrainSettings)target;
            if (settingsChanged)
            {
                EditorUtility.SetDirty(settings);
                ApplySettings(settings);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Apply Settings to Scene Terrain"))
            {
                ApplySettings(settings);
            }

            if (GUILayout.Button("Randomize Seed and Apply"))
            {
                Undo.RecordObject(settings, "Randomize Terrain Seed");
                settings.noiseSettings.seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                EditorUtility.SetDirty(settings);
                ApplySettings(settings);
            }
        }

        static void ApplySettings(TerrainSettings settings)
        {
            settings.Validate();
            EditorUtility.SetDirty(settings);
            TerrainWorldController[] controllers = UnityEngine.Object.FindObjectsByType<TerrainWorldController>(
                FindObjectsInactive.Include);

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i].terrainSettings == settings)
                {
                    controllers[i].RegenerateTerrain();
                    SceneView.lastActiveSceneView?.Repaint();
                }
            }
        }
    }
}
