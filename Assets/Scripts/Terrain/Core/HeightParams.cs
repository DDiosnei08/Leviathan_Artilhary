using System;

namespace Leviathan.Terrain
{
    /// <summary>Uma camada de ruído de relevo (continentes, montanhas, colinas...).</summary>
    public readonly struct NoiseLayer
    {
        public readonly float ScaleMeters;
        public readonly float Strength;
        public readonly int Octaves;
        public readonly float Persistence;
        public readonly float Lacunarity;

        public NoiseLayer(float scaleMeters, float strength, int octaves, float persistence, float lacunarity)
        {
            ScaleMeters = Math.Max(1f, scaleMeters);
            Strength = Math.Max(0f, strength);
            Octaves = Math.Min(8, Math.Max(1, octaves));
            Persistence = Math.Min(0.95f, Math.Max(0.05f, persistence));
            Lacunarity = Math.Min(4f, Math.Max(1.1f, lacunarity));
        }
    }

    /// <summary>
    /// Zonas planas: onde a máscara vale 1, o relevo de detalhe é removido e a altura segue
    /// apenas o relevo de grande escala (a primeira camada), com um nível entre MinLevel e MaxLevel
    /// (fração da altura máxima). Assim as planícies acompanham o relevo geral em vez de formar degraus.
    /// </summary>
    public readonly struct FlatZoneParams
    {
        public readonly bool Enabled;
        public readonly float ScaleMeters;
        public readonly int Octaves;
        public readonly float Threshold;
        public readonly float BlendWidth;
        public readonly float MaskContrast;
        public readonly float MinLevel;
        public readonly float MaxLevel;

        public FlatZoneParams(bool enabled, float scaleMeters, int octaves, float threshold, float blendWidth, float maskContrast, float minLevel, float maxLevel)
        {
            Enabled = enabled;
            ScaleMeters = Math.Max(1f, scaleMeters);
            Octaves = Math.Min(8, Math.Max(1, octaves));
            Threshold = Math.Min(1f, Math.Max(0f, threshold));
            BlendWidth = Math.Max(0.001f, blendWidth);
            MaskContrast = Math.Max(1f, maskContrast);
            float lo = Math.Min(1f, Math.Max(0f, minLevel));
            float hi = Math.Min(1f, Math.Max(0f, maxLevel));
            MinLevel = Math.Min(lo, hi);
            MaxLevel = Math.Max(lo, hi);
        }

        public static FlatZoneParams Disabled
        {
            get { return new FlatZoneParams(false, 1f, 1, 0.5f, 0.2f, 1f, 0.25f, 0.55f); }
        }
    }

    /// <summary>
    /// Parâmetros da função de altura. É IMUTÁVEL: depois de criado, nada muda no meio de uma geração.
    /// A validação (limites) acontece uma vez, aqui, e não a cada amostra.
    /// </summary>
    public sealed class HeightParams
    {
        public const int GeneratorVersion = 1;

        public readonly int Seed;
        public readonly float MaxHeightMeters;
        public readonly float ReliefContrast;
        public readonly FlatZoneParams FlatZones;
        readonly NoiseLayer[] layers;

        public HeightParams(int seed, float maxHeightMeters, float reliefContrast, NoiseLayer[] noiseLayers, FlatZoneParams flatZones)
        {
            if (noiseLayers == null || noiseLayers.Length == 0)
            {
                throw new ArgumentException("É preciso pelo menos uma camada de ruído.", "noiseLayers");
            }

            float strengthSum = 0f;
            for (int i = 0; i < noiseLayers.Length; i++)
            {
                strengthSum += noiseLayers[i].Strength;
            }

            if (strengthSum <= 0f)
            {
                throw new ArgumentException("A soma das forças das camadas precisa ser maior que zero.", "noiseLayers");
            }

            Seed = seed;
            MaxHeightMeters = Math.Max(1f, maxHeightMeters);
            ReliefContrast = Math.Max(1f, reliefContrast);
            FlatZones = flatZones;
            layers = (NoiseLayer[])noiseLayers.Clone();
        }

        public int LayerCount
        {
            get { return layers.Length; }
        }

        public NoiseLayer GetLayer(int index)
        {
            return layers[index];
        }

        /// <summary>Cópia das camadas (o array interno nunca é exposto).</summary>
        public NoiseLayer[] CopyLayers()
        {
            return (NoiseLayer[])layers.Clone();
        }

        /// <summary>Parâmetros iniciais calibrados (ver docs/FASE-1.md).</summary>
        public static HeightParams CreateDefault(int seed)
        {
            return new HeightParams(
                seed,
                1000f,
                3f,
                new[]
                {
                    new NoiseLayer(9000f, 0.30f, 3, 0.4f, 2f),
                    new NoiseLayer(3000f, 0.15f, 3, 0.4f, 2f),
                    new NoiseLayer(600f, 0.04f, 2, 0.4f, 2f)
                },
                new FlatZoneParams(true, 4000f, 2, 0.45f, 0.3f, 2.5f, 0.25f, 0.55f));
        }
    }
}
