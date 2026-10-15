# Assets e cenario procedural

Cena: `Assets/Scenes/Leviathan_BaseGiratoria.unity`.
Unity 6000.6.3f1 / URP 17.6.0. Os canhoes, instrumentos, jogador, grama e
relevo existentes foram preservados. Nao ha novo HUD ou Canvas.

## Selecao dos pacotes baixados

| Fonte | Aproveitado | Uso |
|---|---|---|
| Darth Artisan / Free Trees | Oak e Fir | Bosques, bordas e arvores esparsas |
| Free / Stones | Stone 1, 3 e 5 | Pedras agrupadas e dispersas |
| Industrial Props | LightPole | Postes de madeira nas margens e no perimetro |
| Sector Warzone | Concrete Barrier 1 | Barreiras de concreto |
| Defensive Props | Sandbag barricade | Pequenas posicoes militares |
| Lowpoly Crates | Crate 01 | Caixas de suprimentos perto da base |

Palmeiras, alternativas PBR mais pesadas, demonstracoes, duplicatas HDRP/Built-in,
cenarios de estudio e recursos ainda sem uso no prototipo foram retirados de
`Assets`. Audio, radios e outros modelos destinados a possiveis etapas futuras
tambem foram guardados, nao apagados permanentemente.

- Fontes selecionadas e suas dependencias: `Assets/ThirdParty/Environment`.
- Meshes preparados para o jogo: `Assets/Models/Environment/Curated`.
- Material dos props: `Assets/Materials/Environment/CuratedProps.mat`.
- Originais fora de uso: `Artifacts/AssetCuration/Unused`, mantendo os `.meta`.
- Inventario de movimentos: `Artifacts/AssetCuration/Moves.tsv`.
- Backups dos arquivos alterados: `Artifacts/AssetCuration/Before`.

Os arquivos de licenca existentes foram preservados. O arquivamento e reversivel;
nao publicar nem distribuir os assets-fontes separadamente do jogo sem verificar
as licencas dos respectivos pacotes.

## Preparacao visual

Os materiais Standard das fontes selecionadas foram convertidos para URP/Lit.
Para renderizar grandes quantidades no mundo, `CuratedMeshBaker` combina as partes
de cada modelo em um mesh e transfere as cores/atlas para cores de vertices.
Isso elimina texturas PBR grandes nos desenhos instanciados e permite reutilizar
o shader cartum existente. As pedras recebem um tom cinza e as caixas preservam
tons de madeira. A folhagem tem vento; troncos, pedras e props ficam fixos.

Oak: 5.120 vertices proximo e aproximadamente 1.358 no LOD distante.
Fir: 1.676 vertices proximo e aproximadamente 561 no LOD distante.
O LOD distante usa agrupamento espacial de vertices, preservando cores e removendo
triangulos degenerados. Os meshes originais selecionados permanecem disponiveis
para outro acabamento. O processo pode ser repetido sem trocar os GUIDs dos meshes.

## Geracao e Inspector

`ProceduralTreeManager` usa o preset `Assets/Settings/Vegetation/ProceduralTrees.asset`:

- `Spacing`: 14 m entre celulas candidatas, com deslocamento aleatorio.
- `Density`: 0,68 antes de aplicar relevo e outras mascaras.
- `Grove Scale / Threshold`: 220 m / 0,49 para agrupar os bosques.
- `Scattered Density`: chance de manter uma candidata fora de um bosque.
- `Maximum Trees Per Chunk`: 1.400, sem concentrar a selecao em uma borda.
- `Near / Far Distance`: 125 / 700 m; sombras proximas ate 75 m.

`WorldSceneryManager`, no objeto `Cenario Procedural`, usa
`Assets/Settings/Vegetation/WorldScenery.asset`:

- Pedras: celulas de 32 m, densidade 0,32 modulada por manchas de 130 m.
- Postes e barreiras: margem das estradas, sem ocupar a faixa de passagem.
- Suprimentos e protecoes: pequeno perimetro de servico a 46 m da base unica.
- `Prototypes`: mesh, papel e faixa de escala de cada modelo selecionado.
- `Draw Distance`: 500 m; `Shadow Distance`: 75 m.
- `Builds Per Frame`: 1; `Maximum Instances Per Chunk`: 700.
- `Collision Distance`: 45 m do jogador; pool limitado a 64 MeshColliders.

Dados sao deterministas pela semente e coordenadas globais. Agua, POIs, estradas
e a area da base ficam protegidos. Alturas dos props seguem a interpolacao dos
triangulos do LOD atual; pedras sao parcialmente enterradas para integrar ao chao.
Criacao, mudanca de dados/LOD e descarregamento acompanham os eventos do terreno.
Renderizacao usa lotes instanciados, culling de frustum/distancia e matrizes
reutilizadas. Os colliders proximos sao reutilizados e atualizados a cada 0,5 s.
Ao desativar/destruir o manager, material proprio, eventos e pool sao liberados.

