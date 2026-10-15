using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public sealed class TerrainWorldPOI
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Vector2Int ChunkCoordinate { get; }
        public Vector3 TerrainPosition { get; }
        public float MinimumSpacingMeters { get; }

        public TerrainWorldPOI(
            string id,
            string displayName,
            Vector2Int chunkCoordinate,
            Vector3 terrainPosition,
            float minimumSpacingMeters)
        {
            Id = id;
            DisplayName = displayName;
            ChunkCoordinate = chunkCoordinate;
            TerrainPosition = terrainPosition;
            MinimumSpacingMeters = minimumSpacingMeters;
        }
    }

    public sealed class TerrainWorldFeatureGenerationResult
    {
        public IReadOnlyList<TerrainWorldPOI> PointsOfInterest { get; }
        public int UnconnectedRoutes { get; }

        public TerrainWorldFeatureGenerationResult(List<TerrainWorldPOI> pointsOfInterest, int unconnectedRoutes)
        {
            PointsOfInterest = pointsOfInterest.AsReadOnly();
            UnconnectedRoutes = unconnectedRoutes;
        }
    }

    public static class TerrainWorldFeatures
    {
        sealed class PoiCandidate
        {
            public TerrainPOIDefinition Definition;
            public Vector2Int ChunkCoordinate;
            public int MapX;
            public int MapY;
            public int WorldX;
            public int WorldY;
            public float Score;
        }

        readonly struct HeapEntry
        {
            public readonly int index;
            public readonly float priority;

            public HeapEntry(int index, float priority)
            {
                this.index = index;
                this.priority = priority;
            }
        }

        sealed class MinHeap
        {
            readonly List<HeapEntry> entries = new List<HeapEntry>();

            public int Count => entries.Count;

            public void Push(int index, float priority)
            {
                HeapEntry entry = new HeapEntry(index, priority);
                entries.Add(entry);
                int current = entries.Count - 1;
                while (current > 0)
                {
                    int parent = (current - 1) / 2;
                    if (!ComesBefore(entry, entries[parent]))
                    {
                        break;
                    }

                    entries[current] = entries[parent];
                    current = parent;
                }

                entries[current] = entry;
            }

            public HeapEntry Pop()
            {
                HeapEntry result = entries[0];
                HeapEntry last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0)
                {
                    return result;
                }

                int current = 0;
                while (true)
                {
                    int left = current * 2 + 1;
                    if (left >= entries.Count)
                    {
                        break;
                    }

                    int right = left + 1;
                    int child = right < entries.Count && ComesBefore(entries[right], entries[left])
                        ? right
                        : left;
                    if (!ComesBefore(entries[child], last))
                    {
                        break;
                    }

                    entries[current] = entries[child];
                    current = child;
                }

                entries[current] = last;
                return result;
            }

            static bool ComesBefore(HeapEntry first, HeapEntry second)
            {
                return first.priority < second.priority ||
                    (Mathf.Approximately(first.priority, second.priority) && first.index < second.index);
            }
        }

        public static TerrainWorldFeatureGenerationResult Generate(
            TerrainSettings settings,
            IDictionary<Vector2Int, TerrainChunk> chunks)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (chunks == null)
            {
                throw new ArgumentNullException(nameof(chunks));
            }

            settings.Validate();
            int chunksPerSide = settings.finiteMapChunksPerSide;
            int radius = chunksPerSide / 2;
            int resolution = settings.chunkResolution;
            int cellCountPerChunk = resolution - 1;
            int worldSize = chunksPerSide * cellCountPerChunk + 1;
            float cellSize = settings.GetCellSize(resolution);
            HeightMap[,] chunkMaps = new HeightMap[chunksPerSide, chunksPerSide];
            float[,] slopes = new float[worldSize, worldSize];
            float[,] worldHeights = new float[worldSize, worldSize];
            float[,] moisture = new float[worldSize, worldSize];
            float[,] water = new float[worldSize, worldSize];
            float[,] navigation = new float[worldSize, worldSize];
            float[,] suitability = new float[worldSize, worldSize];
            float[,] poiMask = new float[worldSize, worldSize];
            float[,] roads = new float[worldSize, worldSize];

            for (int chunkZ = -radius; chunkZ <= radius; chunkZ++)
            {
                for (int chunkX = -radius; chunkX <= radius; chunkX++)
                {
                    Vector2Int coordinate = new Vector2Int(chunkX, chunkZ);
                    if (!chunks.TryGetValue(coordinate, out TerrainChunk chunk) ||
                        chunk == null || !chunk.HasGeneratedMesh || chunk.GeneratedData == null)
                    {
                        throw new InvalidOperationException($"Finite world is missing generated data for chunk {coordinate}.");
                    }

                    HeightMap map = chunk.GeneratedData;
                    chunkMaps[chunkX + radius, radius - chunkZ] = map;
                    int offsetX = (chunkX + radius) * cellCountPerChunk;
                    int offsetY = (radius - chunkZ) * cellCountPerChunk;
                    for (int y = 0; y < resolution; y++)
                    {
                        for (int x = 0; x < resolution; x++)
                        {
                            int worldX = offsetX + x;
                            int worldY = offsetY + y;
                            worldHeights[worldX, worldY] = map.WorldHeights != null
                                ? map.WorldHeights[x, y]
                                : settings.heightCurve.Evaluate(Mathf.Clamp01(map.Values[x, y])) * settings.heightMultiplier;
                            slopes[worldX, worldY] = map.Slopes[x, y];
                            moisture[worldX, worldY] = map.Moisture[x, y];
                            water[worldX, worldY] = map.Water[x, y];
                            navigation[worldX, worldY] = map.Navigation[x, y];
                            suitability[worldX, worldY] = map.PoiSuitability[x, y];
                        }
                    }
                }
            }

            List<TerrainWorldPOI> pois = PlacePOIs(
                settings,
                chunkMaps,
                radius,
                cellCountPerChunk,
                cellSize,
                slopes,
                worldHeights,
                moisture,
                water,
                navigation,
                suitability,
                poiMask);

            int unconnectedRoutes = 0;
            if (settings.connectPOIsWithRoads && settings.roadCount > 0)
            {
                for (int i = 1; i < pois.Count; i++)
                {
                    int startX = Mathf.RoundToInt((pois[i - 1].TerrainPosition.x + radius * settings.chunkSize) / cellSize);
                    int startY = Mathf.RoundToInt(((radius + 1) * settings.chunkSize - pois[i - 1].TerrainPosition.z) / cellSize);
                    int targetX = Mathf.RoundToInt((pois[i].TerrainPosition.x + radius * settings.chunkSize) / cellSize);
                    int targetY = Mathf.RoundToInt(((radius + 1) * settings.chunkSize - pois[i].TerrainPosition.z) / cellSize);
                    if (!TryFindPath(startX, startY, targetX, targetY, navigation, water, out List<int> path))
                    {
                        unconnectedRoutes++;
                        continue;
                    }

                    foreach (int index in path)
                    {
                        int x = index % worldSize;
                        int y = index / worldSize;
                        for (int dy = -settings.roadWidthCells; dy <= settings.roadWidthCells; dy++)
                        {
                            for (int dx = -settings.roadWidthCells; dx <= settings.roadWidthCells; dx++)
                            {
                                int roadX = x + dx;
                                int roadY = y + dy;
                                if (roadX >= 0 && roadX < worldSize && roadY >= 0 && roadY < worldSize &&
                                    water[roadX, roadY] <= 0.5f && navigation[roadX, roadY] > 0f)
                                {
                                    roads[roadX, roadY] = 1f;
                                }
                            }
                        }
                    }
                }
            }

            CopyWorldFeaturesToChunks(
                settings,
                chunkMaps,
                radius,
                cellCountPerChunk,
                worldSize,
                poiMask,
                roads);
            return new TerrainWorldFeatureGenerationResult(pois, unconnectedRoutes);
        }

        public static TerrainWorldFeatureGenerationResult GenerateForStreamingChunk(
            TerrainSettings settings,
            Vector2Int coordinate,
            HeightMap map)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (map == null || map.Width != settings.chunkResolution || map.Height != settings.chunkResolution)
            {
                throw new ArgumentException(
                    "Streaming feature generation requires a height map matching the configured chunk resolution.",
                    nameof(map));
            }

            settings.Validate();
            System.Array.Clear(map.PoiMask, 0, map.PoiMask.Length);
            System.Array.Clear(map.Roads, 0, map.Roads.Length);
            float cellSize = settings.GetCellSize(map.Width);
            List<TerrainWorldPOI> pois = PlaceStreamingPOIs(settings, coordinate, map, cellSize);
            int unconnectedRoutes = 0;
            bool[] connectedAnchors = new bool[4];
            if (settings.connectPOIsWithRoads && settings.roadCount > 0)
            {
                Vector2Int hub = pois.Count > 0
                    ? WorldPositionToMap(settings, coordinate, pois[0].TerrainPosition, cellSize)
                    : FindNearestWalkableToCenter(map);
                Vector2Int[] anchors =
                {
                    FindBoundaryAnchor(map, 0),
                    FindBoundaryAnchor(map, 1),
                    FindBoundaryAnchor(map, 2),
                    FindBoundaryAnchor(map, 3)
                };

                for (int i = 0; i < anchors.Length; i++)
                {
                    Vector2Int anchor = anchors[i];
                    if (anchor.x < 0 || hub.x < 0 ||
                        !TryFindPath(hub.x, hub.y, anchor.x, anchor.y, map.Navigation, map.Water,
                            out List<int> path))
                    {
                        unconnectedRoutes++;
                        continue;
                    }

                    AddRoadPath(map, settings, path);
                    connectedAnchors[i] = true;
                }
            }

            SetStreamingBoundaryRoads(map, settings, connectedAnchors);
            return new TerrainWorldFeatureGenerationResult(pois, unconnectedRoutes);
        }

        static List<TerrainWorldPOI> PlaceStreamingPOIs(
            TerrainSettings settings,
            Vector2Int coordinate,
            HeightMap map,
            float cellSize)
        {
            List<TerrainWorldPOI> placed = new List<TerrainWorldPOI>();
            List<PoiCandidate> candidates = new List<PoiCandidate>();
            int margin = Mathf.CeilToInt(map.Width * 0.3f);
            int sampleStride = Mathf.Max(1, Mathf.RoundToInt(50f / cellSize));
            foreach (TerrainPOIDefinition definition in settings.poiDefinitions)
            {
                if (definition.infiniteSpawnChancePerChunk <= 0f ||
                    StableChunkHash01(coordinate, settings.noiseSettings.seed, definition.id) >=
                        definition.infiniteSpawnChancePerChunk)
                {
                    continue;
                }

                PoiCandidate best = null;
                for (int y = margin; y < map.Height - margin; y += sampleStride)
                {
                    for (int x = margin; x < map.Width - margin; x += sampleStride)
                    {
                        if (map.Values[x, y] < definition.minimumElevation ||
                            map.Values[x, y] > definition.maximumElevation ||
                            map.Slopes[x, y] > definition.maximumSlopeDegrees ||
                            map.Moisture[x, y] < definition.minimumMoisture ||
                            map.Moisture[x, y] > definition.maximumMoisture ||
                            map.Water[x, y] > 0.5f ||
                            map.Navigation[x, y] <= 0f ||
                            map.PoiSuitability[x, y] < definition.minimumSuitability ||
                            !HasFlatSite(
                                x,
                                y,
                                definition.minimumFlatAreaRadiusMeters,
                                cellSize,
                                definition.maximumSlopeDegrees,
                                map.Slopes,
                                map.Water,
                                map.Navigation))
                        {
                            continue;
                        }

                        float biomeAffinity = 1f - Mathf.Abs(map.Biomes[x, y] - definition.preferredBiome);
                        float preference = definition.biomePreferenceWeight;
                        float score = (
                            map.PoiSuitability[x, y] * (1f - preference) +
                            biomeAffinity * preference) * 0.9f +
                            StableHash01(
                                coordinate.x * (map.Width - 1) + x,
                                coordinate.y * (map.Height - 1) + y,
                                settings.noiseSettings.seed,
                                definition.id) * 0.1f;
                        if (best == null || score > best.Score ||
                            (Mathf.Approximately(score, best.Score) && (y < best.MapY ||
                             (y == best.MapY && x < best.MapX))))
                        {
                            best = new PoiCandidate
                            {
                                Definition = definition,
                                ChunkCoordinate = coordinate,
                                MapX = x,
                                MapY = y,
                                WorldX = coordinate.x * (map.Width - 1) + x,
                                WorldY = coordinate.y * (map.Height - 1) + y,
                                Score = score
                            };
                        }
                    }
                }

                if (best != null)
                {
                    candidates.Add(best);
                }
            }

            candidates.Sort((left, right) =>
            {
                int scoreOrder = right.Score.CompareTo(left.Score);
                return scoreOrder != 0
                    ? scoreOrder
                    : string.CompareOrdinal(left.Definition.id, right.Definition.id);
            });
            foreach (PoiCandidate candidate in candidates)
            {
                Vector3 position = new Vector3(
                    coordinate.x * settings.chunkSize + candidate.MapX * cellSize,
                    map.WorldHeights != null
                        ? map.WorldHeights[candidate.MapX, candidate.MapY]
                        : settings.heightCurve.Evaluate(map.Values[candidate.MapX, candidate.MapY]) *
                          settings.heightMultiplier,
                    coordinate.y * settings.chunkSize + settings.chunkSize - candidate.MapY * cellSize);
                bool tooClose = false;
                foreach (TerrainWorldPOI existing in placed)
                {
                    if (Vector3.Distance(existing.TerrainPosition, position) <
                        Mathf.Max(existing.MinimumSpacingMeters, candidate.Definition.minimumSpacingMeters))
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                map.PoiMask[candidate.MapX, candidate.MapY] = 1f;
                placed.Add(new TerrainWorldPOI(
                    candidate.Definition.id,
                    candidate.Definition.displayName,
                    coordinate,
                    position,
                    candidate.Definition.minimumSpacingMeters));
            }

            return placed;
        }

        static Vector2Int WorldPositionToMap(
            TerrainSettings settings,
            Vector2Int coordinate,
            Vector3 terrainPosition,
            float cellSize)
        {
            float localX = terrainPosition.x - coordinate.x * settings.chunkSize;
            float localZ = terrainPosition.z - coordinate.y * settings.chunkSize;
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(localX / cellSize), 0, settings.chunkResolution - 1),
                Mathf.Clamp(
                    Mathf.RoundToInt((settings.chunkSize - localZ) / cellSize),
                    0,
                    settings.chunkResolution - 1));
        }

        static Vector2Int FindNearestWalkableToCenter(HeightMap map)
        {
            int centerX = (map.Width - 1) / 2;
            int centerY = (map.Height - 1) / 2;
            int maxDistance = Mathf.Max(map.Width, map.Height);
            for (int distance = 0; distance <= maxDistance; distance++)
            {
                for (int y = Mathf.Max(0, centerY - distance); y <= Mathf.Min(map.Height - 1, centerY + distance); y++)
                {
                    int xOffset = distance - Mathf.Abs(y - centerY);
                    int left = centerX - xOffset;
                    if (IsWalkable(map, left, y))
                    {
                        return new Vector2Int(left, y);
                    }

                    int right = centerX + xOffset;
                    if (right != left && IsWalkable(map, right, y))
                    {
                        return new Vector2Int(right, y);
                    }
                }
            }

            return new Vector2Int(-1, -1);
        }

        static Vector2Int FindBoundaryAnchor(HeightMap map, int edge)
        {
            bool verticalEdge = edge < 2;
            int fixedIndex = edge == 0 ? 0 :
                edge == 1 ? map.Width - 1 :
                edge == 2 ? 0 : map.Height - 1;
            int length = verticalEdge ? map.Height : map.Width;
            int center = (length - 1) / 2;
            for (int offset = 0; offset < length; offset++)
            {
                int first = center + offset;
                if (first < length && IsEdgeWalkable(map, verticalEdge, fixedIndex, first))
                {
                    return verticalEdge
                        ? new Vector2Int(fixedIndex, first)
                        : new Vector2Int(first, fixedIndex);
                }

                int second = center - offset;
                if (offset > 0 && second >= 0 && IsEdgeWalkable(map, verticalEdge, fixedIndex, second))
                {
                    return verticalEdge
                        ? new Vector2Int(fixedIndex, second)
                        : new Vector2Int(second, fixedIndex);
                }
            }

            return new Vector2Int(-1, -1);
        }

        static bool IsEdgeWalkable(HeightMap map, bool verticalEdge, int fixedIndex, int variableIndex)
        {
            int x = verticalEdge ? fixedIndex : variableIndex;
            int y = verticalEdge ? variableIndex : fixedIndex;
            return IsWalkable(map, x, y);
        }

        static bool IsWalkable(HeightMap map, int x, int y)
        {
            return x >= 0 && x < map.Width && y >= 0 && y < map.Height &&
                map.Water[x, y] <= 0.5f && map.Navigation[x, y] > 0f;
        }

        static void AddRoadPath(HeightMap map, TerrainSettings settings, List<int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                int x = path[i] % map.Width;
                int y = path[i] / map.Width;
                for (int dy = -settings.roadWidthCells; dy <= settings.roadWidthCells; dy++)
                {
                    for (int dx = -settings.roadWidthCells; dx <= settings.roadWidthCells; dx++)
                    {
                        int roadX = x + dx;
                        int roadY = y + dy;
                        if (IsWalkable(map, roadX, roadY))
                        {
                            map.Roads[roadX, roadY] = 1f;
                        }
                    }
                }
            }
        }

        static void SetStreamingBoundaryRoads(HeightMap map, TerrainSettings settings, bool[] connectedAnchors)
        {
            int lastX = map.Width - 1;
            int lastY = map.Height - 1;
            for (int y = 0; y < map.Height; y++)
            {
                map.Roads[0, y] = 0f;
                map.Roads[lastX, y] = 0f;
            }

            for (int x = 0; x < map.Width; x++)
            {
                map.Roads[x, 0] = 0f;
                map.Roads[x, lastY] = 0f;
            }

            for (int edge = 0; edge < connectedAnchors.Length; edge++)
            {
                if (!connectedAnchors[edge])
                {
                    continue;
                }

                Vector2Int anchor = FindBoundaryAnchor(map, edge);
                if (anchor.x < 0)
                {
                    continue;
                }

                for (int offset = -settings.roadWidthCells; offset <= settings.roadWidthCells; offset++)
                {
                    int x = edge < 2 ? anchor.x : anchor.x + offset;
                    int y = edge < 2 ? anchor.y + offset : anchor.y;
                    if (IsWalkable(map, x, y))
                    {
                        map.Roads[x, y] = 1f;
                    }
                }
            }
        }

        static List<TerrainWorldPOI> PlacePOIs(
            TerrainSettings settings,
            HeightMap[,] chunkMaps,
            int radius,
            int cellCountPerChunk,
            float cellSize,
            float[,] slopes,
            float[,] worldHeights,
            float[,] moisture,
            float[,] water,
            float[,] navigation,
            float[,] suitability,
            float[,] poiMask)
        {
            List<PoiCandidate> candidates = new List<PoiCandidate>();
            int resolution = settings.chunkResolution;
            int sampleStride = Mathf.Max(1, Mathf.RoundToInt(50f / cellSize));
            for (int chunkX = -radius; chunkX <= radius; chunkX++)
            {
                for (int chunkZ = -radius; chunkZ <= radius; chunkZ++)
                {
                    HeightMap map = chunkMaps[chunkX + radius, radius - chunkZ];
                    for (int y = 1; y < resolution - 1; y += sampleStride)
                    {
                        for (int x = 1; x < resolution - 1; x += sampleStride)
                        {
                            int worldX = (chunkX + radius) * cellCountPerChunk + x;
                            int worldY = (radius - chunkZ) * cellCountPerChunk + y;
                            foreach (TerrainPOIDefinition definition in settings.poiDefinitions)
                            {
                                if (definition.targetCount <= 0 ||
                                    map.Values[x, y] < definition.minimumElevation ||
                                    map.Values[x, y] > definition.maximumElevation ||
                                    map.Slopes[x, y] > definition.maximumSlopeDegrees ||
                                    map.Moisture[x, y] < definition.minimumMoisture ||
                                    map.Moisture[x, y] > definition.maximumMoisture ||
                                    map.Water[x, y] > 0.5f ||
                                    map.Navigation[x, y] <= 0f ||
                                    suitability[worldX, worldY] < definition.minimumSuitability ||
                                    !HasFlatSite(
                                        worldX,
                                        worldY,
                                        definition.minimumFlatAreaRadiusMeters,
                                        cellSize,
                                        definition.maximumSlopeDegrees,
                                        slopes,
                                        water,
                                        navigation))
                                {
                                    continue;
                                }

                                float biomeSample = map.Biomes[x, y];
                                float biomeAffinity = 1f - Mathf.Abs(biomeSample - definition.preferredBiome);
                                float preferenceWeight = definition.biomePreferenceWeight;
                                float score = (
                                    suitability[worldX, worldY] * (1f - preferenceWeight) +
                                    biomeAffinity * preferenceWeight) * 0.9f +
                                    StableHash01(worldX, worldY, settings.noiseSettings.seed, definition.id) * 0.1f;
                                candidates.Add(new PoiCandidate
                                {
                                    Definition = definition,
                                    ChunkCoordinate = new Vector2Int(chunkX, chunkZ),
                                    MapX = x,
                                    MapY = y,
                                    WorldX = worldX,
                                    WorldY = worldY,
                                    Score = score
                                });
                            }
                        }
                    }
                }
            }

            candidates.Sort((left, right) =>
            {
                int scoreOrder = right.Score.CompareTo(left.Score);
                if (scoreOrder != 0)
                {
                    return scoreOrder;
                }

                int yOrder = left.WorldY.CompareTo(right.WorldY);
                if (yOrder != 0)
                {
                    return yOrder;
                }

                int xOrder = left.WorldX.CompareTo(right.WorldX);
                return xOrder != 0
                    ? xOrder
                    : string.CompareOrdinal(left.Definition.id, right.Definition.id);
            });
            List<TerrainWorldPOI> placed = new List<TerrainWorldPOI>();
            Dictionary<string, int> placedPerDefinition = new Dictionary<string, int>();
            float worldOriginX = -radius * settings.chunkSize;
            float worldTopZ = (radius + 1) * settings.chunkSize;
            foreach (PoiCandidate candidate in candidates)
            {
                TerrainPOIDefinition definition = candidate.Definition;
                string id = definition.id;
                placedPerDefinition.TryGetValue(id, out int currentCount);
                if (currentCount >= definition.targetCount)
                {
                    continue;
                }

                Vector3 position = new Vector3(
                    worldOriginX + candidate.WorldX * cellSize,
                    worldHeights[candidate.WorldX, candidate.WorldY],
                    worldTopZ - candidate.WorldY * cellSize);
                bool tooClose = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    float minimumDistance = Mathf.Max(definition.minimumSpacingMeters,
                        placed[i].MinimumSpacingMeters);
                    if ((position - placed[i].TerrainPosition).sqrMagnitude < minimumDistance * minimumDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                placed.Add(new TerrainWorldPOI(
                    id,
                    definition.displayName,
                    candidate.ChunkCoordinate,
                    position,
                    definition.minimumSpacingMeters));
                placedPerDefinition[id] = currentCount + 1;
                poiMask[candidate.WorldX, candidate.WorldY] = 1f;
            }

            return placed;
        }

        static bool TryFindPath(
            int startX,
            int startY,
            int targetX,
            int targetY,
            float[,] navigation,
            float[,] water,
            out List<int> path)
        {
            int width = navigation.GetLength(0);
            int height = navigation.GetLength(1);
            int start = startY * width + startX;
            int target = targetY * width + targetX;
            path = null;
            if (startX < 0 || startX >= width || startY < 0 || startY >= height ||
                targetX < 0 || targetX >= width || targetY < 0 || targetY >= height ||
                navigation[startX, startY] <= 0f || navigation[targetX, targetY] <= 0f ||
                water[startX, startY] > 0.5f || water[targetX, targetY] > 0.5f)
            {
                return false;
            }

            int count = width * height;
            float[] costs = new float[count];
            int[] parents = new int[count];
            bool[] closed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                costs[i] = float.PositiveInfinity;
                parents[i] = -1;
            }

            MinHeap open = new MinHeap();
            costs[start] = 0f;
            open.Push(start, Manhattan(startX, startY, targetX, targetY));
            int[] offsetX = { 1, -1, 0, 0 };
            int[] offsetY = { 0, 0, 1, -1 };
            while (open.Count > 0)
            {
                int current = open.Pop().index;
                if (closed[current])
                {
                    continue;
                }

                if (current == target)
                {
                    List<int> reversed = new List<int>();
                    for (int point = target; point >= 0; point = parents[point])
                    {
                        reversed.Add(point);
                        if (point == start)
                        {
                            break;
                        }
                    }

                    reversed.Reverse();
                    path = reversed;
                    return true;
                }

                closed[current] = true;
                int currentX = current % width;
                int currentY = current / width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nextX = currentX + offsetX[direction];
                    int nextY = currentY + offsetY[direction];
                    if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height ||
                        navigation[nextX, nextY] <= 0f || water[nextX, nextY] > 0.5f)
                    {
                        continue;
                    }

                    int next = nextY * width + nextX;
                    if (closed[next])
                    {
                        continue;
                    }

                    float movementCost = 1f + (3f - navigation[nextX, nextY]) * 2f;
                    float nextCost = costs[current] + movementCost;
                    if (nextCost >= costs[next])
                    {
                        continue;
                    }

                    costs[next] = nextCost;
                    parents[next] = current;
                    open.Push(next, nextCost + Manhattan(nextX, nextY, targetX, targetY));
                }
            }

            return false;
        }

        static bool HasFlatSite(
            int centerX,
            int centerY,
            float radiusMeters,
            float cellSize,
            float maximumSlope,
            float[,] slopes,
            float[,] water,
            float[,] navigation)
        {
            int radius = Mathf.CeilToInt(radiusMeters / cellSize);
            int radiusSquared = radius * radius;
            int width = slopes.GetLength(0);
            int height = slopes.GetLength(1);
            for (int yOffset = -radius; yOffset <= radius; yOffset++)
            {
                int y = centerY + yOffset;
                for (int xOffset = -radius; xOffset <= radius; xOffset++)
                {
                    if (xOffset * xOffset + yOffset * yOffset > radiusSquared)
                    {
                        continue;
                    }

                    int x = centerX + xOffset;
                    if (x < 0 || x >= width || y < 0 || y >= height ||
                        slopes[x, y] > maximumSlope || water[x, y] > 0.5f ||
                        navigation[x, y] <= 0f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static int Manhattan(int x, int y, int targetX, int targetY)
        {
            return Mathf.Abs(targetX - x) + Mathf.Abs(targetY - y);
        }

        static void CopyWorldFeaturesToChunks(
            TerrainSettings settings,
            HeightMap[,] chunkMaps,
            int radius,
            int cellCountPerChunk,
            int worldSize,
            float[,] poiMask,
            float[,] roads)
        {
            for (int chunkX = -radius; chunkX <= radius; chunkX++)
            {
                for (int chunkZ = -radius; chunkZ <= radius; chunkZ++)
                {
                    HeightMap map = chunkMaps[chunkX + radius, radius - chunkZ];
                    int offsetX = (chunkX + radius) * cellCountPerChunk;
                    int offsetY = (radius - chunkZ) * cellCountPerChunk;
                    for (int y = 0; y < settings.chunkResolution; y++)
                    {
                        for (int x = 0; x < settings.chunkResolution; x++)
                        {
                            int worldX = offsetX + x;
                            int worldY = offsetY + y;
                            if (worldX >= worldSize || worldY >= worldSize)
                            {
                                continue;
                            }

                            map.PoiMask[x, y] = poiMask[worldX, worldY];
                            map.Roads[x, y] = roads[worldX, worldY];
                        }
                    }
                }
            }
        }

        static float StableHash01(int x, int y, int seed, string salt)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)x * 0x8da6b343u;
                hash ^= (uint)y * 0xd8163841u;
                for (int i = 0; i < salt.Length; i++)
                {
                    hash = (hash ^ salt[i]) * 16777619u;
                }

                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }

        static float StableChunkHash01(Vector2Int coordinate, int seed, string salt)
        {
            return StableHash01(coordinate.x, coordinate.y, seed, salt);
        }
    }
}
