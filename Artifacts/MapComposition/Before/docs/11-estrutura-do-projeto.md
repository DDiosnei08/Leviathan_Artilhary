# 11 - Estrutura do projeto

> Registro da reorganizacao inicial. A cena integrada atual, a remocao das cenas antigas
> e os scripts da base giratoria estao descritos no [documento 14](14-base-giratoria.md).

## Objetivo

Separar jogo, prototipos, ferramentas do editor, recursos visuais e template, sem
renomear classes ou namespaces nem alterar os GUIDs dos assets existentes.
A organizacao nao implementa novas mecanicas e nao reconstruiu as cenas.

## Pastas

```text
Assets/
  Scripts/
    Gameplay/
      Artillery/             controle, volantes e instrumentos
      CommandRoom/           interacoes e alavancas
      Player/                controlador do operador
      TerrainPlacer.cs       assentamento de objetos
    World/
      ProceduralTerrain/     dados, geracao, chunks, malhas e depuracao
      WorldWeatherProvider.cs
    Prototypes/              montadores de cenas e coordenadores das demos
  Editor/
    SceneBuilders/           montagem e verificacao das demos
    Terrain/                 Inspector e janela do terreno
    ProjectOrganizationValidator.cs
  Scenes/
    Demos/                   base integrada e demonstracao cartum
    Tests/
      Test_ArtilleryControls/
      Test_ControllerPlayer/
    Templates/               SampleScene original
  Models/Artillery/Gauges/    modelo FBX dos instrumentos
  Materials/
    Base/                    concreto, metal, sacos, sinalizacao e suprimentos
    Terrain/                 material cartum do terreno
    Water/                   material cartum da agua
    Legacy/                  MeshMaterial preservado
  Shaders/Environment/        shaders de terreno/agua e HLSL compartilhado
  Settings/
    Terrain/                 presets do terreno e demonstracoes
    Rendering/               pipelines, renderers e configuracao global URP
    Volumes/                 perfis de pos-processamento
    Input/                   InputSystem_Actions.inputactions
  Samples/UnityTemplate/     tutorial, estilos, icone e Readme do template
  Documentation/             guias do prototipo
docs/                        conceito, decisoes, implementacao e referencias
Artifacts/
  Integration/Captures/      imagens da integracao original
  Integration/Logs/          verificacoes anteriores da integracao
  Shaders/Captures/           imagens da demonstracao cartum
  Shaders/Logs/               verificacoes anteriores dos shaders
  Organization/              inventario de GUIDs e validacao desta reorganizacao
```

## Cenas

| Cena | Uso |
| --- | --- |
| [Base_Cartum_Terreno_Agua](../Assets/Scenes/Demos/Base_Cartum_Terreno_Agua.unity) | Melhor ponto de entrada para ver o prototipo visual |
| [Base_Provisoria_Integrada](../Assets/Scenes/Demos/Base_Provisoria_Integrada.unity) | Cena de integracao preservada |
| [Test_ArtilleryControls](../Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity) | Controles e instrumentos isolados |
| [Test_controlsPlayer](../Assets/Scenes/Tests/Test_ControllerPlayer/Test_controlsPlayer.unity) | Jogador e sala de comando isolados |
| [SampleScene](../Assets/Scenes/Templates/SampleScene.unity) | Cena original do template, preservada |

A ordem e a ativacao das cenas em Build Settings foram mantidas: somente
SampleScene continua habilitada. Seu caminho foi atualizado, sem mudar seu GUID.
A demonstracao recomendada deve ser aberta diretamente pelo editor.

## Regras para novos arquivos

- Codigo executado durante Play vai em `Scripts`, agrupado pelo sistema.
- Ferramentas com dependencia de `UnityEditor` vao em `Editor`.
- Os dois montadores MonoBehaviour continuam em `Scripts/Prototypes`, pois sao
  componentes serializados das cenas de teste. Nao move-los para a assembly Editor.
- Demonstracoes completas vao em `Scenes/Demos`; testes isolados em `Scenes/Tests`.
- Modelos importados vao em `Models`; os objetos provisorios da base e do canhao
  continuam nas cenas, sem extracao artificial de prefabs ou modelos.
- Materiais e presets ficam separados por funcao. HLSL compartilhado fica junto
  aos shaders que o incluem, preservando os includes relativos.
- Documentacao de decisoes fica em `docs`; capturas e logs ficam em `Artifacts`.
- Use a janela Project para mover assets; preserve `.meta`, classes e namespaces.
- Nao versionar caches (`Library`, `Temp`, `Logs`) nem editar projetos C# gerados.

## Rastreabilidade e verificacao

[asset-moves.json](../Artifacts/Organization/asset-moves.json) registra os caminhos
antigos/novos, GUIDs e hashes anteriores dos 106 assets e pastas existentes.
Os caminhos literais dos montadores, configuracoes e documentos foram atualizados.
As cenas, materiais e presets mantiveram seus dados e referencias serializadas.

O menu `Tools > Leviathan > Validation > Check Organized Project` verifica as cinco
cenas, componentes ausentes, tipos dos scripts runtime, materiais, modelo e Build
Settings. O modo batch `ProjectOrganizationValidator.ValidateAndPlayBatch` acrescenta
a verificacao da demonstracao durante Play (canhao, jogador, cores, agua e regeneracao).

Validacao concluida em 2026-10-13, Unity 6000.6.3f1 / URP 17.6.0, numa copia isolada:
cinco cenas sem componentes ausentes, compilacao aprovada, materiais/modelo/Build
Settings encontrados e teste em Play aprovado. Os 106 GUIDs originais foram preservados;
os hashes confirmaram que as cinco cenas, o FBX, materiais e presets nao foram alterados.
O verificador de capturas agora aguarda um quadro valido da agua ao iniciar com cache
grafico frio; isso nao altera o shader nem a cena. A Unity apresentou uma excecao interna
de indexacao de Search no primeiro carregamento, sem erro dos sistemas do jogo.

Veja [registro da validacao](../Artifacts/Organization/README.md) para logs e capturas.

Limites preservados: ainda nao ha sistema de tiro ou mapa interativo; a cena integrada
original mantem o posicionamento anterior da base. A realocacao vertical para a seed
atual pertence somente a demonstracao cartum. A reorganizacao nao altera essas escolhas.
