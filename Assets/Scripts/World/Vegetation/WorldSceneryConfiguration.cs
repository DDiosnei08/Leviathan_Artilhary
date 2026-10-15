using UnityEngine;

namespace ProjectLeviathan.Vegetation
{
    public enum SceneryRole { Rock, Lamp, Barrier, Crate, Shrub, FallenLog }

    [System.Serializable]
    public sealed class SceneryPrototype
    {
        public string label;
        public SceneryRole role;
        public Mesh mesh;
        [Tooltip("Optional visual override; otherwise uses the shared scenery material.")]
        public Material material;
        public Vector2 sizeRange=new Vector2(1,3);
    }

    [CreateAssetMenu(menuName="Leviathan/World Scenery")]
    public sealed class WorldSceneryConfiguration : ScriptableObject
    {
        public Material material;
        public SceneryPrototype[] prototypes;
        [Min(10)] public float rockSpacing=32;
        [Range(0,1)] public float rockDensity=.32f;
        [Min(20)] public float clusterScale=130;
        [Range(0,60)] public float maximumRockSlope=38;
        [Min(1)] public int maximumInstancesPerChunk=700;
        [Min(1)] public int buildsPerFrame=1;
        [Min(20)] public float drawDistance=500;
        [Min(0)] public float shadowDistance=75;
        [Min(20)] public float roadsideSpacing=65;
        public bool roadsideFurniture=true;
        [Min(0)] public float collisionDistance=45;
        [Range(8,128)] public int maximumColliders=64;
    }
}
