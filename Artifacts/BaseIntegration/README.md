# Resultado da integracao

- Unity 6000.6.3f1 / URP 17.6.0 / DX11 / RX 6600.
- Cena final: `Assets/Scenes/Leviathan_BaseGiratoria.unity`.
- Prefab: `Assets/Prefabs/BaseGiratoria.prefab`.
- Auditoria: duas cenas, sem componentes faltantes, shaders ou referencias de Build Settings invalidos.
- Play: 61 verificacoes aprovadas. Veja `PlayValidation.txt` e `Logs/FinalAuditPlay.log`.
- Capturas: `Captures/01_Base_No_Terreno.png`, `02_Instrumentos_Canhao.png`, `03_Mapa_Fisico.png`, `04_Base_Apos_Regeneracao.png`.
- Cenas anteriores e `.meta`: `ArchivedScenes`; versao resalva durante o trabalho: `ArchivedScenes_Resaved`.
- FBX original e copia importada: SHA256 `A04685DDD0700229BABA227D716F35B0AB13B1F28E4A8478E79C5A71EF5A016D`.
- Teste de artilharia preservado: SHA256 `2903D3B6C5F59E0A247D572858D6F814C6CC64455487C142FEC8D8417D4E980D`.
- O erro conhecido de indexacao de UnityEditor.Search em batchmode foi separado dos erros de gameplay.
- A meta de 2 ms de GPU para a grama nao foi medida novamente para esta cena.

## Integracao minima no terreno

`TerrainSettings` oferece um site runtime nao serializado, copiado para `TerrainGenerationSettings`.
`HeightMapGenerator` conforma alturas em metros e preserva os valores normalizados/biomas.
`TerrainChunk` e `ThreadedTerrainGenerator` usam `BaseTerrainMesh`, com refinamento local do fosso.
`MeshGenerator` aceita alturas/normais mundiais opcionais; `MeshData` seleciona UInt32 quando necessario.
Props, navegacao/POIs e a mascara da grama excluem a area da base.
O restante dos materiais, presets e da geracao original foi preservado.
