using System;

namespace Leviathan.Terrain
{
    /// <summary>
    /// A função de altura do terreno: Altura(x, z, semente). É a ÚNICA fonte da verdade.
    /// Chunks, pré-visualização, consultas e impactos devem perguntar aqui, nunca guardar uma
    /// segunda definição de altura. Sem estado mutável: pode ser chamada de qualquer thread.
    ///
    /// Pipeline (tudo com derivadas analíticas, regra da cadeia a cada passo):
    ///   1. camadas de ruído fBm somadas e normalizadas para [-1, 1]
    ///   2. contraste suave do relevo (função racional, sem corte duro)
    ///   3. altura em [0, 1] = 0.5 + 0.5 * contraste
    ///   4. zonas planas: mistura com o relevo de grande escala (primeira camada)
    ///   5. multiplicação pela altura máxima em metros
    /// O gradiente devolvido é (dh/dx, dh/dz) em metros por metro (adimensional); a inclinação em
    /// graus sai de SlopeMath.GradientToDegrees.
    /// </summary>
    public sealed class TerrainHeightSampler
    {
        const float MaskPersistence = 0.5f;
        const float MaskLacunarity = 2f;
        const int FlatMaskSeedOffset = 21001;
        const int LayerSeedStride = 1000;

        readonly HeightParams parameters;
        readonly NoiseLayer[] layers;
        readonly float strengthSum;

        public TerrainHeightSampler(HeightParams parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            this.parameters = parameters;
            layers = parameters.CopyLayers();
            float sum = 0f;
            for (int i = 0; i < layers.Length; i++)
            {
                sum += layers[i].Strength;
            }

            strengthSum = sum;
        }

        public HeightParams Parameters
        {
            get { return parameters; }
        }

        public float Sample(float x, float z)
        {
            return Sample(x, z, out float _, out float _);
        }

        /// <summary>Altura em metros e gradiente (dh/dx, dh/dz) em metros por metro.</summary>
        public float Sample(float x, float z, out float dhdx, out float dhdz)
        {
            uint seed = unchecked((uint)parameters.Seed);

            float sum = 0f, sumDx = 0f, sumDz = 0f;
            float macroValue = 0f, macroDx = 0f, macroDz = 0f;

            for (int i = 0; i < layers.Length; i++)
            {
                NoiseLayer layer = layers[i];
                uint layerSeed = unchecked(seed + (uint)(LayerSeedStride * (i + 1)));
                float v = GradientNoise.Fbm(x, z, layer.ScaleMeters, layer.Octaves, layer.Persistence, layer.Lacunarity, layerSeed, out float dx, out float dz);
                if (i == 0)
                {
                    macroValue = Clamp(v, -1f, 1f);
                    macroDx = dx;
                    macroDz = dz;
                }

                sum += layer.Strength * v;
                sumDx += layer.Strength * dx;
                sumDz += layer.Strength * dz;
            }

            float inverseStrength = 1f / strengthSum;
            float relief = Clamp(sum * inverseStrength, -1f, 1f);
            float reliefDx = sumDx * inverseStrength;
            float reliefDz = sumDz * inverseStrength;

            float contrasted = Contrast(relief, parameters.ReliefContrast, out float contrastSlope);
            float h = 0.5f + 0.5f * contrasted;
            float hdx = 0.5f * contrastSlope * reliefDx;
            float hdz = 0.5f * contrastSlope * reliefDz;

            FlatZoneParams flat = parameters.FlatZones;
            if (flat.Enabled)
            {
                float mask = FlatMask(x, z, seed, flat, out float maskDx, out float maskDz);

                float span = flat.MaxLevel - flat.MinLevel;
                float level = flat.MinLevel + span * (0.5f + 0.5f * macroValue);
                float levelDx = span * 0.5f * macroDx;
                float levelDz = span * 0.5f * macroDz;

                float difference = level - h;
                float blendedDx = hdx * (1f - mask) + difference * maskDx + mask * levelDx;
                float blendedDz = hdz * (1f - mask) + difference * maskDz + mask * levelDz;
                h += difference * mask;
                hdx = blendedDx;
                hdz = blendedDz;
            }

            float max = parameters.MaxHeightMeters;
            dhdx = hdx * max;
            dhdz = hdz * max;
            return h * max;
        }

        /// <summary>Máscara de zonas planas em [0, 1] (0 = relevo normal, 1 = plano). Para depuração e estatísticas.</summary>
        public float SampleFlatMask(float x, float z)
        {
            FlatZoneParams flat = parameters.FlatZones;
            if (!flat.Enabled)
            {
                return 0f;
            }

            uint seed = unchecked((uint)parameters.Seed);
            return FlatMask(x, z, seed, flat, out float _, out float _);
        }

        static float FlatMask(float x, float z, uint seed, FlatZoneParams flat, out float maskDx, out float maskDz)
        {
            uint maskSeed = unchecked(seed + (uint)FlatMaskSeedOffset);
            float v = GradientNoise.Fbm(x, z, flat.ScaleMeters, flat.Octaves, MaskPersistence, MaskLacunarity, maskSeed, out float dx, out float dz);
            v = Clamp(v, -1f, 1f);

            float contrasted = Contrast(v, flat.MaskContrast, out float contrastSlope);
            float m01 = 0.5f + 0.5f * contrasted;
            float m01dx = 0.5f * contrastSlope * dx;
            float m01dz = 0.5f * contrastSlope * dz;

            float mask = SmoothStep(flat.Threshold, flat.Threshold + flat.BlendWidth, m01, out float maskSlope);
            maskDx = maskSlope * m01dx;
            maskDz = maskSlope * m01dz;
            return mask;
        }

        /// <summary>
        /// Contraste suave para x em [-1, 1]: y = x (1 + k) / (1 + k |x|), com k = contraste - 1.
        /// Inclinação no centro = contraste; nas pontas = 1 / contraste. Contínua, sem corte duro.
        /// slope recebe dy/dx.
        /// </summary>
        static float Contrast(float x, float contrast, out float slope)
        {
            float k = Math.Max(contrast - 1f, 0f);
            float denominator = 1f + k * Math.Abs(x);
            slope = (1f + k) / (denominator * denominator);
            return x * (1f + k) / denominator;
        }

        static float SmoothStep(float edge0, float edge1, float x, out float slope)
        {
            float t = (x - edge0) / (edge1 - edge0);
            if (t <= 0f)
            {
                slope = 0f;
                return 0f;
            }

            if (t >= 1f)
            {
                slope = 0f;
                return 1f;
            }

            slope = 6f * t * (1f - t) / (edge1 - edge0);
            return t * t * (3f - 2f * t);
        }

        static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
