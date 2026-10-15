#ifndef LEVIATHAN_GRASS_DATA
#define LEVIATHAN_GRASS_DATA
// 32 bytes: local root, half height/width, oct normal, half yaw/bend, RGBA8, stable hash.
struct GrassBlade
{
    float3 position;
    uint shape;
    uint normal;
    uint directionBend;
    uint color;
    uint seed;
};
uint GrassHash(uint v)
{
    uint state = v * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}
float GrassRandom(uint v) { return (GrassHash(v) & 0xffffffu) / 16777216.0; }
uint PackGrassHalf2(float2 v) { return f32tof16(v.x) | (f32tof16(v.y) << 16); }
float2 UnpackGrassHalf2(uint v) { return float2(f16tof32(v & 65535u), f16tof32(v >> 16)); }
uint PackGrassNormal(float3 n)
{
    n /= abs(n.x) + abs(n.y) + abs(n.z);
    float2 p = n.xz;
    if (n.y < 0) p = (1 - abs(p.yx)) * (p >= 0 ? 1 : -1);
    uint2 u = (uint2)round(saturate(p * 0.5 + 0.5) * 65535.0);
    return u.x | (u.y << 16);
}
float3 UnpackGrassNormal(uint packed)
{
    float2 p = float2(packed & 65535u, packed >> 16) / 65535.0 * 2 - 1;
    float3 n = float3(p.x, 1 - abs(p.x) - abs(p.y), p.y);
    if (n.y < 0) n.xz = (1 - abs(n.zx)) * (n.xz >= 0 ? 1 : -1);
    return normalize(n);
}
uint PackGrassColor(float3 color)
{
    uint3 c = (uint3)round(saturate(color) * 255);
    return c.x | (c.y << 8) | (c.z << 16);
}
float3 UnpackGrassColor(uint c) { return float3(c & 255u, (c >> 8) & 255u, (c >> 16) & 255u) / 255.0; }
#endif
