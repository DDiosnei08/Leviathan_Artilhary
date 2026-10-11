using System;
using NUnit.Framework;

namespace Leviathan.Terrain.Tests
{
    public class HeightGridTests
    {
        static TerrainHeightSampler CreateSampler()
        {
            return new TerrainHeightSampler(HeightParams.CreateDefault(12345));
        }

        [Test]
        public void BakeDomain_CoversTheWholeDomainIncludingBorders()
        {
            HeightGrid grid = HeightGrid.BakeDomain(CreateSampler(), 33);
            Assert.AreEqual(33, grid.Width);
            Assert.AreEqual(33, grid.Height);
            Assert.AreEqual(0f, grid.WorldX(0));
            Assert.AreEqual(TerrainConstants.DomainSizeMeters, grid.WorldX(32), 0.01f);
            Assert.AreEqual(TerrainConstants.DomainSizeMeters, grid.WorldZ(32), 0.01f);
        }

        [Test]
        public void Bake_StoresTheSamplerValueAtEachPosition()
        {
            TerrainHeightSampler sampler = CreateSampler();
            HeightGrid grid = HeightGrid.Bake(sampler, 1000f, 2000f, 10f, 8, 6);
            for (int j = 0; j < 6; j++)
            {
                for (int i = 0; i < 8; i++)
                {
                    float expected = sampler.Sample(1000f + i * 10f, 2000f + j * 10f);
                    Assert.AreEqual(expected, grid.Heights[grid.IndexOf(i, j)]);
                }
            }
        }

        [Test]
        public void NeighbouringGrids_ShareExactlyTheSameBorderValues()
        {
            TerrainHeightSampler sampler = CreateSampler();
            HeightGrid left = HeightGrid.Bake(sampler, 0f, 0f, 10f, 11, 11);
            HeightGrid right = HeightGrid.Bake(sampler, 100f, 0f, 10f, 11, 11);
            for (int j = 0; j < 11; j++)
            {
                Assert.AreEqual(left.Heights[left.IndexOf(10, j)], right.Heights[right.IndexOf(0, j)], "Linha " + j);
            }
        }

        [Test]
        public void Bake_RejectsInvalidArguments()
        {
            TerrainHeightSampler sampler = CreateSampler();
            Assert.Throws<ArgumentNullException>(() => HeightGrid.Bake(null, 0f, 0f, 10f, 4, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightGrid.Bake(sampler, 0f, 0f, 10f, 1, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => HeightGrid.Bake(sampler, 0f, 0f, 0f, 4, 4));
        }

        [Test]
        public void Stats_ComputeMinMaxMeanAndPercentages()
        {
            float[] heights = { 0f, 10f, 20f, 30f };
            float[] slopes = { 1f, 5f, 31f, 40f };
            HeightGridStats stats = HeightGridStats.Compute(heights, slopes, 3f, 30f);

            Assert.AreEqual(0f, stats.MinHeight);
            Assert.AreEqual(30f, stats.MaxHeight);
            Assert.AreEqual(15f, stats.MeanHeight, 1e-5f);
            Assert.AreEqual(25f, stats.PercentFlat, 1e-4f);
            Assert.AreEqual(50f, stats.PercentSteep, 1e-4f);
        }

        [Test]
        public void Stats_RejectMismatchedOrEmptyArrays()
        {
            Assert.Throws<ArgumentException>(() => HeightGridStats.Compute(new float[3], new float[2], 3f, 30f));
            Assert.Throws<ArgumentException>(() => HeightGridStats.Compute(new float[0], new float[0], 3f, 30f));
        }

        [Test]
        public void DomainConstants_AreConsistent()
        {
            Assert.AreEqual(20400f, TerrainConstants.DomainSizeMeters);
            Assert.AreEqual(10f, TerrainConstants.VertexSpacingMeters);
            Assert.AreEqual(121, TerrainConstants.ChunkVertices);
            Assert.LessOrEqual(TerrainConstants.DefaultMissionAreaMeters, TerrainConstants.DomainSizeMeters);
        }
    }
}
