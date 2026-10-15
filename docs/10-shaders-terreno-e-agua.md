# 10 - Shaders cartum do terreno e da agua

## Objetivo e versoes

Dar ao terreno procedural uma iluminacao cartum simples, preservando as cores dos biomas, e adicionar uma superficie de agua animada nas regioes indicadas pela geracao.

Projeto consultado: Unity `6000.6.3f1` e Universal Render Pipeline `17.6.0`. A implementacao usa ShaderLab/HLSL e os recursos de iluminacao da URP.

## Arquivos principais

| Arquivo | Papel |
| --- | --- |
| [CartoonTerrain.shader](../Assets/Shaders/Environment/CartoonTerrain.shader) | Terreno, cores dos biomas, faixas de luz, sombra e neblina |
| [CartoonWater.shader](../Assets/Shaders/Environment/CartoonWater.shader) | Agua, ondas pequenas, padrao celular animado e espuma |
| [CartoonCommon.hlsl](../Assets/Shaders/Environment/CartoonCommon.hlsl) | Iluminacao cartum e funcoes compartilhadas da URP |
| [TerrainCartoon.mat](../Assets/Materials/Terrain/TerrainCartoon.mat) | Parametros visuais do terreno |
| [WaterCartoon.mat](../Assets/Materials/Water/WaterCartoon.mat) | Parametros visuais da agua |
| [TerrainWaterMeshGenerator.cs](../Assets/Scripts/World/ProceduralTerrain/TerrainWaterMeshGenerator.cs) | Geometria recortada a partir das mascaras de lagos e rios |
| [CartoonShaderShowcase.cs](../Assets/Scripts/Prototypes/CartoonShaderShowcase.cs) | Alternancia entre a base e a vista de agua na demonstracao |
| [CartoonShaderSceneBuilder.cs](../Assets/Editor/SceneBuilders/CartoonShaderSceneBuilder.cs) | Criacao da demonstracao e verificacoes |

## Integracao

Os assets `BaseProvisoriaTerrain` e `LeviathanTerrain` recebem os novos materiais. O override de material na cena integrada tambem usa `TerrainCartoon`.

`TerrainChunk` continua gerando uma textura de cores por chunk e atribuindo-a a `_BaseMap`. O shader utiliza as UVs existentes para essa textura; o detalhe adicional usa coordenadas de mundo.

A superficie de agua e um filho do chunk, com material compartilhado. O mesh e recriado quando os dados do terreno mudam e descartado ao regenerar ou descarregar o chunk. Mudancas apenas de clima nao recriam a geometria.

Lagos sao recortados no limite da altura configurada e possuem um nivel comum. Rios usam a mascara existente e acompanham a altura local do leito. As margens sao recortadas por triangulo; areas inteiramente secas nao geram superficies de agua.

O material recebe profundidade aproximada e informacao de margem pelas UVs do mesh. Isso permite espuma sem depender da textura de profundidade da camera. O movimento usa coordenadas de mundo e o mesmo tempo de animacao em todos os chunks.

## Cena de demonstracao

Abra `Assets/Scenes/Demos/Base_Cartum_Terreno_Agua.unity` e entre em Play.

- A primeira vista preserva a base, o canhao, o jogador e os instrumentos.
- F4 alterna entre a vista geral da base e uma margem de lago encontrada no mesmo mundo procedural.
- Tab e E continuam sendo os controles da base descritos no guia anterior.
- O componente `CartoonShaderShowcase` tambem possui os comandos de contexto `Show Water View` e `Show Base View`.

A demonstracao usa uma copia das configuracoes com mapa de 17 x 17 chunks e janela de 3 x 3 chunks. Isso permite procurar uma regiao de agua sem inventar um plano sobre areas secas nem trocar a semente do mundo. A base integrada original conserva sua configuracao de mapa menor.

A cena de demonstracao realinha a altura da base, a fundacao e a rampa ao relevo atual. A configuracao consultada ja possuia uma semente diferente daquela usada na primeira montagem da base; seus objetos e posicoes horizontais sao preservados.

Para recriar a demonstracao, use `Leviathan > Create Cartoon Shader Demo`. Esse comando monta uma nova versao a partir da cena integrada e substitui a cena de demonstracao existente.

## Parametros do terreno

Selecione `Assets/Materials/Terrain/TerrainCartoon.mat`.

