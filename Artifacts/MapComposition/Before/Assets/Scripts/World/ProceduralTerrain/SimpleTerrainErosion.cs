using System;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    [Serializable]
    public sealed class TerrainErosionSettings
    {
        public bool enabled;
        [Min(10)] public float channelScale=180;
        [Range(0,20)] public float channelDepth=5;
        [Range(.01f,.5f)] public float channelWidth=.16f;
        [Range(0,3)] public int thermalIterations=1;
        [Range(0,1)] public float thermalStrength=.2f;
        [Range(10,60)] public float talusDegrees=35;
        public TerrainErosionSettings Copy() => (TerrainErosionSettings)MemberwiseClone();
        public void Validate()
        {
            channelScale=Mathf.Max(10,channelScale);channelDepth=Mathf.Clamp(channelDepth,0,20);
            channelWidth=Mathf.Clamp(channelWidth,.01f,.5f);thermalIterations=Mathf.Clamp(thermalIterations,0,3);
            thermalStrength=Mathf.Clamp01(thermalStrength);talusDegrees=Mathf.Clamp(talusDegrees,10,60);
        }
    }
    public static class SimpleTerrainErosion
    {
        /// <summary>World-space gully incision and bounded thermal relaxation. Not hydraulic erosion.</summary>
        public static float[,] Apply(float[,] source,float[,] plains,TerrainErosionSettings e,Vector2 origin,Vector2 step,int seed)
        {
            if(e==null || !e.enabled) return source;
            int w=source.GetLength(0),h=source.GetLength(1);
            var channels=Noise.GenerateNoiseMap(w,h,e.channelScale,3,.55f,2,unchecked(seed+91471),origin,step,true);
            float cell=Mathf.Abs(step.x),talus=Mathf.Tan(e.talusDegrees*Mathf.Deg2Rad)*cell;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                float ridge=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Abs(channels[x,y]-.5f)/e.channelWidth));
                float rough=1-plains[x,y];
                source[x,y]-=ridge*ridge*e.channelDepth*rough*rough;
            }
            // A synchronous, symmetric exchange keeps mass in the interior. The caller supplies
            // a halo wider than the number of iterations so adjacent chunks compute identical edges.
            for(int pass=0;pass<e.thermalIterations;pass++)
            {
                var next=(float[,])source.Clone();
                for(int y=1;y<h-1;y++) for(int x=1;x<w-1;x++)
                {
                    float delta=0;
                    for(int j=-1;j<=1;j++) for(int i=-1;i<=1;i++)
                    {
                        if(i==0 && j==0) continue;
                        float difference=source[x+i,y+j]-source[x,y];
                        float threshold=talus*(i!=0 && j!=0 ? 1.41421356f : 1);
                        float mobility=(1-Mathf.Max(plains[x,y],plains[x+i,y+j]));
                        delta+=Mathf.Sign(difference)*Mathf.Max(0,Mathf.Abs(difference)-threshold)*mobility*e.thermalStrength*.0625f;
                    }
                    next[x,y]+=delta;
                }
                source=next;
            }
            return source;
        }
    }
}
