# Guia rapido do Leviathan2

## Abrir o prototipo

1. Abra `Assets/Scenes/Demos/Base_Cartum_Terreno_Agua.unity`.
2. Entre em Play para ver a base, o canhao e os materiais cartum.
3. Consulte `BASE-PROVISORIA-INTEGRADA.md` para os controles do jogador e artilharia.
4. Consulte `../../docs/10-shaders-terreno-e-agua.md` para as vistas da demonstracao e os parametros visuais.

## O que existe

- Terreno procedural, biomas, chunks, lagos, rios e props de blockout.
- Shaders cartum de terreno e agua, com sombras, neblina e animacao.
- Base provisoria, sala de comando e jogador.
- Um canhao com ajuste de azimute/elevacao, volantes e instrumentos.
- Dados de clima consultaveis por codigo, sem efeitos nos disparos.

Disparo, balistica, mapa interativo, triangulacao, calculadoras e relatos ainda nao
estao implementados neste projeto. O documento anterior incluia instrucoes herdadas
de outro prototipo; os caminhos e o estado acima correspondem ao Leviathan2 atual.

## Onde ajustar

| Recurso | Local |
| --- | --- |
| Presets de terreno | `Assets/Settings/Terrain` |
| Material do terreno | `Assets/Materials/Terrain/TerrainCartoon.mat` |
| Material da agua | `Assets/Materials/Water/WaterCartoon.mat` |
| Render pipeline | `Assets/Settings/Rendering` |
| Modelo dos instrumentos | `Assets/Models/Artillery/Gauges/ArtilleryGaugeSet.fbx` |
| Controles e interacoes | `Assets/Scripts/Gameplay` |
| Geracao do mundo | `Assets/Scripts/World/ProceduralTerrain` |
| Ferramentas de montagem | `Assets/Editor/SceneBuilders` |

Nao altere o preset de outra cena por engano: a demonstracao cartum usa
`CartoonShaderDemo.asset`, a base integrada usa `BaseProvisoriaTerrain.asset`.

## Validar

O menu `Tools > Leviathan > Validation > Check Organized Project` confere os assets,
scripts, cenas e Build Settings, retornando a cena anterior ao terminar.
Em Play, confira volantes, ponteiros, movimentacao do jogador, cores e agua animada.

O roteiro e a estrutura completa estao em `../../docs/11-estrutura-do-projeto.md`.
Nao mover arquivos sem seus `.meta`; prefira a janela Project da Unity.
