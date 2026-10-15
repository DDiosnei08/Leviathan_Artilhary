using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    public static class FalloffGenerator
    {
        public static float[,] GenerateFalloffMap(int width, int height, float a = 3f, float b = 2.2f)
        {
            float[,] map = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)Mathf.Max(1, width - 1) * 2f - 1f;
                    float ny = y / (float)Mathf.Max(1, height - 1) * 2f - 1f;
                    float value = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                    float numerator = Mathf.Pow(value, a);
                    float denominator = numerator + Mathf.Pow(b - b * value, a);

                    if (denominator <= 0f)
                    {
                        map[x, y] = 0f;
                    }
                    else
                    {
                        map[x, y] = numerator / denominator;
                    }
                }
            }

            return map;
        }
    }
}
