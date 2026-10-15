using UnityEngine;
using ProjectLeviathan.World;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class ProceduralBattlefieldSetup
    {
        const string DefaultTerrainSettingsPath = "Assets/Settings/Terrain/LeviathanTerrain.asset";

        public static TerrainWorldController CreateBattlefield(string name, Transform viewer)
        {
            DestroyExisting(name);

            TerrainSettings settings = LoadTerrainSettings();
            GameObject terrainObject = new GameObject(name);
            WorldWeatherProvider weather = terrainObject.AddComponent<WorldWeatherProvider>();
            TerrainWorldController controller = terrainObject.AddComponent<TerrainWorldController>();
            controller.terrainSettings = settings;
            controller.terrainMaterial = settings.terrainMaterial != null
                ? settings.terrainMaterial
                : CreateFallbackMaterial();
            controller.weatherProvider = weather;
            controller.viewer = viewer;
            controller.useFiniteMap = true;
            controller.previewInEditor = true;
            controller.useBackgroundGeneration = false;
            controller.maxChunkGenerationsPerFrame = 2;

            if (settings.terrainMaterial == null)
            {
                settings.terrainMaterial = controller.terrainMaterial;
            }

            if (!Application.isPlaying)
            {
                controller.RegenerateTerrain();
            }

            return controller;
        }

        public static TerrainSettings LoadTerrainSettings()
        {
#if UNITY_EDITOR
            TerrainSettings asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainSettings>(DefaultTerrainSettingsPath);
            if (asset != null)
            {
                asset.Validate();
                return asset;
            }
#endif
            TerrainSettings settings = ScriptableObject.CreateInstance<TerrainSettings>();
            settings.Validate();
            settings.terrainMaterial = CreateFallbackMaterial();
            return settings;
        }

        public static float SampleHeight(TerrainSettings settings, Vector3 worldPosition)
        {
            if (settings == null)
            {
                settings = LoadTerrainSettings();
            }

            settings.Validate();
            Vector2Int chunk = new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / settings.chunkSize),
                Mathf.FloorToInt(worldPosition.z / settings.chunkSize));
            Vector2 origin = settings.GetChunkWorldOrigin(chunk);
            HeightMap map = HeightMapGenerator.GenerateHeightMap(
                settings,
                settings.chunkResolution,
                settings.chunkResolution,
                origin);

            float cell = settings.CellSize;
            float localX = Mathf.Clamp(worldPosition.x - origin.x, 0f, settings.chunkSize);
            float localZ = Mathf.Clamp(worldPosition.z - origin.y, 0f, settings.chunkSize);
            float mapX = localX / cell;
            float mapY = (settings.chunkSize - localZ) / cell;
            return Bilinear(map.WorldHeights, mapX, mapY);
        }

        public static void PlaceOnTerrain(Transform target, TerrainSettings settings, Vector3 worldXZ, float yOffset)
        {
            if (target == null)
            {
                return;
            }

            float height = SampleHeight(settings, worldXZ);
            target.position = new Vector3(worldXZ.x, height + yOffset, worldXZ.z);
        }

        static float Bilinear(float[,] map, float x, float y)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, height - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, height - 1);
            float tx = Mathf.Clamp01(x - x0);
            float ty = Mathf.Clamp01(y - y0);
            float a = Mathf.Lerp(map[x0, y0], map[x1, y0], tx);
            float b = Mathf.Lerp(map[x0, y1], map[x1, y1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        static Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader != null ? new Material(shader) : null;
        }

        static void DestroyExisting(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(existing);
            }
            else
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
