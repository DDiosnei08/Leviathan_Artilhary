# Ambiente, vegetacao e erosao

Atualizacao: o [documento 16](16-assets-e-cenario-procedural.md) substitui os meshes
de arvores desta etapa pelos downloads selecionados e reforca o apoio/sincronizacao da base.

Unity 6000.6.3f1, URP 17.6.0, PC/DX11. A melhoria esta integrada em
`Assets/Scenes/Leviathan_BaseGiratoria.unity`, mantendo o modelo, os dois canhoes,
o jogador, os instrumentos fisicos e o mapa. Nao foram adicionados Canvas ou HUD.

## Base sobre terreno natural

Esta decisao substitui o aterro e o rebaixamento radial descritos no documento 14.
`BaseSiteData.ModifyHeight` agora preserva integralmente a altura natural.
A base permanece unica e deterministica para cada semente; nao se move por frame.
Mudar a semente faz uma nova busca, mantendo apenas uma instancia ativa.

O preset verifica um circulo de 30 m, com variacao maxima de altura de 1 m,
inclinacao maxima de 3 graus e margem acima da agua. A busca usa 1.200 candidatos
ate 1.800 m do centro de busca, sem relaxar os limites para aceitar encostas.
A base fica sobre a maior altura da area mais 2 cm, sem alterar o heightmap.
Se nenhuma area passar, o gerador informa o erro e nao cria uma base suspensa.
Aumente a area de busca nesse caso; nao compense criando um morro artificial.

O FBX possui um fosso embutido. Uma abertura circular de 21,9 m e recortada
somente nos triangulos do terreno e no MeshCollider, revelando o piso do modelo.
Os vertices remanescentes continuam sobre os triangulos naturais. Nao ha
terraplenagem, parede artificial nem alteracao da altura para acomodar o FBX.
`blendRadius`, `pitDepth` e `carveRamp` antigos continuam serializados por
compatibilidade, mas nao esculpem mais o terreno. A abertura e uma adaptacao
geometrica local necessaria ao modelo existente.

## Grama

Preset da cena: `Assets/Settings/Grass/BaseGiratoriaGrass.asset`.
O preset original `ProceduralGrass.asset` foi preservado.

| Parametro | Valor inicial | Efeito |
|---|---|---|
| Blades Per Square Meter | 9 | Candidatos por metro quadrado antes das mascaras |
| Height Range | 0,55 a 0,95 m | Grama mais alta |
| Width Range | 0,045 a 0,075 m | Mais cobertura visivel |
| Near / Middle / Far Distance | 28 / 65 / 120 m | Geometria e alcance de renderizacao |
| Middle / Far Density | 0,65 / 0,22 | Cobertura dos LODs distantes |
| Maximum Resident Patches | 80 | Limite de residencia de buffers |
| Generations Per Frame | 3 | Orcamento de novos patches |

O LOD do chunk nao reduz mais a densidade das laminas muito proximas da camera;
ele so limita os LODs a partir da distancia media. Isso evita perder cobertura
porque o centro de um chunk de 1,2 km esta longe, mesmo quando o jogador esta nele.
Roots e normais usam as alturas finais erodidas e a malha do LOD ativo.
A exclusao da base passou a usar seu raio fisico mais uma margem de meio pixel,
em vez do antigo raio de aterro. Agua, vias, POIs e volumes continuam excluidos.
O fallback DX11 de instanciamento indireto existente foi mantido.

## Arvores

- Componente: `ProceduralTreeManager`, no objeto `Arvores Procedurais`.
- Configuracao: `Assets/Settings/Vegetation/ProceduralTrees.asset`.
- Material: `Assets/Materials/Vegetation/TreeCartoon.mat`.
- Meshes: `Assets/Models/Vegetation`, copas largas e pinheiros com dois LODs.
- Shader URP: `Assets/Shaders/Vegetation/TreeCartoon.shader`.

As arvores sao meshes 3D low-poly geradas por codigo, com tronco e copa,
cores de vertices, sombras principais, neblina e vento discreto na folhagem.
Ha instanciamento em lotes de ate 1.023, culling de frustum/distancia e sombras
apenas dentro de `Shadow Distance`. Nao existem milhares de GameObjects de arvores.

Distribuicao usa celulas globais e hash da semente, com agrupamento regional.
Mascaras eliminam agua, estradas, POIs, encostas e a area da base. O tronco e
ajustado pela interpolacao triangular da malha do LOD atual, nao por raycast.
Ao atingir o limite por chunk, reservoir sampling evita concentrar todas as
arvores no inicio da varredura. Um chunk e reconstruido por frame por padrao;
criacao, mudanca de dados/LOD e descarregamento usam os callbacks do terreno.

