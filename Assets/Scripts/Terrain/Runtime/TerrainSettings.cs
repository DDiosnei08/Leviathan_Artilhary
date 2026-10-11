using System;
using UnityEngine;

namespace Leviathan.Terrain
{
    [Serializable]
    public sealed class NoiseLayerSettings
    {
        public string name = "Camada";
        [Min(10f)] public float scaleMeters = 1000f;
        [Range(0f, 1f)] public float strength = 0.1f;
        [Range(1, 8)] public int octaves = 3;
        [Range(0.05f, 0.95f)] public float persistence = 0.4f;
        [Range(1.1f, 4f)] public float lacunarity = 2f;
    }

    [Serializable]
    public sealed class FlatZoneSettings
    {
        public bool enabled = true;
        [Tooltip("Tamanho típico de uma zona plana, em metros.")]
        [Min(100f)] public float scaleMeters = 4000f;
        [Range(1, 6)] public int octaves = 2;
        [Tooltip("Valor do ruído de máscara a partir do qual começa a zona plana. Menor = mais área plana.")]
        [Range(0f, 1f)] public float threshold = 0.45f;
        [Tooltip("Largura da transição entre relevo e zona plana. Maior = encostas mais suaves.")]
        [Range(0.01f, 1f)] public float blendWidth = 0.3f;
        [Tooltip("Contraste do ruído de máscara. Maior = zonas mais definidas.")]
        [Range(1f, 6f)] public float maskContrast = 2.5f;
        [Tooltip("Nível mínimo das planícies, como fração da altura máxima.")]
        [Range(0f, 1f)] public float minLevel = 0.25f;
        [Tooltip("Nível máximo das planícies, como fração da altura máxima.")]
        [Range(0f, 1f)] public float maxLevel = 0.55f;
    }

    /// <summary>
    /// Configuração do terreno no editor. Crie pelo menu Assets > Create > Leviathan > Terrain Settings.
    /// Todo o cálculo está no assembly Core; esta classe só guarda os valores e os converte para
    /// HeightParams (imutável) por ToParams().
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainSettings", menuName = "Leviathan/Terrain Settings")]
    public sealed class TerrainSettings : ScriptableObject
    {
        [Tooltip("Mesma semente e mesmos parâmetros dão sempre o mesmo terreno.")]
        public int seed = 703110054;

        [Tooltip("Altura máxima do terreno, em metros.")]
        [Min(10f)] public float maxHeightMeters = 1000f;

        [Tooltip("Contraste do relevo (inclinação no centro da curva). 1 = sem contraste.")]
        [Range(1f, 6f)] public float reliefContrast = 3f;

        [Tooltip("Área de missão (limite operacional) dentro do domínio de 20.400 m. Ainda não usada na Fase 1.")]
        [Range(1000f, TerrainConstants.DomainSizeMeters)] public float missionAreaMeters = TerrainConstants.DefaultMissionAreaMeters;

        public NoiseLayerSettings[] layers =
        {
            new NoiseLayerSettings { name = "Continentes", scaleMeters = 9000f, strength = 0.30f, octaves = 3, persistence = 0.4f, lacunarity = 2f },
            new NoiseLayerSettings { name = "Montanhas", scaleMeters = 3000f, strength = 0.15f, octaves = 3, persistence = 0.4f, lacunarity = 2f },
            new NoiseLayerSettings { name = "Colinas", scaleMeters = 600f, strength = 0.04f, octaves = 2, persistence = 0.4f, lacunarity = 2f }
        };

        public FlatZoneSettings flatZones = new FlatZoneSettings();

        /// <summary>Converte para parâmetros imutáveis, validando uma única vez.</summary>
        public HeightParams ToParams()
        {
            NoiseLayer[] converted = new NoiseLayer[layers != null ? layers.Length : 0];
            for (int i = 0; i < converted.Length; i++)
            {
                NoiseLayerSettings s = layers[i];
                converted[i] = new NoiseLayer(s.scaleMeters, s.strength, s.octaves, s.persistence, s.lacunarity);
            }

            FlatZoneSettings f = flatZones ?? new FlatZoneSettings();
            FlatZoneParams flat = new FlatZoneParams(f.enabled, f.scaleMeters, f.octaves, f.threshold, f.blendWidth, f.maskContrast, f.minLevel, f.maxLevel);
            return new HeightParams(seed, maxHeightMeters, reliefContrast, converted, flat);
        }
    }
}
