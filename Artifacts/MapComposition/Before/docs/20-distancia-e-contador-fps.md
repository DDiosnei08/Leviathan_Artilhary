# Distancia de renderizacao e FPS de teste

## Alcances da cena integrada

| Sistema | Antes | Agora | Asset |
| --- | --- | --- | --- |
| Grama | 120 m | 240 m | `Assets/Settings/Grass/BaseGiratoriaGrass.asset` |
| Arvores | 700 m | 1.400 m | `Assets/Settings/Vegetation/ProceduralTrees.asset` |
| Pedras, postes e props | 500 m | 900 m | `Assets/Settings/Vegetation/WorldScenery.asset` |
| Terreno no Play | Janela 3 x 3 | Janela 5 x 5 | `Assets/Settings/Terrain/BaseGiratoriaTerrain.asset` |

A grama manteve a densidade e os LODs existentes; a transicao passou de 12 para
20 m. O limite de patches residentes passou de 80 para 320 para nao cortar a
cobertura pelo limite de buffers. Sombras proximas nao tiveram seu alcance
aumentado. A margem de grama da base continua independente de arvores/props.
O streaming agora rejeita patches inteiramente fora do alcance antes de reservar
buffers: os cantos da janela quadrada nao sao mais gerados/liberados repetidamente
nem consomem o orcamento que deveria preencher os chunks vizinhos.

O terreno usa chunks de 1.200 m: a janela carregada no Play agora tem 6 km de
largura, respeitando os limites do mapa. O preview do Editor continua em 3 x 3
para manter a edicao leve. As cameras ja tinham far clip de 6 km e foram
preservadas. A neblina continua ocultando progressivamente o horizonte distante.
Semente, relevo e posicao da base nao foram alterados.

## Contador

`Assets/Scripts/Diagnostics/TestFpsCounter.cs` foi ligado a `Operacao da Base`
na cena `Assets/Scenes/Leviathan_BaseGiratoria.unity`.

- Exibe FPS medio e tempo medio de frame em ms no canto superior esquerdo.
- Atualiza o texto a cada 0,5 s usando tempo nao escalado; tambem funciona com
  `Time.timeScale = 0`.
- F8 oculta/exibe o contador, com o foco na Game view/janela do jogo.
- O Inspector possui `Visible`, `Development Only` e `Sample Interval`.
- Nao usa Canvas e nao interfere com os instrumentos fisicos ou troca de camera.
- `Development Only` vem ligado: um build normal desativa o componente. Para
  testar fora do Editor, use Development Build ou desmarque esse campo.
- O texto e armazenado entre amostras, evitando formatacao por frame/evento GUI;
  ha pequenas alocacoes ao atualizar o texto duas vezes por segundo.

## Testar e ajustar

Abra novamente a cena integrada e inicie Play. Espere a geracao dos chunks e da
grama estabilizar antes de comparar FPS; depois compare a visao geral e o posto
do canhao. O FPS do Editor inclui seu custo e nao equivale ao de um executavel.
O contador nao separa CPU/GPU nem mede o custo da grama isoladamente.

A grama ocupa aproximadamente 119 MiB em buffers com o novo pool, em vez de
aproximadamente 30 MiB. Mais terreno e objetos tambem aumentam o custo de CPU,
GPU e memoria. Se o teste ficar pesado, reduza primeiro `Far Distance` da grama
para 180 m e `Maximum Resident Patches` para cerca de 196. Mantenha a densidade
proxima e as distancias de sombra; reduzir apenas o pool pode criar falhas na
cobertura. Para outra distancia, reserve ao menos os patches da janela quadrada
que ela cobre, considerando `Patch Size` e os limites dos chunks.

## Validacao

`RenderDistanceFpsValidation.ValidateBatch` compila/testa a cena em uma copia
isolada e encerra o Editor ao concluir. Verifica parametros, chunks carregados,
instancias visiveis de grama alem do alcance antigo, FPS/ms, tempo pausado, F8,
troca de camera e regeneracao. Relatorios, capturas, backups e diff ficam em
`Artifacts/RenderDistanceFps`.

As capturas feitas via `Camera.Render` verificam o cenario, mas nao incluem o
overlay IMGUI do contador. Nao foi alterado VSync nem limite de FPS do projeto.

Validado em Unity 6000.6.3f1 / URP 17.6.0 / DX11 / RX 6600: 22 verificacoes de
distancia/FPS e 28 verificacoes de regressao da grama passaram. O culling enviou
instancias ate 239,99 m tanto na visao geral quanto no conves, mantendo o alcance
apos regenerar. A margem da base permaneceu igual e o LateUpdate da grama
continuou com zero GC em 30 amostras estabilizadas. A verificacao espera frames
de geracao apos os chunks ficarem prontos, respeitando o orcamento de streaming.
