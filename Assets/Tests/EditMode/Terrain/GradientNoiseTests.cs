using NUnit.Framework;

namespace Leviathan.Terrain.Tests
{
    public class GradientNoiseTests
    {
        [Test]
        public void Hash_IsDeterministic_AndDependsOnEveryInput()
        {
            uint a = GradientNoise.Hash(10, 20, 5u);
            Assert.AreEqual(a, GradientNoise.Hash(10, 20, 5u));
            Assert.AreNotEqual(a, GradientNoise.Hash(11, 20, 5u));
            Assert.AreNotEqual(a, GradientNoise.Hash(10, 21, 5u));
            Assert.AreNotEqual(a, GradientNoise.Hash(10, 20, 6u));
        }

        [Test]
        public void Hash_AcceptsNegativeCoordinates()
        {
            Assert.AreEqual(GradientNoise.Hash(-5, -7, 3u), GradientNoise.Hash(-5, -7, 3u));
            Assert.AreNotEqual(GradientNoise.Hash(-5, -7, 3u), GradientNoise.Hash(5, 7, 3u));
        }

        [Test]
        public void Evaluate_IsDeterministic()
        {
            float a = GradientNoise.Evaluate(12.34f, 56.78f, 99u, out float adx, out float adz);
            float b = GradientNoise.Evaluate(12.34f, 56.78f, 99u, out float bdx, out float bdz);
            Assert.AreEqual(a, b);
            Assert.AreEqual(adx, bdx);
            Assert.AreEqual(adz, bdz);
        }

        [Test]
        public void Evaluate_StaysWithinExpectedRange()
        {
            System.Random random = new System.Random(1);
            for (int i = 0; i < 20000; i++)
            {
                float px = (float)(random.NextDouble() * 500.0);
                float pz = (float)(random.NextDouble() * 500.0);
                float n = GradientNoise.Evaluate(px, pz, 7u, out float _, out float _);
                Assert.That(n, Is.InRange(-1.05f, 1.05f));
            }
        }

        [Test]
        public void Evaluate_AnalyticDerivative_MatchesFiniteDifference()
        {
            System.Random random = new System.Random(2);
            const float eps = 0.01f;
            for (int i = 0; i < 500; i++)
            {
                float px = (float)(random.NextDouble() * 50.0);
                float pz = (float)(random.NextDouble() * 50.0);
                GradientNoise.Evaluate(px, pz, 11u, out float dx, out float dz);

                float nx1 = GradientNoise.Evaluate(px + eps, pz, 11u, out float _, out float _);
                float nx0 = GradientNoise.Evaluate(px - eps, pz, 11u, out float _, out float _);
                float nz1 = GradientNoise.Evaluate(px, pz + eps, 11u, out float _, out float _);
                float nz0 = GradientNoise.Evaluate(px, pz - eps, 11u, out float _, out float _);

                Assert.AreEqual((nx1 - nx0) / (2f * eps), dx, 0.01f, "d/dx em (" + px + ", " + pz + ")");
                Assert.AreEqual((nz1 - nz0) / (2f * eps), dz, 0.01f, "d/dz em (" + px + ", " + pz + ")");
            }
        }

        [Test]
        public void Evaluate_IsContinuousAcrossCellBoundaries()
        {
            for (int boundary = 1; boundary <= 20; boundary++)
            {
                float before = GradientNoise.Evaluate(boundary - 1e-4f, 3.37f, 5u, out float _, out float _);
                float after = GradientNoise.Evaluate(boundary + 1e-4f, 3.37f, 5u, out float _, out float _);
                Assert.AreEqual(before, after, 1e-3f, "Fronteira x = " + boundary);

                float beforeZ = GradientNoise.Evaluate(4.21f, boundary - 1e-4f, 5u, out float _, out float _);
                float afterZ = GradientNoise.Evaluate(4.21f, boundary + 1e-4f, 5u, out float _, out float _);
                Assert.AreEqual(beforeZ, afterZ, 1e-3f, "Fronteira z = " + boundary);
            }
        }

        [Test]
        public void Fbm_AnalyticDerivative_MatchesFiniteDifferenceInMeters()
        {
            System.Random random = new System.Random(3);
            const float eps = 1f;
            for (int i = 0; i < 300; i++)
            {
                float x = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                float z = (float)(random.NextDouble() * TerrainConstants.DomainSizeMeters);
                GradientNoise.Fbm(x, z, 600f, 3, 0.4f, 2f, 7u, out float dx, out float dz);

                float x1 = GradientNoise.Fbm(x + eps, z, 600f, 3, 0.4f, 2f, 7u, out float _, out float _);
                float x0 = GradientNoise.Fbm(x - eps, z, 600f, 3, 0.4f, 2f, 7u, out float _, out float _);
                float z1 = GradientNoise.Fbm(x, z + eps, 600f, 3, 0.4f, 2f, 7u, out float _, out float _);
                float z0 = GradientNoise.Fbm(x, z - eps, 600f, 3, 0.4f, 2f, 7u, out float _, out float _);

                Assert.AreEqual((x1 - x0) / (2f * eps), dx, 2e-4f, "d/dx em (" + x + ", " + z + ")");
                Assert.AreEqual((z1 - z0) / (2f * eps), dz, 2e-4f, "d/dz em (" + x + ", " + z + ")");
            }
        }
    }
}
