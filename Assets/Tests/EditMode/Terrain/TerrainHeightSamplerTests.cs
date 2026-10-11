using System;
using NUnit.Framework;

namespace Leviathan.Terrain.Tests
{
    public class TerrainHeightSamplerTests
    {
        const int ReferenceSeed = 12345;

        static TerrainHeightSampler CreateSampler(int seed)
        {
            return new TerrainHeightSampler(HeightParams.CreateDefault(seed));
        }

        [Test]
        public void SameSeed_GivesExactlySameHeights_InTwoInstances()
        {
            TerrainHeightSampler a = CreateSampler(ReferenceSeed);
            TerrainHeightSampler b = CreateSampler(ReferenceSeed);
            Random random = new Random(10);
            for (int i = 0; i < 200; i++)
            {
                float x = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                float z = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                Assert.AreEqual(a.Sample(x, z), b.Sample(x, z));
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentTerrain()
        {
            TerrainHeightSampler a = CreateSampler(1);
            TerrainHeightSampler b = CreateSampler(2);
            Random random = new Random(11);
            double totalDifference = 0.0;
            for (int i = 0; i < 200; i++)
            {
                float x = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                float z = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                totalDifference += Math.Abs(a.Sample(x, z) - b.Sample(x, z));
            }

            Assert.Greater(totalDifference / 200.0, 10.0, "Sementes diferentes deveriam diferir em dezenas de metros, em média.");
        }

        [Test]
        public void Heights_StayWithinZeroAndMaxHeight()
        {
            HeightGrid grid = HeightGrid.BakeDomain(CreateSampler(ReferenceSeed), 128);
            for (int i = 0; i < grid.Heights.Length; i++)
            {
                Assert.That(grid.Heights[i], Is.InRange(0f, 1000f));
            }
        }

        [Test]
        public void Gradient_MatchesFiniteDifference()
        {
            TerrainHeightSampler sampler = CreateSampler(ReferenceSeed);
            Random random = new Random(12);
            const float eps = 1f;
            for (int i = 0; i < 300; i++)
            {
                float x = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                float z = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                sampler.Sample(x, z, out float dhdx, out float dhdz);

                float fdx = (sampler.Sample(x + eps, z) - sampler.Sample(x - eps, z)) / (2f * eps);
                float fdz = (sampler.Sample(x, z + eps) - sampler.Sample(x, z - eps)) / (2f * eps);

                Assert.AreEqual(fdx, dhdx, 0.005f, "dh/dx em (" + x + ", " + z + ")");
                Assert.AreEqual(fdz, dhdz, 0.005f, "dh/dz em (" + x + ", " + z + ")");
            }
        }

        [Test]
        public void ReferenceValues_MatchTheValidatedMirrorImplementation()
        {
            // Valores calculados por uma implementação de referência independente (Python) com a
            // semente 12345 e os parâmetros padrão. Se estes valores mudarem, o gerador mudou:
            // aumente HeightParams.GeneratorVersion e atualize esta tabela de propósito.
            TerrainHeightSampler sampler = CreateSampler(ReferenceSeed);
            AssertReference(sampler, 1000f, 1000f, 370.4507f, -0.296510f, 0.023544f, 0.0000f);
            AssertReference(sampler, 5000f, 12000f, 639.6177f, 0.181477f, -0.181256f, 0.0000f);
            AssertReference(sampler, 20000f, 300f, 402.2791f, 0.136112f, -0.070891f, 0.1929f);
            AssertReference(sampler, 10200f, 10200f, 500.5134f, -0.390530f, 0.001265f, 0.0000f);
            AssertReference(sampler, 15555f, 7777f, 817.5843f, -0.104536f, -0.097320f, 0.0184f);
            AssertReference(sampler, 333f, 19999f, 494.9160f, 0.011203f, -0.136234f, 0.3794f);
        }

        static void AssertReference(TerrainHeightSampler sampler, float x, float z, float height, float dhdx, float dhdz, float mask)
        {
            float h = sampler.Sample(x, z, out float gx, out float gz);
            string where = "em (" + x + ", " + z + ")";
            Assert.AreEqual(height, h, 0.15f, "altura " + where);
            Assert.AreEqual(dhdx, gx, 0.003f, "dh/dx " + where);
            Assert.AreEqual(dhdz, gz, 0.003f, "dh/dz " + where);
            Assert.AreEqual(mask, sampler.SampleFlatMask(x, z), 0.01f, "máscara " + where);
        }

        [Test]
        public void DefaultTerrain_HasBothMountainsAndPlains()
        {
            // Referência (Python, semente 12345, grade 256): mín 233,8 m; máx 832,4 m; plana 23,3 %; íngreme 1,1 %.
            HeightGrid grid = HeightGrid.BakeDomain(CreateSampler(ReferenceSeed), 256);
            HeightGridStats stats = grid.ComputeStats();

            Assert.Less(stats.MinHeight, 350f, "Deveria haver baixadas.");
            Assert.Greater(stats.MaxHeight, 650f, "Deveria haver montanhas.");
            Assert.That(stats.PercentFlat, Is.InRange(15f, 40f), "Área plana (< 3°) em %.");
            Assert.Less(stats.PercentSteep, 5f, "Área íngreme (> 30°) em %.");
        }

        [Test]
        public void FlatZones_AreActuallyFlat()
        {
            HeightGrid grid = HeightGrid.BakeDomain(CreateSampler(ReferenceSeed), 256);
            int flatSamples = 0;
            for (int i = 0; i < grid.FlatMask.Length; i++)
            {
                if (grid.FlatMask[i] > 0.999f)
                {
                    flatSamples++;
                    Assert.Less(grid.SlopeDegrees[i], TerrainConstants.FlatMaxDegrees, "Inclinação dentro da zona plana, amostra " + i);
                }
            }

            Assert.Greater(flatSamples, 1000, "Deveria haver zonas planas no mapa.");
        }

        [Test]
        public void DisabledFlatZones_GiveZeroMask()
        {
            HeightParams defaults = HeightParams.CreateDefault(ReferenceSeed);
            HeightParams withoutFlat = new HeightParams(ReferenceSeed, defaults.MaxHeightMeters, defaults.ReliefContrast, defaults.CopyLayers(), FlatZoneParams.Disabled);
            TerrainHeightSampler sampler = new TerrainHeightSampler(withoutFlat);
            Assert.AreEqual(0f, sampler.SampleFlatMask(5000f, 5000f));
            Assert.AreEqual(0f, sampler.SampleFlatMask(12000f, 700f));
        }

        [Test]
        public void HeightParams_RejectsEmptyOrZeroStrengthLayers()
        {
            Assert.Throws<ArgumentException>(() => new HeightParams(1, 1000f, 3f, new NoiseLayer[0], FlatZoneParams.Disabled));
            Assert.Throws<ArgumentException>(() => new HeightParams(1, 1000f, 3f, new[] { new NoiseLayer(1000f, 0f, 3, 0.4f, 2f) }, FlatZoneParams.Disabled));
        }

        [Test]
        public void HeightParams_ClampsInvalidValuesOnce()
        {
            NoiseLayer layer = new NoiseLayer(-5f, 0.5f, 99, 5f, 0f);
            Assert.AreEqual(1f, layer.ScaleMeters);
            Assert.AreEqual(8, layer.Octaves);
            Assert.AreEqual(0.95f, layer.Persistence, 1e-6f);
            Assert.AreEqual(1.1f, layer.Lacunarity, 1e-6f);
        }
    }
}
