# Project Leviathan: Artilharia

Jogo de guerra dieselpunk/steampunk em Unity 6 (6000.6.3f1, URP, C#). Esta é a **versão 2**: projeto novo e limpo,
com foco em uma artilharia pesada funcional e em um terreno procedural de 20 km.
O projeto anterior (terreno por chunks gerados sob demanda) ficou no repositório `Leviathan`, como arquivo de referência.

## Estado atual

Estados usados: **Planejado**, **Em desenvolvimento**, **Implementado** (código escrito e integrado), **Validado** (testado na Unity com resultado satisfatório), **Bloqueado**.
Nada vira Validado sem o desenvolvedor testar na Unity.

| Fase | O que é | Estado |
| --- | --- | --- |
| 0 | Fundação: projeto limpo, assemblies, testes | Em desenvolvimento (falta abrir na Unity e conferir o Console) |
| 1 | Altura base determinística + pré-visualização mínima | Implementado, **não validado** |
| 2 | Protótipo isolado do filtro de erosão por gradiente | Planejado |
| 3 | Terrain Baker completo (exportar `.lvh`) | Planejado |
| 4 | Chunks, LOD e malha | Planejado |
| 5 | Consulta de altura e impacto de projétil | Planejado |
| 6 | Integração: caminhada e artilharia | Planejado |

## Escala do terreno

- 1 unidade da Unity = 1 metro.
- Chunk de 1200 m, 120 células, 121 x 121 vértices no LOD 0 (10 m entre vértices).
- Mapa de 17 x 17 chunks = **20.400 m** por lado (domínio). A área de missão de 20.000 m é um limite operacional configurável dentro dele.
- A altura vem de **uma única função**, `TerrainHeightSampler.Sample(x, z)`. Mesma semente e mesmos parâmetros, mesmo terreno.

## Estrutura

| Pasta | Conteúdo |
| --- | --- |
| `Assets/Scripts/Terrain/Core` | Cálculo puro (sem Unity API): ruído, altura, estatísticas. Assembly `Leviathan.Terrain.Core` |
| `Assets/Scripts/Terrain/Runtime` | `TerrainSettings` (ScriptableObject). Assembly `Leviathan.Terrain` |
| `Assets/Editor/Terrain` | Janela `Tools > Leviathan > Terrain Preview`. Assembly `Leviathan.Terrain.Editor` |
| `Assets/Tests/EditMode/Terrain` | Testes de edição. Assembly `Leviathan.Terrain.Tests` |
| `docs` | Guia de cada fase e registro de procedência (`PROVENANCE.md`) |

## Como testar a fase atual

Passo a passo em [`docs/FASE-1.md`](docs/FASE-1.md).

## Regras de trabalho

- Uma etapa por vez; cada etapa é validada antes da próxima.
- Cálculo (Core), lógica de jogo (Runtime) e apresentação (Editor, malha, material) ficam separados.
- Sem dependências externas sem justificativa. Sem copiar código de terceiros (ver `docs/PROVENANCE.md`).
- Fora de escopo agora: Leviathans, multiplayer, IA complexa, economia, balística avançada, clima avançado, destruição de terreno.