| Parametro | Efeito |
| --- | --- |
| Tint | Cor multiplicada sobre as cores geradas |
| Color Intensity | Intensidade das cores |
| Shadow Tint | Tonalidade das regioes sombreadas |
| Shadow Contrast | Contraste das sombras e das faixas de iluminacao |
| Lighting Bands | Duas ou tres faixas |
| Band Softness | Suavidade das transicoes |
| World Detail Scale | Frequencia do detalhe em coordenadas de mundo |
| Detail Strength | Intensidade do detalhe sutil |

A textura `Biome Colors` e preenchida automaticamente nos materiais gerados por chunk. Para alterar os biomas, use as cores e a paleta no asset de configuracao do terreno.

Cada chunk possui uma copia do material do terreno para manter sua textura de biomas. Depois de editar os parametros do material principal, use `Regenerate Terrain` para atualizar essas copias e o preview.

## Parametros da agua

Selecione `Assets/Materials/Water/WaterCartoon.mat`.

| Parametro | Efeito |
| --- | --- |
| Shallow Color / Deep Color | Cores de agua rasa e profunda |
| Deep Color Depth | Profundidade aproximada que conduz a cor profunda |
| Surface Highlights | Cor dos padroes claros |
| Wave Height | Altura das ondulacoes, em metros |
| World Wave Scale / Wave Speed | Frequencia e velocidade das ondas |
| World Pattern Scale / Pattern Speed | Tamanho e movimento do padrao celular |
| Pattern Strength | Intensidade dos padroes claros |
| Foam Color / Foam Strength | Cor e intensidade da espuma |
| Shore Foam Depth | Faixa de profundidade para espuma em lagos |
| Detail Fade Distance | Distancia na qual os padroes finos deixam de aparecer |
| Shadow Tint / Shadow Contrast | Aparencia das sombras sobre a agua |

As ondas diminuem junto das margens e em agua muito rasa. O deslocamento e compartilhado pelos passes de cor, profundidade e normais.

No asset de terreno, o grupo `Water Surfaces` possui:

- `Generate Water Surfaces`: liga a geracao de agua.
- `Water Material`: material utilizado pela superficie.
- `Water Surface Offset`: pequeno afastamento vertical para evitar sobreposicao com o leito.
- `River Water Depth`: profundidade visual usada nos trechos de rio.

Alteracoes nesses parametros de geometria exigem `Regenerate Terrain`. Cores, velocidades e intensidades do material compartilhado da agua podem ser ajustadas durante Play.

## Limites desta implementacao

- Agua opaca e estilizada, sem refracao, reflexoes de cena ou simulacao de fluidos.
- Lagos usam o nivel comum definido pelo limiar de lago; bacias com niveis independentes nao sao simuladas.
- Rios acompanham a mascara e o leito existentes; nao foi introduzido um novo gerador hidrologico.
- A precisao da margem depende da resolucao dos dados de altura e agua. O recorte reduz o formato de blocos, mas nao cria detalhe hidrologico ausente nos dados.
- O shader recebe a luz principal, sombras e luz ambiente. Iluminacao cartum por luzes adicionais nao foi implementada.
- A agua nao adiciona collider, nado, dano ou alteracoes de jogabilidade.
- A geometria de agua usa a resolucao do mapa, mesmo quando o terreno troca de LOD. Um LOD separado de agua e uma melhoria futura para mapas muito carregados.

## Verificacoes

O editor de demonstracao inclui verificacoes de mapa seco, recorte da margem, mascara de rio, normais voltadas para cima, bordas entre chunks e desativacao da agua.

A verificacao em Play confere shader e textura de bioma, piso do jogador, controle do canhao, agua renderizada, diferenca entre dois quadros da animacao e regeneracao sem superficies duplicadas.

Os resultados e capturas da verificacao concluida ficam em `Artifacts`. Esses testes cobrem o renderer e a cena utilizados no projeto; nao substituem testes futuros em outras plataformas.

Verificacao executada no Unity com sucesso: compilacao/importacao dos shaders e scripts, testes de geometria, jogador sobre a fundacao, controle de elevacao, textura de biomas, agua animada e regeneracao. A comparacao considerou apenas pixels de agua presentes nos dois quadros, depois do carregamento por GPU.

Capturas: [base e terreno](../Artifacts/Shaders/Captures/CartoonBase.png), [agua - quadro A](../Artifacts/Shaders/Captures/CartoonWater_A.png), [agua - quadro B](../Artifacts/Shaders/Captures/CartoonWater_B.png).

Logs: [montagem e testes de geometria](../Artifacts/Shaders/Logs/shader-build.log), [verificacao em Play](../Artifacts/Shaders/Logs/shader-play.log).
