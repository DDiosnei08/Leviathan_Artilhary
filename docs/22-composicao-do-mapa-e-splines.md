# Composicao do mapa e splines projetadas

## Plano de execucao

1. **Faixas de superficie:** spline editavel, projecao de todos os vertices,
   subdivisoes longitudinal e transversal, UVs em metros e demonstracao em
   paisagem, esfera e cone. Reutilizar a construcao nas estradas procedurais.
2. **Solo e vegetacao:** manchas de densidade e altura da grama em coordenadas
   de mundo, clareiras, bosques com transicao gradual e solo mineral mais legivel.
3. **Relevo:** reforcar sulcos e afloramentos fora das planicies naturais.
   A base continua exigindo apoio plano e seco, sem pedestal artificial.
4. **Infraestrutura:** estradas continuas, acesso da base e referencias industriais
   construidas sobre os POIs existentes; postes, cabos e barreiras coerentes.
5. **Marcas e atmosfera:** desgaste nas vias, pequenos elementos de vegetacao,
   luzes de servico e atmosfera localizada com custo limitado.
6. **Validacao:** compilar em copia isolada do projeto, testar projecao/ausencia de
   superficie, regeneracao, streaming e LOD; capturar resultado e registrar limites.

Este plano acompanha o trabalho. Os resultados e limites verificados sao
registrados abaixo apos a implementacao; uma etapa listada nao e prova de conclusao.

## Implementacao

### 1. Faixas sobre superficies

`SurfaceSplineMesh` e `SurfaceSplineBuilder` geram uma faixa subdividida com
projecao direcional, radial ou pelas normais dos pontos. A demonstracao independente
fica em `Assets/Scenes/Tests/SurfaceSplineDemo.unity`. Uso detalhado no documento 23.
`TerrainRoadMeshGenerator` reutiliza o construtor para renderizar os caminhos da
mascara existente. A altura segue os triangulos do LOD aplicado.

### 2. Vegetacao e solo

- Grama com manchas de 38 m, variacao de densidade e altura em ruido continuo
  de mundo. Os limites de distancia e o pool GPU existentes foram mantidos.
- Bosques com transicao gradual, arvores menores nas bordas e menos exemplares
  isolados. Alturas configuradas entre 6 e 16 m antes da reducao nas bordas.
- Arbustos e madeira caida instanciados em manchas de bosque, usando as mesmas
  exclusoes de agua, caminhos, POIs e base. O total de props e limitado por chunk.
- Mais contraste entre solo seco, umido e afloramentos. A textura de biomas e
  suas UVs continuam sendo a fonte principal de cor.

### 3. Relevo e base

O preset principal usa canais de 13 m de profundidade maxima e escala de 105 m.
Esse efeito continua multiplicado pela mascara de relevo: planicies naturais
nao recebem os sulcos. A erosao e uma aproximacao visual, nao uma simulacao
hidraulica. A escolha da base continua exigindo terreno seco e apoio plano.
Nao foi acrescentado achatamento, montanha artificial nem pedestal para a base.

### 4. Infraestrutura

- No mundo com streaming, rotas conectam POIs e chegam ao perimetro da base
  usando a navegabilidade existente. A busca pode recusar caminhos inviaveis.
- As rotas contornam a area reservada das construcoes industriais.
- Postes acompanham os caminhos em intervalos aproximados de 65 m. Cabos possuem
  flecha e conectam pares validos com ate 110 m de separacao. Nao cruzam POIs.
- Tres prefabs de referencia: posto de radio/vigia, deposito e usina de bombeamento
  com reservatorios e chamine. A distribuicao e deterministica por semente.
  Nao e garantida uma instancia de cada tipo em toda janela carregada: depende
  das chances e dos locais adequados encontrados.
- A reserva dos POIs tambem afasta grama e arvores. As construcoes usam fundacao
  parcialmente enterrada e terrenos com inclinacao maxima de 2 graus.

### 5. Uso e atmosfera

