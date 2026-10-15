using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public enum TerrainPOIVisualStyle
    {
        Outpost,
        Depot,
        Landmark
    }

    [System.Serializable]
    public class TerrainSurfacePalette
    {
        public Color dryColor = new Color(0.56f, 0.48f, 0.32f);
        public Color wetColor = new Color(0.25f, 0.35f, 0.27f);
        public Color coldColor = new Color(0.53f, 0.57f, 0.58f);
        public Color rockyColor = new Color(0.36f, 0.35f, 0.32f);
        [Range(0f, 1f)] public float dryMoistureThreshold = 0.3f;
        [Range(0f, 1f)] public float wetMoistureThreshold = 0.68f;
        [Range(0f, 1f)] public float coldTemperatureThreshold = 0.3f;
        [Range(0f, 90f)] public float rockySlopeThreshold = 28f;
        [Range(0f, 1f)] public float climateBlendStrength = 0.62f;
        [Range(0f, 1f)] public float rainDarkening = 0.22f;
        [Range(0f, 1f)] public float rainWetColorBlend = 0.35f;

        public void Validate()
        {
            dryMoistureThreshold = Mathf.Clamp01(dryMoistureThreshold);
            wetMoistureThreshold = Mathf.Clamp(wetMoistureThreshold, dryMoistureThreshold, 1f);
            coldTemperatureThreshold = Mathf.Clamp01(coldTemperatureThreshold);
            rockySlopeThreshold = Mathf.Clamp(rockySlopeThreshold, 0f, 90f);
            climateBlendStrength = Mathf.Clamp01(climateBlendStrength);
            rainDarkening = Mathf.Clamp01(rainDarkening);
            rainWetColorBlend = Mathf.Clamp01(rainWetColorBlend);
        }
    }

    [System.Serializable]
    public class TerrainPOIDefinition
    {
        public string id = "outpost";
        public string displayName = "Field Outpost";
        [Range(0, 16)] public int targetCount = 2;
        [Range(0f, 1f)] public float minimumElevation = 0.24f;
        [Range(0f, 1f)] public float maximumElevation = 0.68f;
        [Range(0f, 90f)] public float maximumSlopeDegrees = 6f;
        [Range(0f, 1f)] public float minimumMoisture;
        [Range(0f, 1f)] public float maximumMoisture = 1f;
        [Range(0f, 1f)] public float preferredBiome = 0.5f;
        [Range(0f, 1f)] public float biomePreferenceWeight = 0.25f;
        [Range(0f, 1f)] public float minimumSuitability = 0.25f;
        [Min(0f)] public float minimumFlatAreaRadiusMeters = 30f;
        [Min(0f)] public float minimumSpacingMeters = 500f;
        [Range(0f, 1f)] public float infiniteSpawnChancePerChunk = 0.12f;
        public GameObject visualPrefab;
        [Min(0.01f)] public float visualScale = 1f;
        [Min(0f)] public float visualSurfaceOffsetMeters = 0.15f;
        public Color markerColor = new Color(0.72f, 0.48f, 0.2f);
        public TerrainPOIVisualStyle fallbackVisualStyle = TerrainPOIVisualStyle.Outpost;
        public bool useFallbackVisual = true;
    }

    /// <summary>
    /// Zonas planas: regiões de baixa frequência em que o relevo é puxado para um nível de planície suave,
    /// criando áreas boas para posicionar artilharia, POIs e estradas mesmo em mapas montanhosos.
    /// </summary>
    [System.Serializable]
    public class TerrainFlatZoneSettings
    {
        public bool enabled = false;
        [Tooltip("Escala (m) do ruído que decide onde ficam as zonas planas. Menor = zonas menores e mais numerosas.")]
        [Min(1f)] public float scaleMeters = 2500f;
        [Range(1, 4)] public int octaves = 2;
        [Tooltip("Valor do ruído acima do qual a zona plana começa. Menor = mais área plana.")]
        [Range(0f, 1f)] public float threshold = 0.5f;
        [Tooltip("Largura da transição entre relevo e zona plana (em valor de ruído). Maior = encostas mais suaves.")]
        [Range(0.01f, 0.5f)] public float blendWidth = 0.08f;
        [Tooltip("Escala (m) do ruído que varia a altitude das planícies entre si.")]
        [Min(1f)] public float levelScaleMeters = 9000f;
        [Tooltip("Altura normalizada (0-1) mínima das planícies.")]
        [Range(0f, 1f)] public float minimumLevel = 0.24f;
        [Tooltip("Altura normalizada (0-1) máxima das planícies.")]
        [Range(0f, 1f)] public float maximumLevel = 0.30f;

        public void Validate()
        {
            scaleMeters = Mathf.Max(1f, scaleMeters);
            octaves = Mathf.Clamp(octaves, 1, 4);
            threshold = Mathf.Clamp01(threshold);
            blendWidth = Mathf.Clamp(blendWidth, 0.01f, 0.5f);
            levelScaleMeters = Mathf.Max(1f, levelScaleMeters);
            minimumLevel = Mathf.Clamp01(minimumLevel);
            maximumLevel = Mathf.Clamp(maximumLevel, minimumLevel, 1f);
        }
    }

    [CreateAssetMenu(fileName = "TerrainSettings", menuName = "Project Leviathan/Terrain Settings")]
    public class TerrainSettings : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Physical horizontal side length of each chunk, in meters.")]
        [Min(0.0001f)] public float chunkSize = 1200f;
        [Tooltip("Vertex count per side. Cell size is derived as chunkSize / (resolution - 1).")]
        [Min(3)] public int chunkResolution = 121;
        public int chunksVisibleInViewDistance = 1;
        public float heightMultiplier = 18f;
        public AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool generateIsland = false;
        public bool useInfiniteWorld = false;
        [Tooltip("Chunks por lado do mapa finito (sempre ímpar). 17 chunks de 1200 m = 20,4 km.")]
        [Range(1, MaxFiniteMapChunksPerSide)] public int finiteMapChunksPerSide = 3;
        [Tooltip("Mapas finitos com mais chunks por lado que este valor carregam só uma janela ao redor do viewer.")]
        [Range(1, 9)] public int finiteMapFullLoadMaxChunksPerSide = 5;
        [Tooltip("Raio (em chunks) da janela carregada ao redor do viewer nos mapas finitos grandes.")]
        [Range(1, 6)] public int finiteMapStreamingRadiusChunks = 2;

        [Tooltip("Raio (em chunks) da janela gerada FORA do Play (preview no editor) em mapas grandes. 1 = 3x3 chunks. Mantenha baixo: o preview é gerado de forma síncrona e trava o editor se for grande.")]
        [Range(0, 3)] public int finiteMapEditorPreviewRadiusChunks = 1;

        public const int MaxFiniteMapChunksPerSide = 17;

        public float FiniteMapWorldSize => chunkSize * finiteMapChunksPerSide;
        /// <summary>True quando o mapa finito é grande demais para carregar inteiro e usa janela ao redor do viewer.</summary>
        public bool UsesWindowedFiniteMap => finiteMapChunksPerSide > finiteMapFullLoadMaxChunksPerSide;
        public float CellSize => GetCellSize(chunkResolution);

        [Header("Noise")]
        public NoiseSettings noiseSettings = new NoiseSettings();
        public List<TerrainNoiseLayerSettings> terrainNoiseLayers = new List<TerrainNoiseLayerSettings>
        {
            new TerrainNoiseLayerSettings { name = "Continental Shape", scaleMeters = 1800f, strength = 0.26f, octaves = 2 },
            new TerrainNoiseLayerSettings { name = "Surface Detail", scaleMeters = 70f, strength = 0.08f, octaves = 2 }
        };
        [Tooltip("Aumenta o contraste do relevo em torno do valor médio. 1 = sem alteração; 2+ = mais montanhas e vales.")]
        [Range(0.5f, 4f)] public float reliefContrast = 1f;
        public float flatnessBias = 0.22f;
        public float mountainBias = 0.18f;
        public TerrainFlatZoneSettings flatZones = new TerrainFlatZoneSettings();
        public TerrainErosionSettings erosion = new TerrainErosionSettings();

        [Header("Biomes")]
        [Min(1f)] public float biomeNoiseScale = 1687.5f;
        [Min(1f)] public float moistureNoiseScale = 900f;
        [Min(1f)] public float temperatureNoiseScale = 2200f;
        [Range(0f, 1f)] public float temperatureAltitudeInfluence = 0.35f;
        [Range(0f, 1f)] public float biomeNoiseWeight = 0.08f;
        [Range(0f, 1f)] public float biomeHeightInfluence = 0.35f;
        [Range(0f, 0.2f)] public float biomeBlendWidth = 0.035f;

        [Header("Surface and Props")]
        [Range(0, 4)] public int heightSmoothingIterations = 1;
        [Range(0f, 1f)] public float heightSmoothingStrength = 0.65f;
        public bool generateProceduralProps = true;
        [Range(0, 80)] public int decorationsPerChunk = 32;
        [Range(0, 16)] public int structuresPerChunk = 5;
        [Range(0f, 1f)] public float propMinimumHeight = 0.2f;
        [Range(0f, 1f)] public float propMaximumHeight = 0.78f;
        [Range(0f, 90f)] public float structureMaximumSlope = 8f;
        [Range(0f, 90f)] public float decorationMaximumSlope = 25f;
        [Min(0f)] public float propViewDistance = 1600f;
        public List<TerrainPOIDefinition> poiDefinitions = new List<TerrainPOIDefinition>
        {
            new TerrainPOIDefinition
            {
                id = "field-outpost",
                displayName = "Field Outpost",
                targetCount = 2,
                minimumElevation = 0.24f,
                maximumElevation = 0.68f,
                maximumSlopeDegrees = 6f,
                preferredBiome = 0.35f,
                biomePreferenceWeight = 0.25f,
                minimumSuitability = 0.25f,
                minimumFlatAreaRadiusMeters = 30f,
                minimumSpacingMeters = 500f,
                infiniteSpawnChancePerChunk = 0.12f
            },
            new TerrainPOIDefinition
            {
                id = "supply-depot",
                displayName = "Supply Depot",
                targetCount = 1,
                minimumElevation = 0.3f,
                maximumElevation = 0.72f,
                maximumSlopeDegrees = 5f,
                preferredBiome = 0.55f,
                biomePreferenceWeight = 0.3f,
                minimumSuitability = 0.3f,
                minimumFlatAreaRadiusMeters = 40f,
                minimumSpacingMeters = 700f,
                infiniteSpawnChancePerChunk = 0.06f,
                fallbackVisualStyle = TerrainPOIVisualStyle.Depot
            }
        };

        [Header("Falloff")]
        public float falloffA = 3f;
        public float falloffB = 2.2f;

        [Header("Features")]
        public float lakeThreshold = 0.16f;
        public float riverThreshold = 0.22f;
        public float roadDensity = 0.33f;
        public int roadCount = 3;
        public int riverCount = 3;
        public bool connectPOIsWithRoads = true;
        [Range(0, 3)] public int roadWidthCells = 1;

        [Header("Visuals")]
        public Color oceanColor = new Color(0.11f, 0.43f, 0.69f);
        public bool useTerrainColors = true;
        public TerrainSurfacePalette surfacePalette = new TerrainSurfacePalette();
        public Material roadMaterial;
        [Min(0f)] public float roadWidthMeters = 8f;
        [Min(0f)] public float roadSurfaceLift = 0.12f;
        public List<TerrainType> terrainTypes = new List<TerrainType>();
        public List<TerrainBiomePreset> biomePresets = new List<TerrainBiomePreset>();
        public Material terrainMaterial;

        [Header("Water Surfaces")]
        public bool generateWaterSurfaces = true;
        public Material waterMaterial;
        [Min(0f)] public float waterSurfaceOffset = 0.08f;
        [Min(0.1f)] public float riverWaterDepth = 0.4f;

        [Header("LOD")]
        public List<LODInfo> lods = new List<LODInfo>();

        public float GetCellSize(int resolution)
        {
            return chunkSize / (Mathf.Max(2, resolution) - 1);
        }

        public Vector2 GetChunkWorldOrigin(Vector2Int coordinate)
        {
            return new Vector2(coordinate.x * chunkSize, coordinate.y * chunkSize);
        }

        [System.NonSerialized] public ProjectLeviathan.Base.BaseSiteData RuntimeBaseSite;

        public TerrainGenerationSettings CaptureGenerationSettings(bool includeBaseSite = true)
        {
            Validate();
            return new TerrainGenerationSettings(this, includeBaseSite);
        }

        public void Validate()
        {
            if (float.IsNaN(chunkSize) || float.IsInfinity(chunkSize) || chunkSize <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(chunkSize),
                    "Chunk size must be a finite positive number of meters.");
            }

            finiteMapChunksPerSide = Mathf.Clamp(finiteMapChunksPerSide, 1, MaxFiniteMapChunksPerSide);
            if (finiteMapChunksPerSide % 2 == 0)
            {
                finiteMapChunksPerSide = Mathf.Min(MaxFiniteMapChunksPerSide, finiteMapChunksPerSide + 1);
            }

            finiteMapFullLoadMaxChunksPerSide = Mathf.Clamp(finiteMapFullLoadMaxChunksPerSide, 1, 9);
            finiteMapStreamingRadiusChunks = Mathf.Clamp(finiteMapStreamingRadiusChunks, 1, 6);
            finiteMapEditorPreviewRadiusChunks = Mathf.Clamp(finiteMapEditorPreviewRadiusChunks, 0, 3);
            reliefContrast = Mathf.Clamp(reliefContrast, 0.5f, 4f);
            waterSurfaceOffset = Mathf.Max(0f, waterSurfaceOffset);
            riverWaterDepth = Mathf.Max(0.1f, riverWaterDepth);
            if (flatZones == null)
            {
                flatZones = new TerrainFlatZoneSettings();
            }

            flatZones.Validate();
            if(erosion==null) erosion=new TerrainErosionSettings();
            erosion.Validate();

            if (chunkResolution < 3)
            {
                chunkResolution = 3;
            }

            if (chunksVisibleInViewDistance < 1)
            {
                chunksVisibleInViewDistance = 1;
            }

            if (heightMultiplier < 0f)
            {
                heightMultiplier = 0f;
            }

            if (heightCurve == null)
            {
                heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }

            if (noiseSettings == null)
            {
                noiseSettings = new NoiseSettings();
            }

            noiseSettings.Validate();
            if (terrainNoiseLayers == null)
            {
                terrainNoiseLayers = new List<TerrainNoiseLayerSettings>();
            }

            for (int i = terrainNoiseLayers.Count - 1; i >= 0; i--)
            {
                TerrainNoiseLayerSettings layer = terrainNoiseLayers[i];
                if (layer == null)
                {
                    terrainNoiseLayers.RemoveAt(i);
                    continue;
                }

                layer.scaleMeters = Mathf.Max(1f, layer.scaleMeters);
                layer.strength = Mathf.Clamp01(layer.strength);
                layer.octaves = Mathf.Clamp(layer.octaves, 1, 8);
                layer.persistence = Mathf.Clamp(layer.persistence, 0.01f, 1f);
                layer.lacunarity = Mathf.Clamp(layer.lacunarity, 1f, 5f);
            }

            biomeNoiseScale = Mathf.Max(1f, biomeNoiseScale);
            moistureNoiseScale = Mathf.Max(1f, moistureNoiseScale);
            temperatureNoiseScale = Mathf.Max(1f, temperatureNoiseScale);
            temperatureAltitudeInfluence = Mathf.Clamp01(temperatureAltitudeInfluence);
            biomeNoiseWeight = Mathf.Clamp01(biomeNoiseWeight);
            biomeHeightInfluence = Mathf.Clamp01(biomeHeightInfluence);
            biomeBlendWidth = Mathf.Clamp(biomeBlendWidth, 0f, 0.2f);
            heightSmoothingIterations = Mathf.Clamp(heightSmoothingIterations, 0, 4);
            heightSmoothingStrength = Mathf.Clamp01(heightSmoothingStrength);
            decorationsPerChunk = Mathf.Clamp(decorationsPerChunk, 0, 80);
            structuresPerChunk = Mathf.Clamp(structuresPerChunk, 0, 16);
            propMinimumHeight = Mathf.Clamp01(propMinimumHeight);
            propMaximumHeight = Mathf.Max(propMinimumHeight, Mathf.Clamp01(propMaximumHeight));
            structureMaximumSlope = Mathf.Clamp(structureMaximumSlope, 0f, 90f);
            decorationMaximumSlope = Mathf.Clamp(decorationMaximumSlope, 0f, 90f);
            propViewDistance = Mathf.Max(0f, propViewDistance);
            roadWidthCells = Mathf.Clamp(roadWidthCells, 0, 3);
            roadWidthMeters = Mathf.Max(0.1f, roadWidthMeters);
            roadSurfaceLift = Mathf.Max(0.01f, roadSurfaceLift);
            roadWidthMeters = Mathf.Max(roadWidthMeters, CellSize);
            if (surfacePalette == null)
            {
                surfacePalette = new TerrainSurfacePalette();
            }

            surfacePalette.Validate();
            if (poiDefinitions == null)
            {
                poiDefinitions = new List<TerrainPOIDefinition>();
            }

            HashSet<string> poiIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = poiDefinitions.Count - 1; i >= 0; i--)
            {
                TerrainPOIDefinition definition = poiDefinitions[i];
                if (definition == null)
                {
                    poiDefinitions.RemoveAt(i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.id) || !poiIds.Add(definition.id))
                {
                    throw new System.InvalidOperationException(
                        "Each terrain POI definition must have a non-empty, unique id.");
                }

                if (string.IsNullOrWhiteSpace(definition.displayName))
                {
                    definition.displayName = definition.id;
                }

                definition.targetCount = Mathf.Clamp(definition.targetCount, 0, 16);
                definition.minimumElevation = Mathf.Clamp01(definition.minimumElevation);
                definition.maximumElevation = Mathf.Clamp(definition.maximumElevation, definition.minimumElevation, 1f);
                definition.maximumSlopeDegrees = Mathf.Clamp(definition.maximumSlopeDegrees, 0f, 90f);
                definition.minimumMoisture = Mathf.Clamp01(definition.minimumMoisture);
                definition.maximumMoisture = Mathf.Clamp(definition.maximumMoisture, definition.minimumMoisture, 1f);
                definition.preferredBiome = Mathf.Clamp01(definition.preferredBiome);
                definition.biomePreferenceWeight = Mathf.Clamp01(definition.biomePreferenceWeight);
                definition.minimumSuitability = Mathf.Clamp01(definition.minimumSuitability);
                definition.minimumFlatAreaRadiusMeters = Mathf.Max(0f, definition.minimumFlatAreaRadiusMeters);
                definition.minimumSpacingMeters = Mathf.Max(0f, definition.minimumSpacingMeters);
                definition.infiniteSpawnChancePerChunk = Mathf.Clamp01(definition.infiniteSpawnChancePerChunk);
                definition.visualScale = Mathf.Max(0.01f, definition.visualScale);
                definition.visualSurfaceOffsetMeters = Mathf.Max(0f, definition.visualSurfaceOffsetMeters);
            }

            if (falloffA <= 0f)
            {
                falloffA = 0.0001f;
            }

            if (falloffB <= 0f)
            {
                falloffB = 0.0001f;
            }

            if (lods == null || lods.Count == 0)
            {
                lods = new List<LODInfo>
                {
                    new LODInfo { lod = 0, visibleDistance = 200f, meshResolution = 1, useForRendering = true },
                    new LODInfo { lod = 1, visibleDistance = 500f, meshResolution = 2, useForRendering = true },
                    new LODInfo { lod = 2, visibleDistance = 900f, meshResolution = 4, useForRendering = true },
                    new LODInfo { lod = 3, visibleDistance = 1500f, meshResolution = 8, useForRendering = true },
                    new LODInfo { lod = 4, visibleDistance = 2200f, meshResolution = 16, useForRendering = true }
                };
            }

            for (int i = 0; i < lods.Count; i++)
            {
                if (lods[i] == null)
                {
                    lods[i] = new LODInfo();
                }

                lods[i].lod = Mathf.Max(0, lods[i].lod);
                lods[i].meshResolution = Mathf.Max(1, lods[i].meshResolution);
                lods[i].visibleDistance = Mathf.Max(0f, lods[i].visibleDistance);
            }

            if (terrainTypes == null)
            {
                terrainTypes = new List<TerrainType>();
            }

            if (terrainTypes.Count == 0)
            {
                terrainTypes.Add(new TerrainType { name = "Ocean", minHeight = 0f, maxHeight = 0.15f, color = new Color(0.12f, 0.35f, 0.55f) });
                terrainTypes.Add(new TerrainType { name = "Mire", minHeight = 0.15f, maxHeight = 0.24f, color = new Color(0.47f, 0.44f, 0.28f) });
                terrainTypes.Add(new TerrainType { name = "Plain", minHeight = 0.24f, maxHeight = 0.45f, color = new Color(0.34f, 0.52f, 0.30f) });
                terrainTypes.Add(new TerrainType { name = "Fields", minHeight = 0.45f, maxHeight = 0.62f, color = new Color(0.50f, 0.44f, 0.28f) });
                terrainTypes.Add(new TerrainType { name = "Rock", minHeight = 0.62f, maxHeight = 0.8f, color = new Color(0.48f, 0.47f, 0.43f) });
                terrainTypes.Add(new TerrainType { name = "Mountain", minHeight = 0.8f, maxHeight = 1.1f, color = new Color(0.71f, 0.72f, 0.75f) });
            }

            if (biomePresets == null)
            {
                biomePresets = new List<TerrainBiomePreset>();
            }

            if (biomePresets.Count == 0)
            {
                biomePresets.Add(new TerrainBiomePreset { name = "Lago", minHeight = 0f, maxHeight = 0.1f, color = new Color(0.09f, 0.28f, 0.39f) });
                biomePresets.Add(new TerrainBiomePreset { name = "Planície Dieselpunk", minHeight = 0.1f, maxHeight = 0.3f, color = new Color(0.50f, 0.46f, 0.28f) });
                biomePresets.Add(new TerrainBiomePreset { name = "Campos Industriais", minHeight = 0.3f, maxHeight = 0.5f, color = new Color(0.34f, 0.48f, 0.34f) });
                biomePresets.Add(new TerrainBiomePreset { name = "Terreno de Infraestrutura", minHeight = 0.5f, maxHeight = 0.67f, color = new Color(0.43f, 0.38f, 0.30f) });
                biomePresets.Add(new TerrainBiomePreset { name = "Rocha e Trincheiras", minHeight = 0.67f, maxHeight = 0.82f, color = new Color(0.42f, 0.40f, 0.36f) });
                biomePresets.Add(new TerrainBiomePreset { name = "Montanhas de Guerra", minHeight = 0.82f, maxHeight = 1f, color = new Color(0.72f, 0.72f, 0.75f) });
            }

            for (int i = 0; i < biomePresets.Count; i++)
            {
                if (biomePresets[i] == null)
                {
                    biomePresets[i] = new TerrainBiomePreset();
                }

                biomePresets[i].minHeight = Mathf.Clamp01(biomePresets[i].minHeight);
                biomePresets[i].maxHeight = Mathf.Clamp(biomePresets[i].maxHeight, biomePresets[i].minHeight, 1f);
                if (i > 0 && biomePresets[i].minHeight < biomePresets[i - 1].maxHeight - 0.0001f)
                {
                    throw new System.InvalidOperationException(
                        $"Biome preset '{biomePresets[i].name}' overlaps or is out of order after " +
                        $"'{biomePresets[i - 1].name}'. Order presets by elevation and avoid overlapping ranges.");
                }
            }
        }

        public Color GetBiomeColor(float normalizedHeight)
        {
            normalizedHeight = Mathf.Clamp01(normalizedHeight);

            for (int i = 0; i < biomePresets.Count; i++)
            {
                TerrainBiomePreset current = biomePresets[i];
                if (current == null)
                {
                    continue;
                }

                if (biomeBlendWidth > 0f && i + 1 < biomePresets.Count)
                {
                    TerrainBiomePreset next = biomePresets[i + 1];
                    float boundary = current.maxHeight;
                    float blendStart = boundary - biomeBlendWidth * 0.5f;
                    float blendEnd = boundary + biomeBlendWidth * 0.5f;
                    if (next != null && normalizedHeight >= blendStart && normalizedHeight <= blendEnd)
                    {
                        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(blendStart, blendEnd, normalizedHeight));
                        return Color.Lerp(current.color, next.color, blend);
                    }
                }

                if (current.Contains(normalizedHeight))
                {
                    return current.color;
                }
            }

            return oceanColor;
        }
    }
}
