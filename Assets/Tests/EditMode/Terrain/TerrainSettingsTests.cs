using NUnit.Framework;
using UnityEngine;

namespace Leviathan.Terrain.Tests
{
    public class TerrainSettingsTests
    {
        TerrainSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<TerrainSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void DefaultSettings_ProduceValidParameters()
        {
            HeightParams parameters = settings.ToParams();
            Assert.AreEqual(3, parameters.LayerCount);

            TerrainHeightSampler sampler = new TerrainHeightSampler(parameters);
            float h = sampler.Sample(5000f, 5000f, out float gx, out float gz);
            Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h));
            Assert.IsFalse(float.IsNaN(gx) || float.IsNaN(gz));
        }

        [Test]
        public void DefaultSettings_MatchTheCodeDefaults()
        {
            // O ScriptableObject e HeightParams.CreateDefault precisam descrever o MESMO terreno padrão.
            settings.seed = 12345;
            TerrainHeightSampler fromSettings = new TerrainHeightSampler(settings.ToParams());
            TerrainHeightSampler fromCode = new TerrainHeightSampler(HeightParams.CreateDefault(12345));
            for (int i = 0; i < 20; i++)
            {
                float x = 500f + i * 997f;
                float z = 300f + i * 877f;
                Assert.AreEqual(fromCode.Sample(x, z), fromSettings.Sample(x, z), 1e-3f);
            }
        }

        [Test]
        public void EmptyLayers_AreRejectedWithAClearError()
        {
            settings.layers = new NoiseLayerSettings[0];
            Assert.Throws<System.ArgumentException>(() => settings.ToParams());
        }
    }
}
