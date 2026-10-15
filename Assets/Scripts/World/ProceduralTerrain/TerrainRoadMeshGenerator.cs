using System.Collections.Generic;
using ProjectLeviathan.Splines;
using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class TerrainRoadMeshGenerator
    {
        public static Mesh Generate(HeightMap map, TerrainSettings settings, int appliedLod = 0)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));
            if (settings == null) throw new System.ArgumentNullException(nameof(settings));
            var builder = new SurfaceSplineBuilder();
            int stride = 1;
            foreach (var lod in settings.lods) if (lod.lod == appliedLod) stride = lod.GetSimplificationLevel();
            foreach (var path in ExtractPaths(map, settings))
            {
                if (path.Count < 2) continue;
                var knots = new List<SurfaceSplineKnot>(path.Count);
                foreach (Vector3 p in path) knots.Add(new SurfaceSplineKnot(p, Vector3.up));
                builder.Append(knots, false, Mathf.Min(settings.roadWidthMeters, settings.CellSize * .85f),
                    Mathf.Max(1, settings.CellSize * .3f), 6, .45f, Mathf.Max(.04f, settings.roadSurfaceLift), 6,
                    .5f, Project);
            }
            return builder.triangles.Count == 0 ? null : builder.CreateMesh(Matrix4x4.identity, "Projected Terrain Roads");

            bool Project(Vector3 seed, Vector3 hint, out Vector3 p, out Vector3 n) =>
                SampleSurface(map, settings, stride, seed, out p, out n);
        }

        // Trace graph edges once; diagonals are allowed only without an orthogonal connection.
        public static List<List<Vector3>> ExtractPaths(HeightMap map, TerrainSettings settings)
        {
            int w = map.Width, h = map.Height;
            var neighbors = new List<int>[w*h];
            bool Road(int x, int y) => x >= 0 && y >= 0 && x < w && y < h &&
                map.Roads[x,y] > .5f && map.Water[x,y] <= .01f && map.Navigation[x,y] > 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (Road(x,y))
            {
                var list = neighbors[y*w+x] = new List<int>(4);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && Road(x+dx,y+dy) &&
                        !(dx != 0 && dy != 0 && (Road(x+dx,y) || Road(x,y+dy)))) list.Add((y+dy)*w+x+dx);
            }
            var visited = new HashSet<long>(); var paths = new List<List<Vector3>>();
            long Edge(int a, int b) => ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
            Vector3 Point(int i) => new Vector3((i%w)*settings.CellSize, 0, settings.chunkSize-(i/w)*settings.CellSize);
            for (int pass = 0; pass < 2; pass++) for (int i = 0; i < neighbors.Length; i++)
            {
                var links = neighbors[i];
                if (links == null || (pass == 0 && links.Count == 2)) continue;
                foreach (int link in links)
                {
                    if (visited.Contains(Edge(i,link))) continue;
                    var path = new List<Vector3> { Point(i) }; int previous = i, current = link;
                    while (visited.Add(Edge(previous,current)))
                    {
                        path.Add(Point(current)); var next = neighbors[current];
                        if (current == i || next.Count != 2) break;
                        int following = next[0] == previous ? next[1] : next[0]; previous = current; current = following;
                    }
                    paths.Add(path);
                }
            }
            return paths;
        }

        public static bool SampleSurface(HeightMap map, TerrainSettings settings, int stride,
            Vector3 seed, out Vector3 point, out Vector3 normal)
        {
            point = seed; normal = Vector3.up;
            const float edgeTolerance = .001f;
            if (seed.x < -edgeTolerance || seed.z < -edgeTolerance || seed.x > settings.chunkSize+edgeTolerance || seed.z > settings.chunkSize+edgeTolerance) return false;
            seed.x = Mathf.Clamp(seed.x,0,settings.chunkSize); seed.z = Mathf.Clamp(seed.z,0,settings.chunkSize); point = seed;
            float gx = seed.x / settings.CellSize, gy = (settings.chunkSize-seed.z) / settings.CellSize;
            int mx = Mathf.Clamp(Mathf.RoundToInt(gx),0,map.Width-1), my = Mathf.Clamp(Mathf.RoundToInt(gy),0,map.Height-1);
            var site = map.BaseSite;
            Vector2 world = map.WorldOrigin + new Vector2(seed.x,seed.z);
            if (map.Water[mx,my] > .01f || (site.valid && (world-site.center).sqrMagnitude < site.carveRadius*site.carveRadius)) return false;
            int x = Mathf.Min((int)(gx/stride)*stride,map.Width-2), y = Mathf.Min((int)(gy/stride)*stride,map.Height-2);
            int xx = Mathf.Min(x+stride,map.Width-1), yy = Mathf.Min(y+stride,map.Height-1);
            float u = (gx-x)/(xx-x), v = (gy-y)/(yy-y);
            float H(int a,int b) => map.WorldHeights != null ? map.WorldHeights[a,b] : settings.heightCurve.Evaluate(map.Values[a,b])*settings.heightMultiplier;
            float a = H(x,y), b = H(xx,y), c = H(x,yy), d = H(xx,yy);
            bool upper = u+v <= 1;
            point.y = upper ? a*(1-u-v)+b*u+c*v : d*(u+v-1)+b*(1-v)+c*(1-u);
            normal = new Vector3(-(upper ? b-a : d-c)/((xx-x)*settings.CellSize),1,
                (upper ? c-a : d-b)/((yy-y)*settings.CellSize)).normalized;
            return true;
        }
    }
}