No Inspector ajuste `Spacing`, `Density`, `Grove Scale/Threshold`, `Heights`,
`Maximum Slope`, `Normalized Altitude`, `Near/Far Distance`, `Shadow Distance`
e `Wind Direction/Strength`. `TreeCount` e `VisibleTrees` sao contadores acessiveis
no componente pelo codigo/depurador, sem interface sobre a visao do jogador.

## Erosao simples

Em `BaseGiratoriaTerrain.asset`, abra o grupo `Erosion`:

| Parametro | Valor inicial | Efeito |
|---|---|---|
| Enabled | Ligado | Aplica o processamento antes das malhas |
| Channel Scale | 140 m | Tamanho dos sulcos |
| Channel Depth | 7 m | Profundidade maxima aproximada |
| Channel Width | 0,12 | Largura relativa dos sulcos |
| Thermal Iterations | 1 | Passes de relaxamento local |
| Thermal Strength | 0,18 | Intensidade da troca entre vizinhos |
| Talus Angle | 35 graus | Encosta a partir da qual ha relaxamento |

E uma aproximacao estilizada: incisao por ruido de crista e relaxamento termico
simetrico, nao uma simulacao hidraulica de chuva/sedimentos. A mascara das
planicies existentes protege as areas planas. Coordenadas mundiais e um halo
de amostras mantem altura e derivadas continuas nas bordas entre chunks.
O detalhe de superficie usa escala de 100 m e a suavizacao foi reduzida.
As texturas dos biomas continuam pelas UVs e `_BaseMap` existentes; somente o
tom seco/campos do preset da cena foi ajustado para combinar com a vegetacao.

## Testar

1. Pare o Play e reabra `Leviathan_BaseGiratoria` para carregar a cena atualizada.
2. Entre em Play e aguarde o preenchimento inicial dos chunks e dos patches.
3. Use a vista geral e caminhe para fora do anel da base: confira altura da
   grama, movimento da folhagem, copas/pinheiros e ausencia do pedestal.
4. Altere a semente e regenere: a base deve procurar outro ponto valido;
   repetir a mesma semente reproduz o local e a distribuicao da vegetacao.
5. Ajustes no preset de grama exigem `GrassManager.Rebuild`; alteracoes de
   erosao exigem `TerrainWorldController.RegenerateTerrain`.
6. `Leviathan > Environment > Validar ambiente em Play` executa os testes e
   encerra o Editor ao terminar. Use em uma sessao dedicada de validacao.

`Enriquecer cena atual` reabre a cena integrada e reaplica os presets iniciais;
nao e necessario usar esse menu para jogar e ele substitui ajustes desses parametros.
Validacao e capturas desta entrega ficam em `Artifacts/EnvironmentEnrichment`.

Validacao executada em uma copia isolada do projeto: **23 verificacoes do
ambiente e 49 da base/artilharia passaram**, incluindo tres sementes, duas
sessoes de Play sem recarregar a cena e acionamento dos instrumentos fisicos.
Alturas e amostras para normais nas bordas tiveram erro zero. A colocacao da
base nao alterou nenhum valor de altura; o local escolhido variou 0,145 m.
No teste proximo havia 44.308 laminas visiveis e o erro maximo das raizes e
troncos ficou abaixo de 0,13 mm. Contagens variam conforme camera/streaming.
O preset anterior e a cena anterior foram guardados em `Before`, fora de Assets.
A restauracao do asset original ao sair do Play foi reforcada para evitar uma
referencia ao clone destruido quando o reload de cena esta desabilitado.

## Limites e desempenho

Nao foi medida novamente a meta anterior de 2 ms na RX 6600 com a maior densidade.
Uma captura visual ou contagem de instancias nao substitui um tempo valido de GPU.
Meça em build 1080p com Unity Profiler GPU/RenderDoc, comparando grama ligada e
desligada. Se exceder o orcamento, reduza sombras, `Far Distance`, densidades de
LOD distante e depois `Blades Per Square Meter`/residencia.

Arvores nao possuem colisao, destruicao, billboards, oclusao Hi-Z ou crossfade
entre copas de LOD diferentes; pode haver uma troca visivel em 150 m. Elas nao
interferem na artilharia nem implementam qualquer logica de inimigo.
Reconstruir um chunk aloca seus registros de arvores, mas os lotes e planos de
renderizacao sao reutilizados, sem alocacao por arvore em cada frame.
Erosao ocorre durante a geracao, nao todo frame. As planicies continuam suaves
por intencao para preservar locais naturais onde a base cabe.
