using UnityEngine;

namespace ProjectLeviathan.Vegetation
{
    [CreateAssetMenu(menuName="Leviathan/Tree Configuration")]
    public sealed class TreeConfiguration : ScriptableObject
    {
        public Material material;
        public Mesh broadleafNear, broadleafFar, pineNear, pineFar;
        [Min(6)] public float spacing=18;
        [Range(0,1)] public float density=.28f;
        [Range(0,1)] public float scatteredDensity=.12f;
        [Min(40)] public float groveScale=240;
        [Range(0,1)] public float groveThreshold=.42f;
        [Range(.01f,.3f)] public float groveEdgeBlend=.12f;
        [Range(32,2000)] public int maximumTreesPerChunk=650;
        public Vector2 heights=new Vector2(7,13);
        public Vector2 normalizedAltitude=new Vector2(.15f,.76f);
        [Range(0,45)] public float maximumSlope=26;
        [Min(10)] public float nearDistance=150;
        [Min(20)] public float farDistance=700;
        [Min(0)] public float shadowDistance=100;
        public Vector2 windDirection=new Vector2(.7f,.3f);
        [Range(0,.1f)] public float windStrength=.025f;
        [Range(1,4)] public int buildsPerFrame=1;
        void OnValidate()
        {
            spacing=Mathf.Max(6,spacing);farDistance=Mathf.Max(nearDistance+20,farDistance);
            heights.x=Mathf.Max(2,heights.x);heights.y=Mathf.Max(heights.x,heights.y);
        }
    }
}
