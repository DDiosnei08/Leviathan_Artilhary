using NUnit.Framework;

namespace Leviathan.Terrain.Tests
{
    public class SlopeMathTests
    {
        [Test]
        public void ZeroGradient_IsZeroDegrees()
        {
            Assert.AreEqual(0f, SlopeMath.GradientToDegrees(0f, 0f), 1e-6f);
        }

        [Test]
        public void GradientOfOne_Is45Degrees()
        {
            Assert.AreEqual(45f, SlopeMath.GradientToDegrees(1f, 0f), 1e-3f);
            Assert.AreEqual(45f, SlopeMath.GradientToDegrees(0f, -1f), 1e-3f);
        }

        [Test]
        public void OnlyTheMagnitudeMatters()
        {
            // (3, 4) tem magnitude 5, igual a (5, 0), (0, 5) e (-3, -4).
            float expected = SlopeMath.GradientToDegrees(5f, 0f);
            Assert.AreEqual(expected, SlopeMath.GradientToDegrees(3f, 4f), 1e-3f);
            Assert.AreEqual(expected, SlopeMath.GradientToDegrees(0f, 5f), 1e-3f);
            Assert.AreEqual(expected, SlopeMath.GradientToDegrees(-3f, -4f), 1e-3f);
        }

        [Test]
        public void DegreesAndGradient_RoundTrip()
        {
            foreach (float degrees in new[] { 3f, 15f, 30f, 60f })
            {
                float gradient = SlopeMath.DegreesToGradient(degrees);
                Assert.AreEqual(degrees, SlopeMath.GradientToDegrees(gradient, 0f), 1e-3f, "Ângulo " + degrees);
            }
        }
    }
}
