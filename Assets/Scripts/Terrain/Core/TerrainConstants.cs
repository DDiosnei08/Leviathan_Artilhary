namespace Leviathan.Terrain
{
    /// <summary>
    /// Escala fixa do terreno (convenção do projeto: 1 unidade da Unity = 1 metro).
    /// O mapa é uma grade regular de chunks; o domínio é 17 x 1200 m = 20.400 m por lado.
    /// A área de missão (20.000 m) é um limite operacional configurável DENTRO do domínio.
    /// </summary>
    public static class TerrainConstants
    {
        public const float ChunkSizeMeters = 1200f;
        public const int ChunkCells = 120;
        public const int ChunkVertices = ChunkCells + 1;          // 121 vértices por lado no LOD 0
        public const float VertexSpacingMeters = ChunkSizeMeters / ChunkCells; // 10 m
        public const int ChunksPerSide = 17;
        public const float DomainSizeMeters = ChunksPerSide * ChunkSizeMeters; // 20.400 m
        public const float DefaultMissionAreaMeters = 20000f;

        /// <summary>Inclinação (graus) abaixo da qual a área conta como plana nas estatísticas.</summary>
        public const float FlatMaxDegrees = 3f;

        /// <summary>Inclinação (graus) acima da qual a área conta como íngreme nas estatísticas.</summary>
        public const float SteepMinDegrees = 30f;
    }
}
