using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class TerrainPropGenerator
    {
        public static Transform Generate(TerrainChunk chunk, HeightMap heightMap)
        {
            if (chunk == null || heightMap == null || chunk.Settings == null ||
                !chunk.Settings.generateProceduralProps || chunk.GameObject == null)
            {
                return null;
            }

            TerrainSettings settings = chunk.Settings;
            Transform root = new GameObject("Generated Surface Props").transform;
            root.SetParent(chunk.GameObject.transform, false);
            if (!Application.isPlaying)
            {
                root.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            int seed = unchecked(settings.noiseSettings.seed ^
                chunk.Coordinate.x * 73856093 ^
                chunk.Coordinate.y * 19349663);
            System.Random random = new System.Random(seed);
            GenerateStructures(root, settings, heightMap, random);
            GenerateDecorations(root, settings, heightMap, random);
            return root;
        }

        static void GenerateStructures(Transform root, TerrainSettings settings, HeightMap map, System.Random random)
        {
            int placed = 0;
            int attempts = settings.structuresPerChunk * 12;
            Vector3[] placedPositions = new Vector3[settings.structuresPerChunk];
            for (int i = 0; i < attempts && placed < settings.structuresPerChunk; i++)
            {
                if (!TryGetPlacement(settings, map, random, out Vector3 position, out float biome,
                        out int mapX, out int mapY, 4))
                {
                    continue;
                }

                float height = map.Values[mapX, mapY];
                if (height < settings.propMinimumHeight || height > settings.propMaximumHeight ||
                    map.Water[mapX, mapY] > 0.5f ||
                    map.Slopes[mapX, mapY] > settings.structureMaximumSlope)
                {
                    continue;
                }

                bool tooClose = false;
                for (int previous = 0; previous < placed; previous++)
                {
                    if ((position - placedPositions[previous]).sqrMagnitude < 900f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                if (random.NextDouble() < 0.58)
                {
                    CreateBunker(root, settings, position, (float)random.NextDouble() * 360f, biome);
                }
                else
                {
                    CreateTrench(root, settings, position, (float)random.NextDouble() * 180f, biome);
                }

                placedPositions[placed] = position;
                placed++;
            }
        }

        static void GenerateDecorations(Transform root, TerrainSettings settings, HeightMap map, System.Random random)
        {
            int placed = 0;
            int attempts = settings.decorationsPerChunk * 5;
            for (int i = 0; i < attempts && placed < settings.decorationsPerChunk; i++)
            {
                if (!TryGetPlacement(settings, map, random, out Vector3 position, out float biome,
                        out int x, out int y, 3))
                {
                    continue;
                }

                float elevation = map.Values[x, y];
                if (elevation < settings.propMinimumHeight || elevation > settings.propMaximumHeight ||
                    map.Water[x, y] > 0.5f ||
                    map.Slopes[x, y] > settings.decorationMaximumSlope)
                {
                    continue;
                }

                bool rockyBiome = biome > 0.7f || elevation > 0.64f;
                if (rockyBiome || random.NextDouble() < 0.58)
                {
                    CreateRock(root, settings, position, random);
                }
                else
                {
                    CreateDeadTree(root, settings, position, random, biome);
                }

                placed++;
            }
        }

        static bool TryGetPlacement(
            TerrainSettings settings,
            HeightMap map,
            System.Random random,
            out Vector3 position,
            out float biome,
            out int mapX,
            out int mapY,
            int border)
        {
            position = Vector3.zero;
            biome = 0.5f;
            mapX = 0;
            mapY = 0;
            int width = map.Width;
            int height = map.Height;
            if (width <= border * 2 + 1 || height <= border * 2 + 1)
            {
                return false;
            }

            mapX = random.Next(border, width - border);
            mapY = random.Next(border, height - border);
            float elevation = map.Values[mapX, mapY];
            if (elevation <= settings.lakeThreshold)
            {
                return false;
            }

            biome = map.Biomes[mapX, mapY];
            float worldHeight = map.WorldHeights != null
                ? map.WorldHeights[mapX, mapY]
                : settings.heightCurve.Evaluate(Mathf.Clamp01(elevation)) * settings.heightMultiplier;
            float cellSize = settings.GetCellSize(width);
            position = new Vector3(
                mapX * cellSize,
                worldHeight,
                settings.chunkSize - mapY * cellSize);
            if (map.BaseSite.IsExcluded(map.WorldOrigin.x + position.x, map.WorldOrigin.y + position.z,
                map.BaseSite.InfluenceRadius - map.BaseSite.exclusionRadius + 20f)) return false;
            return true;
        }

        static void CreateBunker(Transform root, TerrainSettings settings, Vector3 position, float rotation, float biome)
        {
            GameObject bunker = CreatePart(root, settings, "Concrete Bunker", PrimitiveType.Cube,
                position + Vector3.up * 1.5f, new Vector3(12f, 3f, 8f),
                Quaternion.Euler(0f, rotation, 0f), new Color(0.36f, 0.35f, 0.31f), true);
            bunker.isStatic = true;

            CreatePart(root, settings, "Bunker Roof", PrimitiveType.Cube,
                position + Vector3.up * 3.15f, new Vector3(13f, 0.55f, 9f),
                Quaternion.Euler(0f, rotation, 0f), new Color(0.27f, 0.28f, 0.25f), false);
            CreatePart(root, settings, "Bunker Entrance", PrimitiveType.Cube,
                position + Quaternion.Euler(0f, rotation, 0f) * new Vector3(0f, 1.05f, 3.85f),
                new Vector3(2.6f, 1.8f, 0.2f), Quaternion.Euler(0f, rotation, 0f),
                new Color(0.09f, 0.1f, 0.09f), false);

            Color sandbag = Color.Lerp(new Color(0.43f, 0.39f, 0.29f), new Color(0.35f, 0.39f, 0.31f), biome);
            for (int i = -2; i <= 2; i++)
            {
                Vector3 local = new Vector3(i * 1.45f, 0.5f, 4.8f);
                CreatePart(root, settings, "Bunker Sandbag", PrimitiveType.Sphere,
                    position + Quaternion.Euler(0f, rotation, 0f) * local,
                    new Vector3(1.5f, 0.8f, 1f), Quaternion.Euler(0f, rotation, 0f), sandbag, false);
            }
        }

        static void CreateTrench(Transform root, TerrainSettings settings, Vector3 position, float rotation, float biome)
        {
            Quaternion orientation = Quaternion.Euler(0f, rotation, 0f);
            Color earth = Color.Lerp(new Color(0.36f, 0.3f, 0.22f), new Color(0.28f, 0.34f, 0.27f), biome);
            CreatePart(root, settings, "Trench Parapet", PrimitiveType.Cube,
                position + orientation * new Vector3(0f, 0.55f, -2.7f),
                new Vector3(22f, 1.1f, 1.7f), orientation, earth, true);
            CreatePart(root, settings, "Trench Parapet", PrimitiveType.Cube,
                position + orientation * new Vector3(0f, 0.55f, 2.7f),
                new Vector3(22f, 1.1f, 1.7f), orientation, earth, true);
            CreatePart(root, settings, "Trench Walkway", PrimitiveType.Cube,
                position + orientation * new Vector3(0f, 0.08f, 0f),
                new Vector3(19f, 0.16f, 3.7f), orientation,
                new Color(0.17f, 0.17f, 0.14f), false);
        }

        static void CreateRock(Transform root, TerrainSettings settings, Vector3 position, System.Random random)
        {
            float scale = Mathf.Lerp(1.5f, 4.5f, (float)random.NextDouble());
            GameObject rock = CreatePart(root, settings, "Field Rock", PrimitiveType.Sphere,
                position + Vector3.up * scale * 0.32f,
                new Vector3(scale, scale * Mathf.Lerp(0.45f, 0.85f, (float)random.NextDouble()), scale * 0.8f),
                Quaternion.Euler(
                    (float)random.NextDouble() * 28f,
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 28f),
                new Color(0.39f, 0.39f, 0.35f), false);
            rock.isStatic = true;
        }

        static void CreateDeadTree(Transform root, TerrainSettings settings, Vector3 position, System.Random random, float biome)
        {
            float treeHeight = Mathf.Lerp(5f, 9f, (float)random.NextDouble());
            Color wood = Color.Lerp(new Color(0.25f, 0.19f, 0.13f), new Color(0.29f, 0.25f, 0.2f), biome);
            CreatePart(root, settings, "Dead Tree Trunk", PrimitiveType.Cylinder,
                position + Vector3.up * treeHeight * 0.5f,
                new Vector3(0.42f, treeHeight * 0.5f, 0.42f),
                Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), wood, false);

            for (int i = 0; i < 3; i++)
            {
                float branchHeight = treeHeight * Mathf.Lerp(0.48f, 0.88f, (float)random.NextDouble());
                float branchLength = Mathf.Lerp(1.8f, 3.8f, (float)random.NextDouble());
                float angle = (float)random.NextDouble() * 360f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.right;
                Vector3 start = position + Vector3.up * branchHeight;
                Vector3 end = start + direction * branchLength + Vector3.up * branchLength * 0.35f;
                CreateCylinderBetween(root, settings, "Dead Tree Branch", start, end,
                    0.12f, wood);
            }
        }

        static void CreateCylinderBetween(
            Transform root,
            TerrainSettings settings,
            string name,
            Vector3 start,
            Vector3 end,
            float radius,
            Color color)
        {
            Vector3 delta = end - start;
            GameObject branch = CreatePart(root, settings, name, PrimitiveType.Cylinder,
                (start + end) * 0.5f, new Vector3(radius, delta.magnitude * 0.5f, radius),
                Quaternion.FromToRotation(Vector3.up, delta.normalized), color, false);
            branch.isStatic = true;
        }

        static GameObject CreatePart(
            Transform root,
            TerrainSettings settings,
            string name,
            PrimitiveType primitive,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Color color,
            bool hasCollider)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (!hasCollider && collider != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(collider);
                }
                else
                {
                    Object.DestroyImmediate(collider);
                }
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (settings.terrainMaterial != null)
            {
                renderer.sharedMaterial = settings.terrainMaterial;
            }

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);

            if (!Application.isPlaying)
            {
                part.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            part.isStatic = true;
            return part;
        }
    }
}
