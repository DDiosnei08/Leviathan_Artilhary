using System;
using UnityEngine;

namespace ProjectLeviathan.Base
{
    public interface IHeightSource { float GetBaseHeight(float x, float z); }
    public interface IBaseSiteSuitability { bool IsDry(float x,float z); }
    public enum BaseFacingMode { TowardsPoint, AwayFromPoint, Random }
    [Serializable]
    public sealed class BaseSiteSettings
    {
        public int seed = 12345;
        public Vector2 searchCenter = new Vector2(600, 600);
        public float minDistance = 150, maxDistance = 700, preferredDistance = 300;
        [Range(16, 2000)] public int candidates = 600;
        public float waterLevel, minAboveWater = 4.5f, maxAltitude = 1000;
        public float checkRadius = 30, maxHeightRange = .25f, maxSlopeDeg = 2f;
        [Range(3, 15)] public int gridSteps = 9;
        public float prominenceRadius = 150, prominenceWeight = 0;
        public float flatRadius = 28, blendRadius = 0, carveRadius = 21.9f, carveRamp = .2f;
        public float pitDepth = 3.8f, exclusionRadius = 36;
        public BaseFacingMode facingMode = BaseFacingMode.TowardsPoint;
        public Vector2 facePoint;
        public bool constrainToMap;
        public Vector2 minimumWorldPosition, maximumWorldPosition;
    }
    [Serializable]
    public struct BaseSiteData
    {
        public bool valid;
        public int seed;
        public Vector2 center;
        public float groundHeight, yaw, score;
        public float flatRadius, blendRadius, carveRadius, carveRamp, pitDepth, exclusionRadius;
        public float InfluenceRadius => Mathf.Max(flatRadius,carveRadius);
        public bool IsExcluded(float x, float z, float margin = 0)
        { float r = exclusionRadius + margin; return valid && (new Vector2(x,z) - center).sqrMagnitude <= r * r; }
        public float ModifyHeight(float x, float z, float natural)
        {
            // Site placement never raises, flattens or otherwise changes the natural height field.
            // The imported pit uses a mesh cutout instead of a radial earthworks deformation.
            return natural;
        }
    }
    public static class BaseSiteRegistry
    {
        public static BaseSiteData Site { get; private set; }
        public static bool HasSite => Site.valid;
        public static event Action<BaseSiteData> OnSiteChanged;
        static object owner;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Site = default; owner = null; OnSiteChanged = null; }
        public static bool Set(BaseSiteData data, object caller)
        {
            if (owner != null && !ReferenceEquals(owner,caller)) return false;
            owner = caller; Site = data; OnSiteChanged?.Invoke(Site); return true;
        }
        public static void Clear(object caller)
        { if (!ReferenceEquals(owner,caller)) return; owner = null; Site = default; OnSiteChanged?.Invoke(Site); }
        public static float ModifyHeight(float x,float z,float natural) => Site.ModifyHeight(x,z,natural);
        public static bool IsExcluded(float x,float z,float margin = 0) => Site.IsExcluded(x,z,margin);
    }
    public static class BaseSiteSolver
    {
        public static BaseSiteData Solve(IHeightSource source, BaseSiteSettings s)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var suitability=source as IBaseSiteSuitability;
            var random = new System.Random(s.seed);
            var candidates = new Vector2[Mathf.Max(1,s.candidates)];
            int n = Mathf.Clamp(s.gridSteps | 1,3,15);
            var heights = new float[n,n]; var inside = new bool[n,n];
            float minimum = Mathf.Max(0,s.minDistance), maximum = Mathf.Max(minimum+1,s.maxDistance);
            for (int i = 0; i < candidates.Length; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float r = Mathf.Sqrt(Mathf.Lerp(minimum*minimum,maximum*maximum,(float)random.NextDouble()));
                candidates[i] = s.searchCenter + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
            }
            BaseSiteData best = default;
            float[] relaxation = {1};
            foreach (float relax in relaxation)
            {
                float bestScore = float.PositiveInfinity;
                foreach (Vector2 c in candidates)
                {
                    float influence = Mathf.Max(s.checkRadius,s.flatRadius+s.blendRadius);
                    if(s.constrainToMap && (c.x-influence<s.minimumWorldPosition.x || c.y-influence<s.minimumWorldPosition.y ||
                        c.x+influence>s.maximumWorldPosition.x || c.y+influence>s.maximumWorldPosition.y)) continue;
                    float radius = Mathf.Max(s.checkRadius,s.flatRadius);
                    float step = 2 * radius / (n-1);
                    float low = float.PositiveInfinity, high = float.NegativeInfinity, sum = 0;
                    int count = 0;
                    bool dry=true;
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        Vector2 p = c + new Vector2(-radius+x*step,-radius+y*step);
                        float d = Vector2.Distance(p,c); inside[x,y] = d <= radius;
                        if (!inside[x,y]) continue;
                        if(suitability!=null && !suitability.IsDry(p.x,p.y)) dry=false;
                        float h = source.GetBaseHeight(p.x,p.y); heights[x,y] = h;
                        low = Mathf.Min(low,h); high = Mathf.Max(high,h);
                        if (d <= s.flatRadius) { sum += h; count++; }
                    }
                    // Water and altitude remain hard constraints, even in the earthworks fallback.
                    if (!dry || !float.IsFinite(low) || high > s.maxAltitude || low < s.waterLevel + Mathf.Max(s.minAboveWater,s.pitDepth+0.5f)) continue;
                    float slope = 0;
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    {
                        if (!inside[x,y]) continue;
                        if (x+1<n && inside[x+1,y]) slope = Mathf.Max(slope,Mathf.Atan(Mathf.Abs(heights[x,y]-heights[x+1,y])/step)*Mathf.Rad2Deg);
                        if (y+1<n && inside[x,y+1]) slope = Mathf.Max(slope,Mathf.Atan(Mathf.Abs(heights[x,y]-heights[x,y+1])/step)*Mathf.Rad2Deg);
                    }
                    float range = high-low;
                    if (range > s.maxHeightRange*relax || slope > s.maxSlopeDeg*relax) continue;
                    // Verify the support annulus and interior independently of the coarse grid.
                    // This rejects small ridges between samples instead of leaving the rim floating.
                    bool supported=true;
                    for(int ringIndex=1;ringIndex<=4 && supported;ringIndex++) for(int q=0;q<64;q++)
                    {
                        float a=q*Mathf.PI/32, r=radius*ringIndex/4;
                        if(suitability!=null && !suitability.IsDry(c.x+Mathf.Cos(a)*r,c.y+Mathf.Sin(a)*r)) {supported=false;break;}
                        float h=source.GetBaseHeight(c.x+Mathf.Cos(a)*r,c.y+Mathf.Sin(a)*r);
                        if(!float.IsFinite(h)) {supported=false;break;}
                        low=Mathf.Min(low,h);high=Mathf.Max(high,h);
                        if(high-low>s.maxHeightRange || low<s.waterLevel+Mathf.Max(s.minAboveWater,s.pitDepth+.5f)) {supported=false;break;}
                    }
                    if(!supported) continue;
                    range=high-low;
                    float ground = high + .02f;
                    float ring = 0;
                    for (int q = 0; q < 8; q++) { float a=q*Mathf.PI/4; ring+=source.GetBaseHeight(c.x+Mathf.Cos(a)*s.prominenceRadius,c.y+Mathf.Sin(a)*s.prominenceRadius); }
                    float score=range/Mathf.Max(.01f,s.maxHeightRange)+.5f*slope/Mathf.Max(.01f,s.maxSlopeDeg)+
                        .4f*Mathf.Abs(Vector2.Distance(c,s.searchCenter)-s.preferredDistance)/Mathf.Max(1,s.preferredDistance)-s.prominenceWeight*Mathf.Clamp((ground-ring/8)/20,-1,1);
                    if (score>=bestScore) continue;
                    bestScore=score;
                    best = new BaseSiteData {valid=true,seed=s.seed,center=c,groundHeight=ground,score=score,
                        flatRadius=s.flatRadius,blendRadius=s.blendRadius,carveRadius=s.carveRadius,carveRamp=s.carveRamp,pitDepth=s.pitDepth,exclusionRadius=s.exclusionRadius};
                }
                if (best.valid) break;
            }
            Vector2 direction=s.facingMode==BaseFacingMode.AwayFromPoint ? best.center-s.facePoint : s.facePoint-best.center;
            best.yaw=s.facingMode==BaseFacingMode.Random ? (float)random.NextDouble()*360 : Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg;
            return best;
        }
    }
}