## Correcao do local da base

No estado recebido, o preset tinha a semente **-1204466725**, mas o preview da
base ainda guardava **-265324695**. O modelo e o recorte podiam permanecer no
local/altura de outro mapa depois de alterar o preset no Editor.

`BasePreviewCoordinator` agora verifica alteracoes do relevo, configuracao e
dependencias do prefab a cada dois segundos fora do Play. Ele recalcula o local,
reposiciona o preview e o jogador, atualiza o mapa fisico, move a janela de
streaming e regenera o terreno com a mesma semente. A cena e marcada como alterada.
O coordenador nao realiza esse polling durante Play; a base runtime continua
usando o `BaseSpawner` existente e sua propriedade unica no registro.

O tamanho de apoio e derivado do mesh `Fosso_Piso`, em coordenadas locais do prefab,
sem usar o AABB expandido pela rotacao. Um deslocamento da fundacao em relacao
ao root e compensado ao posicionar o modelo.

A busca verifica uma area de pelo menos 30 m, com limite de **25 cm de variacao**
e **2 graus de inclinacao**, sem relaxar esses valores para aceitar encostas.
Ha verificacao adicional de quatro aneis com 64 amostras cada, alem da grade.
A fonte tambem verifica as mascaras naturais de lagos/rios, antes da exclusao
de vegetacao ao redor da base. Nenhuma altura e alterada para acomodar o modelo.

O fosso continua sendo um recorte geometrico centralizado, com raio derivado
da fundacao menos 25 cm. O piso e a parede do proprio FBX fecham a abertura.
Se nao existir local valido dentro da busca, o preview fica oculto e o gerador
informa o problema; ele nao cria uma base suspensa. Aumente `Max Distance` para
procurar mais longe mantendo os limites de apoio.

A textura do mapa fisico agora usa `DestroyImmediate` fora do Play, permitindo
atualizacoes repetidas do preview sem erro de liberacao de recursos.

## Validar e ajustar

1. Reabra a cena integrada fora do Play e aguarde a janela de terreno carregar.
2. Entre em Play: confira bosques, pedras e o perimetro de servico da base.
3. Caminhe para fora da base e verifique as colisoes proximas dos props.
4. Mude a semente do preset fora do Play: apos ate dois segundos, preview,
   recorte, mapa fisico e terreno devem se atualizar juntos.
5. Repita a mesma semente para verificar reproducibilidade.
6. Ajustes nos presets de vegetacao requerem desativar/reativar seus managers
   ou regenerar o mundo para reconstruir os registros existentes.

`AssetCurationSetup.ApplyBatch` reaplica a selecao e os presets da entrega;
nao e necessario executa-lo para jogar. Os validadores batch encerram o Editor
ao terminar: use uma copia/sessao dedicada. Resultados, inventario e capturas
ficam em `Artifacts/AssetCuration`.

Validacao da entrega: **61 verificacoes de curadoria/geracao e 49 de
base/artilharia passaram** em uma copia isolada. Dez sementes encontraram apoio
seco e reproduzivel. O preview foi testado ao trocar de semente fora do Play,
incluindo sua janela de streaming. O recorte foi verificado por raycasts dentro
e fora do fosso e comparado ao centro fisico do modelo. A sessao capturada tinha
12.549 arvores e 1.726 props nos chunks carregados, com seis colliders proximos
ativos. Essas contagens dependem da camera, semente e residencia dos chunks.
A cena isolada de teste de artilharia permaneceu com o mesmo SHA-256.
`Changes.diff` mostra as alteracoes dos arquivos existentes em relacao aos backups.

## Limites

Esta etapa nao cria cidades, inimigos, energia eletrica, luzes noturnas nos postes
nem interacao com caixas. Arvores ainda nao possuem colisao de tronco. Props
instanciados nao sao GameObjects individuais; apenas os colliders proximos existem
na Hierarchy. O limite do pool pode omitir colisao em uma regiao excepcionalmente
cheia. Grama nao recebe uma mascara individual para cada pedra pequena.

O custo de GPU do conjunto nao foi medido nesta entrega. Para reduzir custo,
ajuste primeiro sombras e distancia, depois densidade e meshes proximos.
O LOD de copas nao possui crossfade. O sistema de LOD do terreno existente
continua separado; esta etapa nao implementa costura geometrica de suas malhas.
Nao e uma garantia de encontrar apoio em qualquer preset arbitrario, por exemplo
um mapa inteiramente submerso ou sem planicies.
