using System;

namespace Leviathan.Terrain
{
    /// <summary>
    /// Grade de amostras da função de altura (alturas, gradiente, inclinação e máscara plana).
    /// Serve para pré-visualização e estatísticas. A posição de cada amostra é
    /// origem + índice * espaçamento (uma multiplicação, nunca somas acumuladas), para que
    /// grades vizinhas obtenham exatamente as mesmas coordenadas nas bordas compartilhadas.
    /// Índice da amostra (i, j): j * Width + i, com i ao longo de x e j ao longo de z.
    /// </summary>
    public sealed class HeightGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float OriginX;
        public readonly float OriginZ;
        public readonly float MetersPerSample;
        public readonly float[] Heights;
        public readonly float[] GradientX;
        public readonly float[] GradientZ;
        public readonly float[] SlopeDegrees;
        public readonly float[] FlatMask;

        HeightGrid(int width, int height, float originX, float originZ, float metersPerSample)
        {
            Width = width;
            Height = height;
            OriginX = originX;
            OriginZ = originZ;
            MetersPerSample = metersPerSample;
            int count = width * height;
            Heights = new float[count];
            GradientX = new float[count];
            GradientZ = new float[count];
            SlopeDegrees = new float[count];
            FlatMask = new float[count];
        }

        public int IndexOf(int i, int j)
        {
            return j * Width + i;
        }

        public float WorldX(int i)
        {
            return OriginX + i * MetersPerSample;
        }

        public float WorldZ(int j)
        {
            return OriginZ + j * MetersPerSample;
        }

        /// <summary>Amostra uma região retangular da função de altura.</summary>
        public static HeightGrid Bake(TerrainHeightSampler sampler, float originX, float originZ, float metersPerSample, int width, int height)
        {
            if (sampler == null)
            {
                throw new ArgumentNullException("sampler");
            }

            if (width < 2 || height < 2)
            {
                throw new ArgumentOutOfRangeException("width", "A grade precisa ter pelo menos 2 x 2 amostras.");
            }

            if (metersPerSample <= 0f)
            {
                throw new ArgumentOutOfRangeException("metersPerSample", "O espaçamento precisa ser positivo.");
            }

            HeightGrid grid = new HeightGrid(width, height, originX, originZ, metersPerSample);
            for (int j = 0; j < height; j++)
            {
                float z = grid.WorldZ(j);
                for (int i = 0; i < width; i++)
                {
                    float x = grid.WorldX(i);
                    int index = grid.IndexOf(i, j);
                    float h = sampler.Sample(x, z, out float gx, out float gz);
                    grid.Heights[index] = h;
                    grid.GradientX[index] = gx;
                    grid.GradientZ[index] = gz;
                    grid.SlopeDegrees[index] = SlopeMath.GradientToDegrees(gx, gz);
                    grid.FlatMask[index] = sampler.SampleFlatMask(x, z);
                }
            }

            return grid;
        }

        /// <summary>Amostra o domínio inteiro (0 a DomainSizeMeters) com N amostras por lado, incluindo as bordas.</summary>
        public static HeightGrid BakeDomain(TerrainHeightSampler sampler, int samplesPerSide)
        {
            if (samplesPerSide < 2)
            {
                throw new ArgumentOutOfRangeException("samplesPerSide", "São necessárias pelo menos 2 amostras por lado.");
            }

            float metersPerSample = TerrainConstants.DomainSizeMeters / (samplesPerSide - 1);
            return Bake(sampler, 0f, 0f, metersPerSample, samplesPerSide, samplesPerSide);
        }

        public HeightGridStats ComputeStats()
        {
            return HeightGridStats.Compute(Heights, SlopeDegrees, TerrainConstants.FlatMaxDegrees, TerrainConstants.SteepMinDegrees);
        }
    }

    /// <summary>Estatísticas de uma grade: alturas e porcentagem de área plana e íngreme.</summary>
    public readonly struct HeightGridStats
    {
        public readonly float MinHeight;
        public readonly float MaxHeight;
        public readonly float MeanHeight;
        public readonly float PercentFlat;
        public readonly float PercentSteep;

        HeightGridStats(float min, float max, float mean, float percentFlat, float percentSteep)
        {
            MinHeight = min;
            MaxHeight = max;
            MeanHeight = mean;
            PercentFlat = percentFlat;
            PercentSteep = percentSteep;
        }

        /// <summary>
        /// PercentFlat = amostras com inclinação estritamente menor que flatMaxDegrees;
        /// PercentSteep = amostras com inclinação estritamente maior que steepMinDegrees (ambos em % de 0 a 100).
        /// </summary>
        public static HeightGridStats Compute(float[] heights, float[] slopeDegrees, float flatMaxDegrees, float steepMinDegrees)
        {
            if (heights == null || slopeDegrees == null || heights.Length == 0 || heights.Length != slopeDegrees.Length)
            {
                throw new ArgumentException("As listas de alturas e inclinações precisam ter o mesmo tamanho, maior que zero.");
            }

            float min = float.MaxValue, max = float.MinValue;
            double sum = 0.0;
            int flat = 0, steep = 0;
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                if (h < min) min = h;
                if (h > max) max = h;
                sum += h;
                if (slopeDegrees[i] < flatMaxDegrees) flat++;
                if (slopeDegrees[i] > steepMinDegrees) steep++;
            }

            float count = heights.Length;
            return new HeightGridStats(min, max, (float)(sum / count), 100f * flat / count, 100f * steep / count);
        }
    }
}
