using System;

namespace Leviathan.Terrain
{
    /// <summary>
    /// Ruído de gradiente 2D (estilo Perlin) com derivadas analíticas, e fBm sobre ele.
    /// Sem estado e sem Unity API: mesma entrada gera sempre a mesma saída.
    ///
    /// Matemática: em cada célula inteira há 4 cantos, cada um com um gradiente unitário
    /// escolhido por hash entre 16 direções. Com (fx, fz) a posição local na célula:
    ///   a, b, c, d = produto escalar de cada gradiente de canto com o vetor até o ponto
    ///   n = a + (b - a) ux + (c - a) uz + (a - b - c + d) ux uz,   u = fade(f) (quíntica)
    /// As derivadas saem da regra do produto sobre essa expressão (ver comentários no código).
    /// O resultado é multiplicado por sqrt(2) para ficar aproximadamente em [-1, 1].
    /// </summary>
    public static class GradientNoise
    {
        const float Normalize = 1.41421356f;

        // 16 direções unitárias (ângulos de 22,5 graus), como literais para ser independente de plataforma.
        static readonly float[] DirectionX =
        {
            1f, 0.92387953f, 0.70710678f, 0.38268343f,
            0f, -0.38268343f, -0.70710678f, -0.92387953f,
            -1f, -0.92387953f, -0.70710678f, -0.38268343f,
            0f, 0.38268343f, 0.70710678f, 0.92387953f
        };

        static readonly float[] DirectionZ =
        {
            0f, 0.38268343f, 0.70710678f, 0.92387953f,
            1f, 0.92387953f, 0.70710678f, 0.38268343f,
            0f, -0.38268343f, -0.70710678f, -0.92387953f,
            -1f, -0.92387953f, -0.70710678f, -0.38268343f
        };

        /// <summary>Hash inteiro de uma célula e uma semente. Sem System.Random e sem estado.</summary>
        public static uint Hash(int x, int z, uint seed)
        {
            unchecked
            {
                uint h = ((uint)x * 0x9E3779B1u) ^ ((uint)z * 0x85EBCA77u) ^ (seed * 0xC2B2AE3Du);
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }

        static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static float FadeDerivative(float t)
        {
            return 30f * t * t * (t - 1f) * (t - 1f);
        }

        /// <summary>
        /// Valor do ruído em (px, pz), coordenadas no espaço do ruído (uma célula = 1 unidade).
        /// dnx e dnz são as derivadas em relação a px e pz.
        /// </summary>
        public static float Evaluate(float px, float pz, uint seed, out float dnx, out float dnz)
        {
            float floorX = (float)Math.Floor(px);
            float floorZ = (float)Math.Floor(pz);
            int ix = (int)floorX;
            int iz = (int)floorZ;
            float fx = px - floorX;
            float fz = pz - floorZ;

            int i00 = (int)(Hash(ix, iz, seed) & 15u);
            int i10 = (int)(Hash(ix + 1, iz, seed) & 15u);
            int i01 = (int)(Hash(ix, iz + 1, seed) & 15u);
            int i11 = (int)(Hash(ix + 1, iz + 1, seed) & 15u);

            float g00x = DirectionX[i00], g00z = DirectionZ[i00];
            float g10x = DirectionX[i10], g10z = DirectionZ[i10];
            float g01x = DirectionX[i01], g01z = DirectionZ[i01];
            float g11x = DirectionX[i11], g11z = DirectionZ[i11];

            float a = g00x * fx + g00z * fz;
            float b = g10x * (fx - 1f) + g10z * fz;
            float c = g01x * fx + g01z * (fz - 1f);
            float d = g11x * (fx - 1f) + g11z * (fz - 1f);

            float ux = Fade(fx);
            float uz = Fade(fz);
            float dux = FadeDerivative(fx);
            float duz = FadeDerivative(fz);

            float k = a - b - c + d;
            float n = a + (b - a) * ux + (c - a) * uz + k * ux * uz;

            // Derivadas dos produtos escalares: da/dfx = g00x, db/dfx = g10x, dc/dfx = g01x, dd/dfx = g11x (idem em z).
            float dkx = g00x - g10x - g01x + g11x;
            float dkz = g00z - g10z - g01z + g11z;

            dnx = g00x + (g10x - g00x) * ux + (b - a) * dux + (g01x - g00x) * uz + dkx * ux * uz + k * dux * uz;
            dnz = g00z + (g10z - g00z) * ux + (g01z - g00z) * uz + (c - a) * duz + dkz * ux * uz + k * ux * duz;

            dnx *= Normalize;
            dnz *= Normalize;
            return n * Normalize;
        }

        /// <summary>
        /// Soma de oitavas, normalizada para aproximadamente [-1, 1].
        /// x e z estão em metros; scaleMeters é o comprimento de onda da primeira oitava.
        /// dx e dz são as derivadas em relação a x e z (metros), já com a mesma normalização.
        /// </summary>
        public static float Fbm(float x, float z, float scaleMeters, int octaves, float persistence, float lacunarity, uint seed, out float dx, out float dz)
        {
            float value = 0f, sumDx = 0f, sumDz = 0f;
            float amplitude = 1f, frequency = 1f, amplitudeSum = 0f;
            float inverseScale = 1f / scaleMeters;

            for (int o = 0; o < octaves; o++)
            {
                uint octaveSeed = unchecked(seed * 31u + (uint)o * 7919u + 1u);
                float n = Evaluate(x * inverseScale * frequency, z * inverseScale * frequency, octaveSeed, out float ndx, out float ndz);
                value += amplitude * n;
                sumDx += amplitude * frequency * ndx * inverseScale;
                sumDz += amplitude * frequency * ndz * inverseScale;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            float normalization = 1f / amplitudeSum;
            dx = sumDx * normalization;
            dz = sumDz * normalization;
            return value * normalization;
        }
    }
}