As estradas tem marcas de rodas, cascalho e bordas irregulares em shader.
Os edificios usam pintura, aco, concreto e desgaste dos materiais dieselpunk.
Ha luminarias fisicas de servico na base e nos POIs. As luzes locais sao sem
sombras, limitadas por distancia e ativadas quando o ceu escurece.
Fumaca e bruma junto a usina usam poucos sprites procedurais: maximo de 40 e
12 particulas, respectivamente. Os efeitos param fora do raio configurado.
A luz ambiente noturna foi elevada para melhorar a leitura do terreno.

## Ajustes

| Asset/componente | Parametros principais |
| --- | --- |
| BaseGiratoriaGrass | Meadow Patch Size, Meadow Variation, Meadow Minimum Density, Height Range |
| ProceduralTrees | Grove Threshold, Grove Edge Blend, Heights, Scattered Density |
| WorldScenery | Prototypes, Rock Density, Maximum Instances Per Chunk |
| BaseGiratoriaTerrain | Erosion, Surface Palette, POI Definitions, Roadside meshes/materials |
| SplineTrack | Gravel, Wheel Tracks, Track Strength, Gravel Scale, Irregular Edge |
| LocalAtmosphere nos prefabs | Effects Distance e Light Distance |
| Ceu Procedural | Night Ambient Sky/Horizon e Moon Intensity |

`Leviathan > Environment > Aplicar composicao do mapa` reaplica estes valores
e reconstroi os prefabs industriais. Use esse comando apenas para voltar ao preset;
nao e necessario para jogar ou regenerar a cena ja integrada.

## Limites

- Os edificios sao modelos modulares de prototipo, nao arte final nem interiores
  jogaveis. Sao referencias visuais; nao adicionam marcadores automaticos de inimigos.
- As estradas ainda seguem a navegabilidade da grade. Ha curvas suavizadas,
  mas cruzamentos podem sobrepor faixas. Nao ha pontes, tuneis nem terraplenagem.
- Cabos terminam nos limites de cada caminho/chunk; em curvas muito fechadas,
  agua, forte declive ou perto de POIs, a rede pode ficar interrompida.
- O acesso da base termina no perimetro de seguranca, sem invadir o fosso.
- As alteracoes de ligacao de POIs se aplicam ao modo com streaming usado pela
  cena principal; presets antigos de mapa pequeno mantem sua logica de rotas.
- Bruma e fumaca sao efeitos leves por sprites, sem volume fisico ou colisao
  com edificios. Crateras deformaveis, ruinas detalhadas e pontes ficam para outra etapa.
- Nao foi estabelecido um novo tempo de GPU. Medir no build de desenvolvimento
  em 1080p, com mesmo ponto de camera, horario e semente, e comparar GPU Profiler
  e FPS. Se preciso, reduzir arvores distantes, props por chunk e particulas.

## Evidencias

Os testes em copia isolada e capturas ficam em `Artifacts/MapComposition`.
O validador verifica aderencia das estradas ao LOD, exclusao dos POIs, apoio da
vegetacao, base unica, repeticao da distribuicao apos regeneracao e ausencia de
erros em Play. O teste da spline verifica tambem a imagem gerada pela GPU.
O backup anterior ao trabalho fica em `Artifacts/MapComposition/Before`.

Validacao concluida no Unity 6000.6.3f1 com URP 17.6.0, em copia isolada:

- `SplineValidation.txt`: 47 verificacoes aprovadas, nenhuma falha.
- `MapValidation.txt`: 22 verificacoes aprovadas, nenhuma falha.
- `BaseRegression.txt`: 57 verificacoes aprovadas, nenhuma falha.
- Total: 126 verificacoes aprovadas, incluindo entrada e saida de Play,
  regeneracao, streaming e projecao em paisagem, esfera e cone.

Os arquivos validados foram integrados ao projeto principal. A cena de teste de
artilharia permaneceu inalterada. Alteracoes locais da cena principal foram
preservadas; o teste isolado nao substitui a verificacao de futuras edicoes nela.
O diff das fontes e configuracoes deste trabalho fica em `Changes.diff` nesta
pasta de evidencias. As capturas finais ficam em `Captures`.
