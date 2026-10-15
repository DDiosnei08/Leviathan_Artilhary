using UnityEditor;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public class TerrainEditorWindow : EditorWindow
    {
        TerrainSettings settings;
        TerrainWorldController worldController;
        Texture2D heightPreview;
        Texture2D falloffPreview;
        Texture2D biomePreview;
        Texture2D biomeColorPreview;
        Texture2D slopePreview;
        Texture2D moisturePreview;
        Texture2D temperaturePreview;
        Texture2D waterPreview;
        Texture2D navigationPreview;
        Texture2D poiPreview;
        Texture2D roadPreview;
        Vector2 previewScroll;

        [MenuItem("Project Leviathan/Terrain Debug")]
        public static void OpenWindow()
        {
            TerrainEditorWindow window = GetWindow<TerrainEditorWindow>("Terrain Debug");
            window.minSize = new Vector2(1040f, 360f);
        }

        void OnEnable()
        {
            RefreshPreview();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            settings = (TerrainSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(TerrainSettings), false);
            worldController = (TerrainWorldController)EditorGUILayout.ObjectField(
                "Generated World (optional)",
                worldController,
                typeof(TerrainWorldController),
                true);
            if (GUILayout.Button("Refresh"))
            {
                RefreshPreview();
            }
            EditorGUILayout.EndHorizontal();

            if (settings == null)
            {
                EditorGUILayout.HelpBox("Selecione um TerrainSettings para visualizar o mapa de altura e falloff.", MessageType.Info);
                return;
            }

            settings.Validate();
            EditorGUILayout.LabelField("Seed", settings.noiseSettings.seed.ToString());
            EditorGUILayout.LabelField(
                "Finite Map",
                $"{settings.finiteMapChunksPerSide} x {settings.finiteMapChunksPerSide} chunks  |  {settings.FiniteMapWorldSize:F0}m");
            EditorGUILayout.LabelField(
                "Biome noise",
                $"Biome {settings.biomeNoiseScale:0}m  |  moisture {settings.moistureNoiseScale:0}m  |  temperature {settings.temperatureNoiseScale:0}m  |  smoothing {settings.heightSmoothingIterations}");
            if (worldController == null || !worldController.TryGetTerrainChunk(Vector2Int.zero, out TerrainChunk centerChunk) ||
                centerChunk.GeneratedData == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a generated finite TerrainWorldController to inspect its POI and road maps.",
                    MessageType.Info);
            }

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.Height(250f));
            EditorGUILayout.BeginHorizontal();
            DrawPreview("Height", heightPreview);
            DrawPreview("Falloff", falloffPreview);
            DrawPreview("Biome / climate blend", biomePreview);
            DrawPreview("Biome colors", biomeColorPreview);
            DrawPreview("Slope (0-45 deg)", slopePreview);
            DrawPreview("Moisture", moisturePreview);
            DrawPreview("Temperature", temperaturePreview);
            DrawPreview("Water", waterPreview);
            DrawPreview("Navigation (0-3)", navigationPreview);
            DrawPreview("POI markers", poiPreview);
            DrawPreview("Road network", roadPreview);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        static void DrawPreview(string title, Texture2D texture)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(180f));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (texture != null)
            {
                Rect rect = GUILayoutUtility.GetRect(180f, 220f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                GUILayoutUtility.GetRect(180f, 220f);
            }

            EditorGUILayout.EndVertical();
        }

        void RefreshPreview()
        {
            if (settings == null)
            {
                return;
            }

            settings.Validate();
            int size = Mathf.Clamp(settings.chunkResolution, 33, 512);
            HeightMap heightMap = HeightMapGenerator.GenerateHeightMap(settings, size, size, Vector2.zero);
            DestroyPreview(ref heightPreview);
            DestroyPreview(ref falloffPreview);
            DestroyPreview(ref biomePreview);
            DestroyPreview(ref biomeColorPreview);
            DestroyPreview(ref slopePreview);
            DestroyPreview(ref moisturePreview);
            DestroyPreview(ref temperaturePreview);
            DestroyPreview(ref waterPreview);
            DestroyPreview(ref navigationPreview);
            DestroyPreview(ref poiPreview);
            DestroyPreview(ref roadPreview);

            heightPreview = TextureGenerator.TextureFromHeightMap(heightMap.Values);
            falloffPreview = TextureGenerator.TextureFromHeightMap(heightMap.Falloff);
            biomePreview = TextureGenerator.TextureFromHeightMap(heightMap.Biomes);
            biomeColorPreview = TextureGenerator.TextureFromColorMap(
                TextureGenerator.BuildColorMap(heightMap, settings, Vector2.zero),
                size,
                size);
            slopePreview = CreateScaledPreview(heightMap.Slopes, 45f);
            moisturePreview = TextureGenerator.TextureFromHeightMap(heightMap.Moisture);
            temperaturePreview = TextureGenerator.TextureFromHeightMap(heightMap.Temperature);
            waterPreview = TextureGenerator.TextureFromHeightMap(heightMap.Water);
            navigationPreview = CreateScaledPreview(heightMap.Navigation, 3f);

            HeightMap worldData = null;
            if (worldController != null &&
                worldController.TryGetTerrainChunk(Vector2Int.zero, out TerrainChunk centerChunk))
            {
                worldData = centerChunk.GeneratedData;
            }

            poiPreview = TextureGenerator.TextureFromHeightMap(
                worldData != null ? worldData.PoiMask : new float[size, size]);
            roadPreview = TextureGenerator.TextureFromHeightMap(
                worldData != null ? worldData.Roads : new float[size, size]);
        }

        static Texture2D CreateScaledPreview(float[,] source, float scale)
        {
            float[,] scaled = new float[source.GetLength(0), source.GetLength(1)];
            for (int y = 0; y < source.GetLength(1); y++)
            {
                for (int x = 0; x < source.GetLength(0); x++)
                {
                    scaled[x, y] = Mathf.Clamp01(source[x, y] / scale);
                }
            }

            return TextureGenerator.TextureFromHeightMap(scaled);
        }

        void DestroyPreview(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            DestroyImmediate(texture);
            texture = null;
        }

        void OnDisable()
        {
            DestroyPreview(ref heightPreview);
            DestroyPreview(ref falloffPreview);
            DestroyPreview(ref biomePreview);
            DestroyPreview(ref biomeColorPreview);
            DestroyPreview(ref slopePreview);
            DestroyPreview(ref moisturePreview);
            DestroyPreview(ref temperaturePreview);
            DestroyPreview(ref waterPreview);
            DestroyPreview(ref navigationPreview);
            DestroyPreview(ref poiPreview);
            DestroyPreview(ref roadPreview);
        }
    }
}
